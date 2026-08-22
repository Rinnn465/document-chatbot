using System.Threading.Channels;
using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public sealed record DocumentProcessingJob(
    Guid DocumentId,
    DocumentType FileType,
    byte[] FileContent);

public interface IDocumentProcessingQueue
{
    ValueTask QueueAsync(DocumentProcessingJob job, CancellationToken cancellationToken = default);
    ValueTask<DocumentProcessingJob> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class DocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<DocumentProcessingJob> _queue =
        Channel.CreateUnbounded<DocumentProcessingJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask QueueAsync(
        DocumentProcessingJob job,
        CancellationToken cancellationToken = default) =>
        _queue.Writer.WriteAsync(job, cancellationToken);

    public ValueTask<DocumentProcessingJob> DequeueAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAsync(cancellationToken);
}
