using DocumentChatbot.Web.Authorization;
using DocumentChatbot.Web.Contracts;
using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DocumentChatbot.Web.Hubs;

[Authorize(Policy = AppPolicies.StudentOnly)]
public sealed class ChatHub(
    IChatService chatService,
    IHubContext<DocumentHub> documentHub) : Hub
{
    private const int Prn222CourseId = 1;

    public async Task<ChatSessionResponse> CreateSession()
    {
        var cancellationToken = Context.ConnectionAborted;
        var session = await chatService.CreateSessionAsync(
            GetUserId(),
            Prn222CourseId,
            cancellationToken);

        return ChatSessionResponse.From(session);
    }

    public async Task<IReadOnlyList<ChatSessionResponse>> GetSessions()
    {
        var cancellationToken = Context.ConnectionAborted;
        var sessions = await chatService.GetSessionsAsync(
            GetUserId(),
            Prn222CourseId,
            cancellationToken);

        return sessions.Select(ChatSessionResponse.From).ToArray();
    }

    public async Task<ChatSessionResponse> GetSession(Guid sessionId)
    {
        var cancellationToken = Context.ConnectionAborted;
        var session = await chatService.GetSessionAsync(
            GetUserId(),
            sessionId,
            cancellationToken);

        return ChatSessionResponse.From(session);
    }

    public async Task<ChatSessionResponse> RenameSession(Guid sessionId, string title)
    {
        var session = await chatService.RenameSessionAsync(
            GetUserId(),
            sessionId,
            title,
            Context.ConnectionAborted);

        return ChatSessionResponse.From(session);
    }

    public Task DeleteSession(Guid sessionId) =>
        chatService.DeleteSessionAsync(
            GetUserId(),
            sessionId,
            Context.ConnectionAborted);

    public async Task Ask(Guid sessionId, string question)
    {
        var cancellationToken = Context.ConnectionAborted;
        await SendStatusAsync("retrieving", "Đang tìm các đoạn tài liệu liên quan...", cancellationToken);

        try
        {
            var result = await chatService.AskAsync(
                GetUserId(),
                sessionId,
                question,
                cancellationToken);

            await Clients.Caller.SendAsync(
                "AnswerReceived",
                AskQuestionResponse.From(result),
                cancellationToken);
            await documentHub.Clients
                .Group(DocumentHub.CourseGroup(Prn222CourseId))
                .SendAsync(
                    "ChatUsageUpdated",
                    new ChatUsageUpdated(Prn222CourseId, DateTimeOffset.UtcNow),
                    cancellationToken);
            await SendStatusAsync("ready", string.Empty, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            await SendErrorAsync(exception.Message, cancellationToken);
        }
        catch (ChatSessionNotFoundException)
        {
            await SendErrorAsync("Phiên hội thoại không còn tồn tại. Hãy tải lại trang.", cancellationToken);
        }
        catch (ChatSessionAccessDeniedException)
        {
            await SendErrorAsync("Bạn không có quyền sử dụng phiên hội thoại này.", cancellationToken);
        }
        catch (HttpRequestException)
        {
            await SendErrorAsync("Dịch vụ RAG chưa sẵn sàng. Hãy kiểm tra Python service.", cancellationToken);
        }
        catch (TaskCanceledException)
        {
            await SendErrorAsync("Dịch vụ RAG phản hồi quá thời gian.", CancellationToken.None);
        }
    }

    private Guid GetUserId() =>
        Guid.TryParse(Context.UserIdentifier, out var userId)
            ? userId
            : throw new HubException("Không xác định được tài khoản Student.");

    private Task SendStatusAsync(string state, string message, CancellationToken cancellationToken) =>
        Clients.Caller.SendAsync(
            "ChatStatusChanged",
            new ChatStatusResponse(state, message),
            cancellationToken);

    private async Task SendErrorAsync(string message, CancellationToken cancellationToken)
    {
        await Clients.Caller.SendAsync("ChatError", new ChatErrorResponse(message), cancellationToken);
        await SendStatusAsync("ready", string.Empty, cancellationToken);
    }
}
