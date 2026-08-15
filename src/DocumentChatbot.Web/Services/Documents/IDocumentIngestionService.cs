using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

/// <summary>
/// Sends structured document sections to the RAG service so they can be
/// chunked without losing their original page or slide boundaries.
/// </summary>
public interface IDocumentIngestionService
{
    Task<DocumentIngestResult> IngestAsync(
        string documentId,
        string documentName,
        string? chapter,
        IReadOnlyList<ExtractedDocumentSection> sections,
        CancellationToken cancellationToken = default);

    Task<DocumentChunkPage> GetChunksAsync(
        string documentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string documentId, CancellationToken cancellationToken = default);
}
