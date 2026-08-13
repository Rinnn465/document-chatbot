from functools import lru_cache
from typing import TYPE_CHECKING, Literal

from fastapi import FastAPI, HTTPException, Response
from pydantic import BaseModel, ConfigDict, Field, model_validator

if TYPE_CHECKING:
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


app = FastAPI(title="Document Chatbot RAG Service", version="1.0.0")


@lru_cache(maxsize=1)
def get_rag() -> "RAGPipeline":
    from rag import RAGPipeline

    return RAGPipeline()


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/ask", response_model=AskResponse)
def ask(request: AskRequest) -> AskResponse:
    try:
        result = get_rag().answer(
            question=request.question.strip(),
            conversation_history=[message.model_dump() for message in request.conversation_history],
        )
        return AskResponse.model_validate(result)
    except ValueError as exception:
        raise HTTPException(status_code=503, detail=str(exception)) from exception


@app.post("/documents", response_model=IngestResponse)
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


@app.delete("/documents/{document_id}", status_code=204)
def delete_document(document_id: str) -> Response:
    try:
        get_rag().delete_document(document_id)
        return Response(status_code=204)
    except ValueError as exception:
        raise HTTPException(status_code=503, detail=str(exception)) from exception
