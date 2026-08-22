using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Contracts;

public sealed record DocumentProcessingUpdate(
    Guid DocumentId,
    int CourseId,
    string Title,
    string Status,
    string Stage,
    int Progress,
    string Message,
    int ChunkCount,
    DateTimeOffset OccurredAtUtc)
{
    public static DocumentProcessingUpdate From(
        DocumentSummary document,
        string stage,
        int progress,
        string message) => new(
        document.Id,
        document.CourseId,
        document.Title,
        document.Status.ToString(),
        stage,
        Math.Clamp(progress, 0, 100),
        message,
        document.ChunkCount,
        DateTimeOffset.UtcNow);
}

public sealed record KnowledgeBaseUpdated(
    int CourseId,
    Guid DocumentId,
    string DocumentTitle,
    int ChunkCount,
    DateTimeOffset UpdatedAtUtc);
