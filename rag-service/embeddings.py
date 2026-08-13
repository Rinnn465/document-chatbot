import os
from typing import List

from sentence_transformers import SentenceTransformer


class SentenceTransformerEmbeddings:
    """LangChain-compatible embedding adapter reused from the previous RAG project."""

    def __init__(self) -> None:
        self.model_name = os.getenv(
            "EMBEDDING_MODEL",
            "intfloat/multilingual-e5-small",
        )
        self.batch_size = int(os.getenv("EMBEDDING_BATCH_SIZE", "8"))
        self._uses_query_instruction = "qwen3-embedding" in self.model_name.lower()
        self._uses_e5_prefixes = "e5" in self.model_name.lower()

        self.model = SentenceTransformer(
            self.model_name,
            device=os.getenv("EMBEDDING_DEVICE", "cpu"),
            trust_remote_code=True,
            cache_folder=os.getenv("SENTENCE_TRANSFORMERS_HOME") or None,
        )

    def embed_documents(self, texts: List[str]) -> List[List[float]]:
        inputs = [f"passage: {text}" for text in texts] if self._uses_e5_prefixes else texts
        embeddings = self.model.encode(
            inputs,
            batch_size=self.batch_size,
            normalize_embeddings=True,
            show_progress_bar=False,
        )
        return embeddings.tolist()

    def embed_query(self, text: str) -> List[float]:
        query = text
        if self._uses_query_instruction:
            query = (
                "Instruct: Retrieve passages from PRN222 course documents that answer "
                f"the student's question.\nQuery: {text}"
            )
        elif self._uses_e5_prefixes:
            query = f"query: {text}"

        embedding = self.model.encode(query, normalize_embeddings=True)
        return embedding.tolist()
