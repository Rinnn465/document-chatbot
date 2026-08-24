import hashlib
import os
import re
import sys
import unicodedata
from pathlib import Path
from threading import RLock
from typing import Any

from dotenv import load_dotenv
from langchain_chroma import Chroma
from openai import OpenAI

from embeddings import SentenceTransformerEmbeddings
from knowledge_store import KnowledgeSnapshotStore
from prompt import (
    OUT_OF_SCOPE_ANSWER,
    GROUNDED_RETRY_INSTRUCTION,
    REWRITE_PROMPT,
    SYSTEM_PROMPT,
    build_answer_input,
    build_rewrite_input,
)

load_dotenv()

CHUNK_SIZE = 2_500
CHUNK_OVERLAP = 250


class RAGPipeline:
    def __init__(self) -> None:
        api_key = _read_secret_env("OPENAI_API_KEY")
        if not api_key:
            raise ValueError("OPENAI_API_KEY is not configured for the RAG service.")

        self.top_k = int(os.getenv("TOP_K", "5"))
        self.minimum_relevance_score = float(os.getenv("MIN_RELEVANCE_SCORE", "0.25"))
        self.enable_query_rewrite = os.getenv("ENABLE_QUERY_REWRITE", "true").lower() == "true"
        self.model = os.getenv("OPENAI_MODEL", "gpt-5.4-mini")
        self.reasoning_effort = os.getenv("OPENAI_REASONING_EFFORT", "low")
        self.answer_retry_score = float(os.getenv("ANSWER_RETRY_SCORE", "0.60"))
        self._query_rewrite_cache: dict[str, list[str]] = {}
        self._answer_cache: dict[str, dict[str, Any]] = {}
        self._cache_lock = RLock()

        self.client = OpenAI(api_key=api_key)
        embedding_function = SentenceTransformerEmbeddings()
        self.embedding_model = embedding_function.model_name
        self.snapshot_store = KnowledgeSnapshotStore(
            os.getenv("KNOWLEDGE_DIR", "knowledge")
        )
        self.vector_db = Chroma(
            collection_name=os.getenv("CHROMA_COLLECTION", "course_documents"),
            persist_directory=os.getenv("CHROMA_DIR", "chroma_db"),
            embedding_function=embedding_function,
        )

    def ingest_document(
        self,
        document_id: str,
        document_name: str,
        chapter: str | None,
        text: str | None = None,
        sections: list[dict[str, Any]] | None = None,
    ) -> int:
        document_sections = _normalize_sections(text, sections)
        chunk_records = _build_chunk_records(document_name, chapter, document_sections)
        if not chunk_records:
            return 0

        existing = self.vector_db.get(
            where={"document_id": document_id},
            include=[],
        )
        existing_ids = set(existing.get("ids") or [])
        metadatas = []
        ids = []
        texts = []
        snapshot_chunks = []
        for index, record in enumerate(chunk_records, start=1):
            section_type = record["section_type"]
            section_number = record["section_number"]
            section_chunk_index = record["section_chunk_index"]
            section_identity = section_number or record["section_index"]
            chunk_id = f"{document_id}:{section_type}:{section_identity}:{section_chunk_index}"
            ids.append(chunk_id)
            metadata: dict[str, Any] = {
                "chunk_id": chunk_id,
                "document_id": document_id,
                "document_name": document_name,
                "source": document_name,
                "chunk_index": index,
                "section_type": section_type,
                "section_index": record["section_index"],
                "section_chunk_index": section_chunk_index,
            }
            if chapter:
                metadata["chapter"] = chapter
            if section_number is not None:
                metadata["section_number"] = section_number
                if section_type == "page":
                    metadata["page_number"] = section_number
                elif section_type == "slide":
                    metadata["slide_number"] = section_number
            if record["section_title"]:
                metadata["section_title"] = record["section_title"]
            metadatas.append(metadata)
            texts.append(record["text"])
            snapshot_chunks.append(
                {
                    "chunkId": chunk_id,
                    "chunkIndex": index,
                    "sectionType": section_type,
                    "sectionIndex": record["section_index"],
                    "sectionNumber": section_number,
                    "sectionChunkIndex": section_chunk_index,
                    "sectionTitle": record["section_title"],
                    "contentHash": hashlib.sha256(record["text"].encode("utf-8")).hexdigest(),
                    "content": record["text"],
                }
            )

        self.vector_db.add_texts(texts=texts, metadatas=metadatas, ids=ids)
        stale_ids = sorted(existing_ids.difference(ids))
        if stale_ids:
            self.vector_db.delete(ids=stale_ids)
        self.snapshot_store.save_document(
            document_id=document_id,
            document_name=document_name,
            chapter=chapter,
            sections=document_sections,
            chunks=snapshot_chunks,
            index_signature={
                "embeddingModel": getattr(self, "embedding_model", "unknown"),
                "chunkSize": CHUNK_SIZE,
                "chunkOverlap": CHUNK_OVERLAP,
            },
        )
        self._clear_answer_cache()
        return len(texts)

    def delete_document(self, document_id: str) -> None:
        matches = self.vector_db.get(
            where={"document_id": document_id},
            include=[],
        )
        ids = matches.get("ids") or []
        if ids:
            self.vector_db.delete(ids=ids)
        self.snapshot_store.delete_document(document_id)
        self._clear_answer_cache()

    def answer(
        self,
        question: str,
        conversation_history: list[dict[str, str]],
    ) -> dict[str, Any]:
        answer_cache_key = _conversation_cache_key(question, conversation_history)
        cached_answer = self._get_cached_value("_answer_cache", answer_cache_key)
        if cached_answer is not None:
            cached_result = _copy_answer_result(cached_answer)
            source_log = ", ".join(
                _format_source_log(source)
                for source in cached_result["sources"]
            )
            _log(f"[RAG] Grounded answer cache hit: {source_log}")
            return cached_result

        english_queries = self._rewrite_queries(question, conversation_history)
        _log(
            f"[RAG] Retrieval queries: original={question!r} | english={english_queries!r}"
        )
        retrieved = self._retrieve_documents(question, english_queries)
        relevant = [
            (document, float(score))
            for document, score in retrieved
            if score is not None and float(score) >= self.minimum_relevance_score
        ]

        candidate_log = ", ".join(
            _format_retrieval_candidate(document, score)
            for document, score in relevant
        )
        _log(f"[RAG] Retrieved candidates: {candidate_log or '(none)'}")

        if not relevant:
            return self._not_grounded()

        contexts: list[tuple[str, str]] = []
        sources: list[dict[str, Any]] = []
        for index, (document, score) in enumerate(relevant, start=1):
            label = f"S{index}"
            contexts.append((label, document.page_content))
            sources.append(self._citation(document, score, label))

        answer_input = build_answer_input(question, conversation_history, contexts)
        answer_text = self._generate_answer(answer_input, SYSTEM_PROMPT)

        if (
            _is_out_of_scope_answer(answer_text)
            and max(score for _, score in relevant)
            >= getattr(self, "answer_retry_score", 0.60)
        ):
            _log("[RAG] High-confidence evidence returned OUT_OF_SCOPE; retrying once.")
            try:
                answer_text = self._generate_answer(
                    answer_input,
                    f"{SYSTEM_PROMPT}\n\n{GROUNDED_RETRY_INSTRUCTION}",
                )
            except Exception as exception:
                _log(f"[RAG] Grounded answer retry failed: {exception}")

        if _is_out_of_scope_answer(answer_text):
            return self._not_grounded()

        answer_text, cited_sources = _select_sources_and_clean_answer(answer_text, sources)
        if not cited_sources:
            return self._not_grounded()

        answer_text = _remove_document_preface(answer_text)
        if not answer_text:
            return self._not_grounded()

        source_log = ", ".join(_format_source_log(source) for source in cited_sources)
        _log(f"[RAG] Sources used: {source_log}")

        result = {
            "answer": answer_text,
            "grounded": True,
            "sources": cited_sources,
            "context_count": len(cited_sources),
        }
        self._set_cached_value("_answer_cache", answer_cache_key, result)
        return _copy_answer_result(result)

    def _generate_answer(self, answer_input: str, instructions: str) -> str:
        response = self.client.responses.create(
            model=self.model,
            instructions=instructions,
            input=answer_input,
            reasoning={"effort": self.reasoning_effort},
            text={"verbosity": "low"},
            store=False,
        )
        answer_text = unicodedata.normalize(
            "NFC",
            (getattr(response, "output_text", "") or "").strip(),
        )
        return _remove_contradictory_fallback(answer_text)

    def _rewrite_queries(
        self,
        question: str,
        conversation_history: list[dict[str, str]],
    ) -> list[str]:
        if not self.enable_query_rewrite:
            return []

        cache_key = _conversation_cache_key(question, conversation_history)
        cached_queries = self._get_cached_value("_query_rewrite_cache", cache_key)
        if cached_queries is not None:
            return list(cached_queries)

        try:
            response = self.client.responses.create(
                model=self.model,
                instructions=REWRITE_PROMPT,
                input=build_rewrite_input(question, conversation_history),
                text={"verbosity": "low"},
                temperature=0,
                store=False,
            )
            rewritten = (getattr(response, "output_text", "") or "").strip()
            queries = _parse_retrieval_queries(rewritten)
            comparison_text = " ".join([question, *queries[:1]])
            selected_queries = (
                queries[:3]
                if _is_comparison_question(comparison_text)
                else queries[:1]
            )
            self._set_cached_value(
                "_query_rewrite_cache",
                cache_key,
                list(selected_queries),
            )
            return selected_queries
        except Exception as exception:
            _log(f"[RAG] Query rewrite failed; using original question: {exception}")
            return []

    def _get_cached_value(self, attribute: str, key: str) -> Any | None:
        lock = self._get_cache_lock()
        with lock:
            cache = getattr(self, attribute, None)
            if cache is None:
                cache = {}
                setattr(self, attribute, cache)
            return cache.get(key)

    def _set_cached_value(self, attribute: str, key: str, value: Any) -> None:
        lock = self._get_cache_lock()
        with lock:
            cache = getattr(self, attribute, None)
            if cache is None:
                cache = {}
                setattr(self, attribute, cache)
            if len(cache) >= 256 and key not in cache:
                cache.pop(next(iter(cache)))
            cache[key] = value

    def _clear_answer_cache(self) -> None:
        lock = self._get_cache_lock()
        with lock:
            cache = getattr(self, "_answer_cache", None)
            if cache is not None:
                cache.clear()

    def _get_cache_lock(self) -> RLock:
        lock = getattr(self, "_cache_lock", None)
        if lock is None:
            lock = RLock()
            self._cache_lock = lock
        return lock

    def _retrieve_documents(
        self,
        original_question: str,
        english_queries: str | list[str],
    ) -> list[tuple[Any, float]]:
        if isinstance(english_queries, str):
            english_queries = [english_queries]

        queries = [original_question]
        for query in english_queries:
            if query and all(query.casefold().strip() != item.casefold().strip() for item in queries):
                queries.append(query)

        candidates_per_query = max(self.top_k * 2, self.top_k)
        merged: dict[str, tuple[Any, float]] = {}
        matches_by_query: list[list[tuple[Any, float]]] = []
        for query in queries:
            matches = self.vector_db.similarity_search_with_relevance_scores(
                query,
                k=candidates_per_query,
            )
            normalized_matches = [
                (document, float(raw_score))
                for document, raw_score in matches
                if raw_score is not None
            ]
            matches_by_query.append(normalized_matches)
            for document, score in normalized_matches:
                chunk_key = _document_chunk_key(document)
                current = merged.get(chunk_key)
                if current is None or score > current[1]:
                    merged[chunk_key] = (document, score)

        selected: list[tuple[Any, float]] = []
        selected_keys: set[str] = set()

        # Reserve the best result from each generated English sub-query. This
        # gives comparison questions evidence for both concepts instead of
        # allowing one side to occupy every context slot.
        if len(english_queries) > 1:
            for matches in matches_by_query[1:]:
                for document, score in matches:
                    chunk_key = _document_chunk_key(document)
                    if chunk_key not in selected_keys:
                        selected.append(merged[chunk_key])
                        selected_keys.add(chunk_key)
                        break
                if len(selected) >= self.top_k:
                    return selected[: self.top_k]

        for document, score in sorted(merged.values(), key=lambda item: item[1], reverse=True):
            chunk_key = _document_chunk_key(document)
            if chunk_key in selected_keys:
                continue
            selected.append((document, score))
            selected_keys.add(chunk_key)
            if len(selected) >= self.top_k:
                break
        return selected

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
            "chunk_index": _optional_int(metadata.get("chunk_index")),
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


