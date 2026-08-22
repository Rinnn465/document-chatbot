using DocumentChatbot.Data;
using DocumentChatbot.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentChatbot.Web.Services;

public sealed class SqlChatSessionRepository(DocumentChatbotDbContext dbContext)
    : IChatSessionRepository
{
    public async Task AddAsync(
        ChatSession session,
        CancellationToken cancellationToken = default)
    {
        dbContext.ChatSessions.Add(new ChatSessionEntity
        {
            ChatSessionId = session.Id,
            CourseId = session.CourseId,
            UserId = session.UserId,
            Title = session.Title,
            CreatedAtUtc = session.CreatedAtUtc.UtcDateTime
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ChatSession?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await SessionQuery()
            .SingleOrDefaultAsync(session => session.ChatSessionId == id, cancellationToken);

        return entity is null ? null : MapSession(entity);
    }

    public async Task<IReadOnlyList<ChatSession>> GetAllAsync(
        Guid userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        var entities = await SessionQuery()
            .Where(session => session.UserId == userId && session.CourseId == courseId)
            .ToListAsync(cancellationToken);

        return entities
            .Select(MapSession)
            .OrderByDescending(session => session.UpdatedAtUtc)
            .ToArray();
    }

    public async Task SaveAsync(
        ChatSession session,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ChatSessions
            .Include(item => item.Messages)
            .ThenInclude(message => message.Citations)
            .SingleAsync(item => item.ChatSessionId == session.Id, cancellationToken);

        entity.Title = session.Title;
        var existingMessageIds = entity.Messages
            .Select(message => message.ChatMessageId)
            .ToHashSet();

        var pendingMessages = session.Messages
            .Where(message => !existingMessageIds.Contains(message.Id))
            .ToArray();
        var referencedDocumentIds = pendingMessages
            .SelectMany(message => message.Citations)
            .Select(citation => Guid.TryParse(citation.DocumentId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var existingDocumentIdArray = await dbContext.Documents
            .Where(document => referencedDocumentIds.Contains(document.DocumentId))
            .Select(document => document.DocumentId)
            .ToArrayAsync(cancellationToken);
        var existingDocumentIds = existingDocumentIdArray.ToHashSet();

        foreach (var message in pendingMessages)
        {
            var messageEntity = new ChatMessageEntity
            {
                ChatMessageId = message.Id,
                ChatSessionId = session.Id,
                Role = message.Role.ToString(),
                Content = message.Content,
                SentAtUtc = message.SentAtUtc.UtcDateTime,
                Citations = message.Citations
                    .Select(citation => MapCitation(citation, message.Id, existingDocumentIds))
                    .Where(citation => citation is not null)
                    .Cast<CitationEntity>()
                    .ToList()
            };
            dbContext.ChatMessages.Add(messageEntity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ChatSessions
            .SingleOrDefaultAsync(session => session.ChatSessionId == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.ChatSessions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<ChatSessionEntity> SessionQuery() =>
        dbContext.ChatSessions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(session => session.Messages)
            .ThenInclude(message => message.Citations)
            .ThenInclude(citation => citation.Document);

    private static ChatSession MapSession(ChatSessionEntity entity)
    {
        var session = new ChatSession(
            entity.ChatSessionId,
            entity.CourseId,
            entity.UserId,
            entity.Title,
            AsUtc(entity.CreatedAtUtc));

        foreach (var message in OrderMessages(entity.Messages))
        {
            session.Append(new ChatMessage(
                message.ChatMessageId,
                Enum.Parse<MessageRole>(message.Role, ignoreCase: true),
                message.Content,
                AsUtc(message.SentAtUtc),
                message.Citations
                    .OrderBy(citation => citation.CitationId)
                    .Select(MapCitation)
                    .ToArray()));
        }

        return session;
    }

    private static IEnumerable<ChatMessageEntity> OrderMessages(
        IEnumerable<ChatMessageEntity> messages)
    {
        foreach (var timestampGroup in messages
                     .GroupBy(message => message.SentAtUtc)
                     .OrderBy(group => group.Key))
        {
            var userMessages = timestampGroup
                .Where(message => message.Role.Equals(
                    nameof(MessageRole.User),
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(message => message.ChatMessageId)
                .ToArray();
            var assistantMessages = timestampGroup
                .Where(message => message.Role.Equals(
                    nameof(MessageRole.Assistant),
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(message => message.ChatMessageId)
                .ToArray();

            // The original schema stored whole seconds, so a user/assistant
            // pair could share a timestamp. Interleave legacy rows as
            // user -> assistant. New rows retain 100 ns precision.
            for (var index = 0; index < Math.Max(userMessages.Length, assistantMessages.Length); index++)
            {
                if (index < userMessages.Length)
                {
                    yield return userMessages[index];
                }

                if (index < assistantMessages.Length)
                {
                    yield return assistantMessages[index];
                }
            }
        }
    }

    private static Citation MapCitation(CitationEntity entity) => new(
        entity.ChunkId,
        entity.ChunkIndex,
        entity.DocumentId.ToString(),
        entity.Document.Title,
        entity.Document.Chapter,
        entity.PageNumber,
        entity.SlideNumber,
        entity.Excerpt,
        decimal.ToDouble(entity.RelevanceScore));

    private static CitationEntity? MapCitation(
        Citation citation,
        Guid chatMessageId,
        IReadOnlySet<Guid> existingDocumentIds)
    {
        if (!Guid.TryParse(citation.DocumentId, out var documentId) ||
            !existingDocumentIds.Contains(documentId))
        {
            return null;
        }

        return new CitationEntity
        {
            ChatMessageId = chatMessageId,
            DocumentId = documentId,
            ChunkId = citation.ChunkId,
            ChunkIndex = citation.ChunkIndex,
            PageNumber = citation.PageNumber,
            SlideNumber = citation.SlideNumber,
            Excerpt = citation.Excerpt,
            RelevanceScore = Convert.ToDecimal(Math.Clamp(citation.RelevanceScore, 0, 9.99999))
        };
    }

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
