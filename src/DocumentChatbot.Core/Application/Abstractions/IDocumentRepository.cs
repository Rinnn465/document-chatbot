using DocumentChatbot.Core.Domain;

namespace DocumentChatbot.Core.Application.Abstractions;

public interface IDocumentRepository
{
    Task AddAsync(DocumentSummary document, CancellationToken cancellationToken = default);

    Task<DocumentSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DocumentSummary document, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
