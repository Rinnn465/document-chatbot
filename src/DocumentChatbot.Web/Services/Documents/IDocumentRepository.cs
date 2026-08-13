using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public interface IDocumentRepository
{
    Task AddAsync(DocumentSummary document, CancellationToken cancellationToken = default);

    Task<DocumentSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentSummary>> GetAllAsync(
        int courseId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(DocumentSummary document, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