def _read_secret_env(name: str) -> str:
    direct_value = os.getenv(name, "").strip()
    if direct_value:
        return direct_value

    secret_path = os.getenv(f"{name}_FILE", "").strip()
    if not secret_path:
        return ""
    try:
        return Path(secret_path).read_text(encoding="utf-8").strip()
    except OSError as exception:
        raise ValueError(f"Cannot read {name} from its secret file.") from exception


def _log(message: str) -> None:
    encoding = getattr(sys.stdout, "encoding", None) or "utf-8"
    safe_message = message.encode(encoding, errors="backslashreplace").decode(encoding)
    print(safe_message, flush=True)


_CONTEXT_REFERENCE_PATTERN = re.compile(
    r"\b(?:nó|đó|này|vừa nêu|ở trên|phần trên|câu trước|tiếp theo|thế còn)\b",
    flags=re.IGNORECASE,
)


def _conversation_cache_key(
    question: str,
    conversation_history: list[dict[str, str]],
) -> str:
    normalized_question = unicodedata.normalize(
        "NFC",
        _normalize_inline(question).casefold(),
    )
    key_material = normalized_question
    if _CONTEXT_REFERENCE_PATTERN.search(normalized_question):
        history_material = "\n".join(
            f"{item.get('role', '').casefold()}:{_normalize_inline(item.get('content', '')).casefold()}"
            for item in conversation_history[-8:]
        )
        key_material = f"{normalized_question}\n{history_material}"
    return hashlib.sha256(key_material.encode("utf-8")).hexdigest()


