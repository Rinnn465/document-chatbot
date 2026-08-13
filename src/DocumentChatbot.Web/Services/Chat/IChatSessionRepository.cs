using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public interface IChatSessionRepository
{
    Task AddAsync(ChatSession session, CancellationToken cancellationToken = default);
    Task<ChatSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatSession>> GetAllAsync(
        Guid userId,
        int courseId,
        CancellationToken cancellationToken = default);
    Task SaveAsync(ChatSession session, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
