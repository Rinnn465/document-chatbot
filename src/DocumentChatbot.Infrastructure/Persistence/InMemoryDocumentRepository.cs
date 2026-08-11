using System.Collections.Concurrent;
using DocumentChatbot.Core.Application.Abstractions;
using DocumentChatbot.Core.Domain;

namespace DocumentChatbot.Infrastructure.Persistence;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, DocumentSummary> _documents = new();

    public Task AddAsync(DocumentSummary document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_documents.TryAdd(document.Id, document))
        {
            throw new InvalidOperationException($"Document '{document.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<DocumentSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _documents.TryGetValue(id, out var document);
        return Task.FromResult(document);
    }

    public Task<IReadOnlyList<DocumentSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DocumentSummary> documents = _documents.Values
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToArray();
        return Task.FromResult(documents);
    }

    public Task SaveAsync(DocumentSummary document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _documents.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
