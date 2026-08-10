namespace DocumentChatbot.Core.Domain;

public sealed class ChatSession
{
    private readonly List<ChatMessage> _messages = [];
    private readonly object _messageLock = new();

    public ChatSession(
        Guid id,
        string userId,
        string title,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Session id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        Id = id;
        UserId = userId.Trim();
        Title = title.Trim();
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }
    public string UserId { get; }
    public string Title { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<ChatMessage> Messages
    {
        get
        {
            lock (_messageLock)
            {
                return _messages.ToArray();
            }
        }
    }

    public void Append(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_messageLock)
        {
            _messages.Add(message);
            UpdatedAtUtc = message.SentAtUtc;
        }
    }
}
