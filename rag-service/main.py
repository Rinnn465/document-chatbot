from functools import lru_cache
from typing import TYPE_CHECKING, Literal

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, ConfigDict, Field

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
