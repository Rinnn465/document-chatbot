using DocumentChatbot.Web.Contracts;
using DocumentChatbot.Web.Hubs;
using DocumentChatbot.Web.Models;
using Microsoft.AspNetCore.SignalR;

namespace DocumentChatbot.Web.Services;

public interface IDocumentStatusNotifier
{
    Task NotifyAsync(
        DocumentSummary document,
        string stage,
        int progress,
        string message,
        CancellationToken cancellationToken = default);
}

public sealed class SignalRDocumentStatusNotifier(
    IHubContext<DocumentHub> hubContext) : IDocumentStatusNotifier
{
    public async Task NotifyAsync(
        DocumentSummary document,
        string stage,
        int progress,
        string message,
        CancellationToken cancellationToken = default)
    {
        var clients = hubContext.Clients.Group(DocumentHub.CourseGroup(document.CourseId));
        await clients.SendAsync(
            "DocumentProcessingChanged",
            DocumentProcessingUpdate.From(document, stage, progress, message),
            cancellationToken);

        if (document.Status == DocumentStatus.Indexed)
        {
            await clients.SendAsync(
                "KnowledgeBaseUpdated",
                new KnowledgeBaseUpdated(
                    document.CourseId,
                    document.Id,
                    document.Title,
                    document.ChunkCount,
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }
    }
}
