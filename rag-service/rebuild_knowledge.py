from rag import RAGPipeline


def main() -> None:
    pipeline = RAGPipeline()
    snapshots = list(pipeline.snapshot_store.iter_snapshots())
    if not snapshots:
        print("No knowledge snapshots were found.")
        return

    total_chunks = 0
    for snapshot in snapshots:
        document = snapshot["document"]
        sections = [
            {
                "section_type": section["sectionType"],
                "section_number": section.get("sectionNumber"),
                "title": section.get("title"),
                "content": section.get("content", ""),
            }
            for section in snapshot.get("sections", [])
        ]
        chunk_count = pipeline.ingest_document(
            document_id=document["documentId"],
            document_name=document["documentName"],
            chapter=document.get("chapter"),
            sections=sections,
        )
        total_chunks += chunk_count
        print(f"Rebuilt {document['documentName']}: {chunk_count} chunks")

    print(f"Knowledge rebuild completed: {len(snapshots)} documents, {total_chunks} chunks")


if __name__ == "__main__":
    main()
