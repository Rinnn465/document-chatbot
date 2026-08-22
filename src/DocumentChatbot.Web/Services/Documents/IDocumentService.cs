using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public interface IDocumentService
{
    Task<DocumentSummary> UploadAsync(
        int courseId,
        string title,
        string? chapter,
        string originalFileName,
        DocumentType fileType,
        Stream fileStream,
        long fileSizeBytes,
        Guid uploadedById,
        string uploadedByName,
        CancellationToken cancellationToken = default);

    Task<DocumentSummary> QueueUploadAsync(
        int courseId,
        string title,
        string? chapter,
        string originalFileName,
        DocumentType fileType,
        Stream fileStream,
        long fileSizeBytes,
        Guid uploadedById,
        string uploadedByName,
        CancellationToken cancellationToken = default);

    Task ProcessQueuedUploadAsync(
        DocumentProcessingJob job,
        CancellationToken cancellationToken = default);

    Task<DocumentSummary> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentSummary>> GetAllAsync(
        int courseId,
        CancellationToken cancellationToken = default);

    Task<DocumentChunkPage> GetChunksAsync(
        Guid id,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
