from functools import lru_cache
import os
import secrets
from pathlib import Path
from typing import TYPE_CHECKING, Literal

from fastapi import Depends, FastAPI, Header, HTTPException, Query, Response, status
from pydantic import BaseModel, ConfigDict, Field, model_validator

if TYPE_CHECKING:
    from knowledge_store import KnowledgeSnapshotStore
    from rag import RAGPipeline


def to_camel(value: str) -> str:
    first, *rest = value.split("_")
    return first + "".join(part.capitalize() for part in rest)


class ApiModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class ConversationMessage(ApiModel):
    role: Literal["user", "assistant"]
    content: str = Field(min_length=1, max_length=4_000)


class AskRequest(ApiModel):
    question: str = Field(min_length=2, max_length=2_000)
    conversation_history: list[ConversationMessage] = Field(
        default_factory=list,
        max_length=8,
    )


class CitationResponse(ApiModel):
    chunk_id: str
    document_id: str
    document_name: str
    chapter: str | None = None
    page_number: int | None = None
    slide_number: int | None = None
    excerpt: str
    relevance_score: float


class AskResponse(ApiModel):
    answer: str
    grounded: bool
    sources: list[CitationResponse]
    context_count: int


class DocumentSection(ApiModel):
    section_type: Literal["document", "page", "slide"]
    section_number: int | None = Field(default=None, ge=1)
    title: str | None = Field(default=None, max_length=500)
    content: str = ""


class IngestRequest(ApiModel):
    document_id: str = Field(min_length=1, max_length=100)
    document_name: str = Field(min_length=1, max_length=255)
    chapter: str | None = Field(default=None, max_length=255)
    sections: list[DocumentSection] = Field(default_factory=list)
    text: str | None = None

    @model_validator(mode="after")
    def require_document_content(self) -> "IngestRequest":
        has_sections = any(section.content.strip() or (section.title or "").strip() for section in self.sections)
        if not has_sections and not (self.text or "").strip():
            raise ValueError("Either sections or text must contain extractable content.")
        return self


class IngestResponse(ApiModel):
    document_id: str
    chunk_count: int


class DocumentChunkResponse(ApiModel):
    chunk_id: str
    chunk_index: int
    section_type: str
    section_index: int
    section_number: int | None = None
    section_chunk_index: int
    section_title: str | None = None
    content_hash: str
    content: str


class DocumentChunksResponse(ApiModel):
    document_id: str
    document_name: str
    chapter: str | None = None
    total_count: int
    page: int
    page_size: int
    items: list[DocumentChunkResponse]


app = FastAPI(title="Document Chatbot RAG Service", version="1.0.0")


def read_secret(name: str) -> str:
    direct_value = os.getenv(name, "").strip()
    if direct_value:
        return direct_value

    secret_path = os.getenv(f"{name}_FILE", "").strip()
    if not secret_path:
        return ""

    try:
        return Path(secret_path).read_text(encoding="utf-8").strip()
    except OSError as exception:
        raise RuntimeError(f"Cannot read {name} from its secret file.") from exception


def require_service_token(
    supplied_token: str | None = Header(default=None, alias="X-RAG-Service-Token"),
) -> None:
    expected_token = read_secret("RAG_SERVICE_TOKEN")
    if not expected_token:
        return
    if not supplied_token or not secrets.compare_digest(supplied_token, expected_token):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid RAG service token.",
        )


@lru_cache(maxsize=1)
def get_rag() -> "RAGPipeline":
    from rag import RAGPipeline

    return RAGPipeline()


@lru_cache(maxsize=1)
def get_knowledge_store() -> "KnowledgeSnapshotStore":
    from knowledge_store import KnowledgeSnapshotStore

    return KnowledgeSnapshotStore(os.getenv("KNOWLEDGE_DIR", "knowledge"))


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/ask", response_model=AskResponse, dependencies=[Depends(require_service_token)])
def ask(request: AskRequest) -> AskResponse:
    try:
        result = get_rag().answer(
            question=request.question.strip(),
            conversation_history=[message.model_dump() for message in request.conversation_history],
        )
        return AskResponse.model_validate(result)
    except ValueError as exception:
        raise HTTPException(status_code=503, detail=str(exception)) from exception


@app.post("/documents", response_model=IngestResponse, dependencies=[Depends(require_service_token)])
def ingest_document(request: IngestRequest) -> IngestResponse:
    try:
        count = get_rag().ingest_document(
            document_id=request.document_id.strip(),
            document_name=request.document_name.strip(),
            chapter=request.chapter.strip() if request.chapter else None,
            sections=[section.model_dump() for section in request.sections],
            text=request.text,
        )
        return IngestResponse(document_id=request.document_id, chunk_count=count)
    except ValueError as exception:
        raise HTTPException(status_code=503, detail=str(exception)) from exception


@app.delete(
    "/documents/{document_id}",
    status_code=204,
    dependencies=[Depends(require_service_token)],
)
def delete_document(document_id: str) -> Response:
    try:
        get_rag().delete_document(document_id)
        return Response(status_code=204)
    except ValueError as exception:
        raise HTTPException(status_code=503, detail=str(exception)) from exception


@app.get(
    "/documents/{document_id}/chunks",
    response_model=DocumentChunksResponse,
    dependencies=[Depends(require_service_token)],
)
def get_document_chunks(
    document_id: str,
    page: int = Query(default=1, ge=1),
    page_size: int = Query(default=20, alias="pageSize", ge=1, le=100),
) -> DocumentChunksResponse:
    try:
        result = get_knowledge_store().get_document_chunks(document_id, page, page_size)
        if result is None:
            raise HTTPException(status_code=404, detail="Indexed document snapshot was not found.")
        return DocumentChunksResponse.model_validate(result)
    except HTTPException:
        raise
    except ValueError as exception:
        raise HTTPException(status_code=503, detail=str(exception)) from exception
