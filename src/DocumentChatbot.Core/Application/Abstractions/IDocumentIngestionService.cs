using DocumentChatbot.Core.Application.Models;

namespace DocumentChatbot.Core.Application.Abstractions;

/// <summary>
/// Sends extracted document text to the RAG service so it can be chunked,
/// embedded and stored in the vector database.
/// </summary>
public interface IDocumentIngestionService
{
    Task<DocumentIngestResult> IngestAsync(
        string documentId,
        string documentName,
        string? chapter,
        string text,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string documentId, CancellationToken cancellationToken = default);
}
