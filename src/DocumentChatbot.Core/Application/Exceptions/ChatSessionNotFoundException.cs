namespace DocumentChatbot.Core.Application.Exceptions;

public sealed class ChatSessionNotFoundException(Guid sessionId)
    : Exception($"Chat session '{sessionId}' was not found.");
