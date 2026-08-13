namespace DocumentChatbot.Web.Exceptions;

public sealed class ChatSessionNotFoundException(Guid sessionId)
    : Exception($"Chat session '{sessionId}' was not found.");
