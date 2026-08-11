using DocumentChatbot.Core.Domain;

namespace DocumentChatbot.Core.Application.Abstractions;

public interface IDocumentService
{
    Task<DocumentSummary> UploadAsync(
        string title,
        string? chapter,
        string originalFileName,
        DocumentType fileType,
        Stream fileStream,
        long fileSizeBytes,
        string uploadedById,
        string uploadedByName,
        CancellationToken cancellationToken = default);

    Task<DocumentSummary> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
