namespace DocumentChatbot.Core.Domain;

public sealed class ChatMessage
{
    public ChatMessage(
        Guid id,
        MessageRole role,
        string content,
        DateTimeOffset sentAtUtc,
        IReadOnlyCollection<Citation>? citations = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Message id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content is required.", nameof(content));
        }

        Id = id;
        Role = role;
        Content = content.Trim();
        SentAtUtc = sentAtUtc;
        Citations = citations?.ToArray() ?? [];
    }

    public Guid Id { get; }
    public MessageRole Role { get; }
    public string Content { get; }
    public DateTimeOffset SentAtUtc { get; }
    public IReadOnlyList<Citation> Citations { get; }
}
