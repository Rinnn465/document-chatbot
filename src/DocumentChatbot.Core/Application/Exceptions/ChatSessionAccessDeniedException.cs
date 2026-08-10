namespace DocumentChatbot.Core.Application.Exceptions;

public sealed class ChatSessionAccessDeniedException(Guid sessionId)
    : Exception($"The current user cannot access chat session '{sessionId}'.");
