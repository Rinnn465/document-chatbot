import hashlib
import json
import os
import threading
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


KNOWLEDGE_SCHEMA_VERSION = 1


class KnowledgeSnapshotStore:
    """Stores normalized, rebuildable document snapshots outside the vector DB."""

    def __init__(self, root_directory: str | Path) -> None:
        self.root_directory = Path(root_directory)
        self.documents_directory = self.root_directory / "documents"
        self.manifest_path = self.root_directory / "manifest.json"
        self._lock = threading.RLock()

    def save_document(
        self,
        *,
        document_id: str,
        document_name: str,
        chapter: str | None,
        sections: list[dict[str, Any]],
        chunks: list[dict[str, Any]],
        index_signature: dict[str, Any],
    ) -> dict[str, Any]:
        now = datetime.now(timezone.utc).isoformat()
        content_hash = _content_hash(document_name, chapter, sections)
        snapshot_file = self._snapshot_file_name(document_id)
        snapshot = {
            "schemaVersion": KNOWLEDGE_SCHEMA_VERSION,
            "indexedAtUtc": now,
            "contentHash": content_hash,
            "indexSignature": index_signature,
            "document": {
                "documentId": document_id,
                "documentName": document_name,
                "chapter": chapter,
            },
            "sectionCount": len(sections),
            "chunkCount": len(chunks),
            "sections": [_serialize_section(section) for section in sections],
            "chunks": chunks,
        }

        manifest_entry = {
            "documentId": document_id,
            "documentName": document_name,
            "chapter": chapter,
            "contentHash": content_hash,
            "sectionCount": len(sections),
            "chunkCount": len(chunks),
            "indexedAtUtc": now,
            "snapshotFile": f"documents/{snapshot_file}",
        }

        with self._lock:
            self.documents_directory.mkdir(parents=True, exist_ok=True)
            _write_json_atomic(self.documents_directory / snapshot_file, snapshot)

            manifest = self._read_manifest()
            entries = {
                str(entry.get("documentId")): entry
                for entry in manifest.get("documents", [])
                if entry.get("documentId")
            }
            entries[document_id] = manifest_entry
            manifest["schemaVersion"] = KNOWLEDGE_SCHEMA_VERSION
            manifest["updatedAtUtc"] = now
            manifest["documents"] = sorted(
                entries.values(),
                key=lambda entry: str(entry.get("documentName", "")).casefold(),
            )
            _write_json_atomic(self.manifest_path, manifest)

        return manifest_entry

    def delete_document(self, document_id: str) -> None:
        with self._lock:
            snapshot_path = self.documents_directory / self._snapshot_file_name(document_id)
            snapshot_path.unlink(missing_ok=True)

            manifest = self._read_manifest()
            remaining = [
                entry
                for entry in manifest.get("documents", [])
                if str(entry.get("documentId")) != document_id
            ]
            if len(remaining) == len(manifest.get("documents", [])):
                return

            manifest["schemaVersion"] = KNOWLEDGE_SCHEMA_VERSION
            manifest["updatedAtUtc"] = datetime.now(timezone.utc).isoformat()
            manifest["documents"] = remaining
            _write_json_atomic(self.manifest_path, manifest)

    def iter_snapshots(self) -> Iterable[dict[str, Any]]:
        manifest = self._read_manifest()
        for entry in manifest.get("documents", []):
            relative_path = str(entry.get("snapshotFile") or "")
            if not relative_path:
                continue
            path = self.root_directory / relative_path
            if path.is_file():
                with path.open("r", encoding="utf-8") as stream:
                    yield json.load(stream)

    def _read_manifest(self) -> dict[str, Any]:
        if not self.manifest_path.is_file():
            return {
                "schemaVersion": KNOWLEDGE_SCHEMA_VERSION,
                "updatedAtUtc": None,
                "documents": [],
            }
        with self.manifest_path.open("r", encoding="utf-8") as stream:
            value = json.load(stream)
        if not isinstance(value, dict) or not isinstance(value.get("documents"), list):
            raise ValueError(f"Invalid knowledge manifest: {self.manifest_path}")
        return value

    @staticmethod
    def _snapshot_file_name(document_id: str) -> str:
        # The hash prevents path traversal and keeps filenames valid on Windows.
        digest = hashlib.sha256(document_id.encode("utf-8")).hexdigest()
        return f"{digest}.json"


def _serialize_section(section: dict[str, Any]) -> dict[str, Any]:
    return {
        "sectionIndex": section["section_index"],
        "sectionType": section["section_type"],
        "sectionNumber": section["section_number"],
        "title": section["section_title"],
        "content": section["content"],
    }


def _content_hash(
    document_name: str,
    chapter: str | None,
    sections: list[dict[str, Any]],
) -> str:
    canonical = {
        "documentName": document_name,
        "chapter": chapter,
        "sections": [_serialize_section(section) for section in sections],
    }
    encoded = json.dumps(
        canonical,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _write_json_atomic(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path = path.with_name(f".{path.name}.{os.getpid()}.tmp")
    try:
        with temporary_path.open("w", encoding="utf-8", newline="\n") as stream:
            json.dump(value, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_path, path)
    finally:
        temporary_path.unlink(missing_ok=True)