def _copy_answer_result(result: dict[str, Any]) -> dict[str, Any]:
    return {
        **result,
        "sources": [dict(source) for source in result.get("sources", [])],
    }


def _remove_contradictory_fallback(answer_text: str) -> str:
    # Some models append the fallback after a grounded answer even though the
    # prompt asks for one or the other. Keep the grounded portion.
    if OUT_OF_SCOPE_ANSWER in answer_text and answer_text != OUT_OF_SCOPE_ANSWER:
        return answer_text.replace(OUT_OF_SCOPE_ANSWER, "").strip()
    return answer_text


def _is_out_of_scope_answer(answer_text: str) -> bool:
    return not answer_text or answer_text.strip() == OUT_OF_SCOPE_ANSWER


def _optional_int(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


_DOCUMENT_PREFACE_PATTERN = re.compile(
    r"^\s*(?:"
    r"theo\s+(?:các\s+)?tài\s+liệu"
    r"|dựa\s+(?:trên|theo)\s+(?:các\s+)?tài\s+liệu"
    r"|theo\s+(?:các\s+)?nguồn"
    r"|(?:các\s+)?tài\s+liệu\s+(?:cho\s+biết|mô\s+tả|nêu)"
    r")\s*(?:\[[Ss]\d+\])?\s*[:：,-]?\s*",
    flags=re.IGNORECASE,
)


def _remove_document_preface(value: str) -> str:
    return _DOCUMENT_PREFACE_PATTERN.sub("", value, count=1).lstrip()


def _compact_excerpt(text: str, maximum_length: int = 280) -> str:
    compact = " ".join(text.split())
    if len(compact) <= maximum_length:
        return compact
    return compact[: maximum_length - 1].rstrip() + "…"


def _select_sources_and_clean_answer(
    answer_text: str,
    sources: list[dict[str, Any]],
) -> tuple[str, list[dict[str, Any]]]:
    citation_pattern = re.compile(r"\[(S\d+)\]", flags=re.IGNORECASE)
    available_sources = {
        source["label"].upper(): source
        for source in sources
    }
    cited_labels = list(dict.fromkeys(
        match.group(1).upper()
        for match in citation_pattern.finditer(answer_text)
        if match.group(1).upper() in available_sources
    ))

    if not cited_labels:
        if not sources:
            return answer_text, []
        fallback_source = dict(sources[0])
        return f"{answer_text.strip()} [1]", [fallback_source]

    selected = [available_sources[label] for label in cited_labels]
    display_labels = {
        source_label: str(index)
        for index, source_label in enumerate(cited_labels, start=1)
    }

    def replace_citation(match: re.Match[str]) -> str:
        display_label = display_labels.get(match.group(1).upper())
        return f"[{display_label}]" if display_label else ""

    display_answer = citation_pattern.sub(replace_citation, answer_text).strip()
    return display_answer, selected


def _format_source_log(source: dict[str, Any]) -> str:
    location = ""
    if source.get("slide_number") is not None:
        location = f" · Slide {source['slide_number']}"
    elif source.get("page_number") is not None:
        location = f" · Page {source['page_number']}"
    score = source.get("relevance_score")
    score_text = f" · score={score:.4f}" if isinstance(score, (int, float)) else ""
    return f"{source.get('document_name', 'Unknown document')}{location}{score_text}"


def _format_retrieval_candidate(document: Any, score: float) -> str:
    metadata = document.metadata or {}
    location = ""
    if metadata.get("slide_number") is not None:
        location = f" · Slide {metadata['slide_number']}"
    elif metadata.get("page_number") is not None:
        location = f" · Page {metadata['page_number']}"
    document_name = metadata.get("document_name") or metadata.get("source") or "Unknown document"
    return f"{document_name}{location} · score={score:.4f}"


def _document_chunk_key(document: Any) -> str:
    metadata = document.metadata or {}
    chunk_id = str(metadata.get("chunk_id") or "").strip()
    if chunk_id:
        return chunk_id
    return hashlib.sha1(document.page_content.encode("utf-8")).hexdigest()


def _clean_retrieval_query(value: str) -> str:
    cleaned = value.strip()
    if cleaned.startswith("```") and cleaned.endswith("```"):
        cleaned = cleaned[3:-3].strip()
        if cleaned.lower().startswith("text\n"):
            cleaned = cleaned[5:].strip()
    return cleaned.strip("`\"' ")


def _parse_retrieval_queries(value: str) -> list[str]:
    cleaned_block = value.strip()
    if cleaned_block.startswith("```") and cleaned_block.endswith("```"):
        cleaned_block = cleaned_block[3:-3].strip()
        if cleaned_block.lower().startswith("text\n"):
            cleaned_block = cleaned_block[5:].strip()

    queries = []
    for line in cleaned_block.splitlines():
        line = re.sub(r"^\s*(?:[-*]|\d+[.)])\s*", "", line)
        query = _clean_retrieval_query(line)
        if query and query.casefold() not in {item.casefold() for item in queries}:
            queries.append(query)
    return queries[:3]


def _is_comparison_question(value: str) -> bool:
    normalized = f" {_normalize_inline(value).casefold()} "
    indicators = (
        " khác nhau",
        " so sánh",
        " phân biệt",
        "difference between",
        "different from",
        "compare ",
        "comparison",
        " versus ",
        " vs ",
    )
    return any(indicator in normalized for indicator in indicators)


def _normalize_sections(
    text: str | None,
    sections: list[dict[str, Any]] | None,
) -> list[dict[str, Any]]:
    normalized = []
    for index, section in enumerate(sections or [], start=1):
        content = _normalize_lines(str(section.get("content") or ""))
        title = _normalize_inline(str(section.get("title") or "")) or None
        if not content and not title:
            continue

        section_type = str(section.get("section_type") or "document").lower()
        if section_type not in {"document", "page", "slide"}:
            section_type = "document"
        section_number = _optional_int(section.get("section_number"))
        normalized.append(
            {
                "section_index": index,
                "section_type": section_type,
                "section_number": section_number,
                "section_title": title,
                "content": content,
            }
        )

    if not normalized and (text or "").strip():
        normalized.append(
            {
                "section_index": 1,
                "section_type": "document",
                "section_number": None,
                "section_title": None,
                "content": _normalize_lines(text or ""),
            }
        )
    return normalized


def _build_chunk_records(
    document_name: str,
    chapter: str | None,
    sections: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    for section in sections:
        content = section["content"]
        section_chunks = _chunk_text(content) if content else [""]
        for section_chunk_index, chunk in enumerate(section_chunks, start=1):
            header = _section_header(document_name, chapter, section)
            indexed_text = "\n".join(part for part in [header, chunk] if part).strip()
            if not indexed_text:
                continue
            records.append(
                {
                    **section,
                    "section_chunk_index": section_chunk_index,
                    "text": indexed_text,
                }
            )
    return records


def _section_header(
    document_name: str,
    chapter: str | None,
    section: dict[str, Any],
) -> str:
    lines = [f"Document: {document_name}"]
    if chapter:
        lines.append(f"Chapter: {chapter}")

    section_type = section["section_type"]
    section_number = section["section_number"]
    title = section["section_title"]
    label = section_type.capitalize()
    if section_number is not None:
        label += f" {section_number}"
    if title:
        label += f": {title}"
    if section_number is not None or title:
        lines.append(label)
    return "\n".join(lines)


def _chunk_text(
    text: str,
    chunk_size: int = CHUNK_SIZE,
    overlap: int = CHUNK_OVERLAP,
) -> list[str]:
    normalized = _normalize_lines(text)
    if not normalized:
        return []

    chunks: list[str] = []
    start = 0
    while start < len(normalized):
        hard_end = min(start + chunk_size, len(normalized))
        end = hard_end
        if hard_end < len(normalized):
            newline_boundary = normalized.rfind("\n", start + chunk_size // 2, hard_end)
            space_boundary = normalized.rfind(" ", start + chunk_size // 2, hard_end)
            boundary = max(newline_boundary, space_boundary)
            if boundary > start:
                end = boundary

        chunk = normalized[start:end].strip()
        if chunk:
            chunks.append(chunk)
        if end >= len(normalized):
            break
        start = max(end - overlap, start + 1)

    return chunks


def _normalize_lines(value: str) -> str:
    lines = [
        _normalize_inline(line)
        for line in value.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    ]
    return "\n".join(line for line in lines if line)


def _normalize_inline(value: str) -> str:
    return re.sub(r"[ \t\f\v]+", " ", value).strip()
