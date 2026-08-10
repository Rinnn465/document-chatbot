import hashlib
import os
from pathlib import Path
from typing import Any

from dotenv import load_dotenv
from langchain_chroma import Chroma
from openai import OpenAI

from embeddings import SentenceTransformerEmbeddings
from prompt import (
    OUT_OF_SCOPE_ANSWER,
    REWRITE_PROMPT,
    SYSTEM_PROMPT,
    build_answer_input,
    build_rewrite_input,
)

load_dotenv()


class RAGPipeline:
    def __init__(self) -> None:
        api_key = os.getenv("OPENAI_API_KEY")
        if not api_key:
            raise ValueError("OPENAI_API_KEY is not configured for the RAG service.")

        self.top_k = int(os.getenv("TOP_K", "5"))
        self.minimum_relevance_score = float(os.getenv("MIN_RELEVANCE_SCORE", "0.25"))
        self.enable_query_rewrite = os.getenv("ENABLE_QUERY_REWRITE", "true").lower() == "true"
        self.model = os.getenv("OPENAI_MODEL", "gpt-5.4-mini")
        self.reasoning_effort = os.getenv("OPENAI_REASONING_EFFORT", "low")

        self.client = OpenAI(api_key=api_key)
        self.vector_db = Chroma(
            collection_name=os.getenv("CHROMA_COLLECTION", "course_documents"),
            persist_directory=os.getenv("CHROMA_DIR", "chroma_db"),
            embedding_function=SentenceTransformerEmbeddings(),
        )

    def answer(
        self,
        question: str,
        conversation_history: list[dict[str, str]],
    ) -> dict[str, Any]:
        retrieval_query = self._rewrite_question(question, conversation_history)
        retrieved = self.vector_db.similarity_search_with_relevance_scores(
            retrieval_query,
            k=self.top_k,
        )
        relevant = [
            (document, float(score))
            for document, score in retrieved
            if score is not None and float(score) >= self.minimum_relevance_score
        ]

        if not relevant:
            return self._not_grounded()

        contexts: list[tuple[str, str]] = []
        sources: list[dict[str, Any]] = []
        for index, (document, score) in enumerate(relevant, start=1):
            label = f"S{index}"
            contexts.append((label, document.page_content))
            sources.append(self._citation(document, score, label))

        response = self.client.responses.create(
            model=self.model,
            instructions=SYSTEM_PROMPT,
            input=build_answer_input(question, conversation_history, contexts),
            reasoning={"effort": self.reasoning_effort},
            text={"verbosity": "low"},
            store=False,
        )
        answer_text = (getattr(response, "output_text", "") or "").strip()

        if not answer_text or answer_text == OUT_OF_SCOPE_ANSWER:
            return self._not_grounded()

        return {
            "answer": answer_text,
            "grounded": True,
            "sources": sources,
            "context_count": len(relevant),
        }

    def _rewrite_question(
        self,
        question: str,
        conversation_history: list[dict[str, str]],
    ) -> str:
        if not self.enable_query_rewrite or not conversation_history:
            return question

        try:
            response = self.client.responses.create(
                model=self.model,
                instructions=REWRITE_PROMPT,
                input=build_rewrite_input(question, conversation_history),
                text={"verbosity": "low"},
                store=False,
            )
            rewritten = (getattr(response, "output_text", "") or "").strip()
            return rewritten or question
        except Exception as exception:
            print(f"[RAG] Query rewrite failed; using original question: {exception}")
            return question

    @staticmethod
    def _citation(document: Any, score: float, label: str) -> dict[str, Any]:
        metadata = document.metadata or {}
        source = str(metadata.get("source") or metadata.get("document_name") or "").strip()
        document_name = str(metadata.get("document_name") or Path(source).name).strip()
        document_id = str(metadata.get("document_id") or source or document_name).strip()
        raw_chunk_id = str(metadata.get("chunk_id") or "").strip()
        chunk_id = raw_chunk_id or hashlib.sha1(
            f"{document_id}:{document.page_content}".encode("utf-8")
        ).hexdigest()[:16]

        return {
            "chunk_id": chunk_id,
            "document_id": document_id,
            "document_name": document_name,
            "chapter": metadata.get("chapter"),
            "page_number": _optional_int(metadata.get("page_number") or metadata.get("page")),
            "slide_number": _optional_int(metadata.get("slide_number")),
            "excerpt": _compact_excerpt(document.page_content),
            "relevance_score": round(score, 4),
            "label": label,
        }

    @staticmethod
    def _not_grounded() -> dict[str, Any]:
        return {
            "answer": OUT_OF_SCOPE_ANSWER,
            "grounded": False,
            "sources": [],
            "context_count": 0,
        }


def _optional_int(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _compact_excerpt(text: str, maximum_length: int = 280) -> str:
    compact = " ".join(text.split())
    if len(compact) <= maximum_length:
        return compact
    return compact[: maximum_length - 1].rstrip() + "…"
