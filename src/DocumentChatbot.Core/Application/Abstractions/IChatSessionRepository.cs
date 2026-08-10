using DocumentChatbot.Core.Domain;

namespace DocumentChatbot.Core.Application.Abstractions;

public interface IChatSessionRepository
{
    Task AddAsync(ChatSession session, CancellationToken cancellationToken = default);
    Task<ChatSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(ChatSession session, CancellationToken cancellationToken = default);
}
