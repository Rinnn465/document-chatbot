using DocumentChatbot.Core.Application.Abstractions;
using DocumentChatbot.Core.Application.Exceptions;
using DocumentChatbot.Web.Contracts;
using DocumentChatbot.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentChatbot.Web.Controllers;

[ApiController]
[Route("chat")]
public sealed class ChatController(
    IChatService chatService,
    IUserContext userContext) : ControllerBase
{
    [HttpPost("sessions")]
    [ProducesResponseType<ChatSessionResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ChatSessionResponse>> CreateSession(
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await chatService.CreateSessionAsync(
                userContext.UserId,
                cancellationToken);
            var response = ChatSessionResponse.From(session);

            return CreatedAtAction(
                nameof(GetSession),
                new { sessionId = session.Id },
                response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ToProblem(exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType<ChatSessionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatSessionResponse>> GetSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await chatService.GetSessionAsync(
                userContext.UserId,
                sessionId,
                cancellationToken);
            return Ok(ChatSessionResponse.From(session));
        }
        catch (ChatSessionNotFoundException exception)
        {
            return NotFound(ToProblem(exception.Message, StatusCodes.Status404NotFound));
        }
        catch (ChatSessionAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPost("sessions/{sessionId:guid}/messages")]
    [ProducesResponseType<AskQuestionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AskQuestionResponse>> Ask(
        Guid sessionId,
        AskQuestionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await chatService.AskAsync(
                userContext.UserId,
                sessionId,
                request.Question,
                cancellationToken);
            return Ok(AskQuestionResponse.From(result));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ToProblem(exception.Message, StatusCodes.Status400BadRequest));
        }
        catch (ChatSessionNotFoundException exception)
        {
            return NotFound(ToProblem(exception.Message, StatusCodes.Status404NotFound));
        }
        catch (ChatSessionAccessDeniedException)
        {
            return Forbid();
        }
    }

    private static ProblemDetails ToProblem(string detail, int status) => new()
    {
        Title = status == StatusCodes.Status404NotFound
            ? "Không tìm thấy phiên hội thoại"
            : "Dữ liệu không hợp lệ",
        Detail = detail,
        Status = status
    };
}
