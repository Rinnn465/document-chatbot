namespace DocumentChatbot.Web.Exceptions;

public sealed class ChatSessionAccessDeniedException(Guid sessionId)
    : Exception($"The current user cannot access chat session '{sessionId}'.");
