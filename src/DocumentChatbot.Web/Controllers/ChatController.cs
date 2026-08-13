using DocumentChatbot.Web.Services;
using DocumentChatbot.Web.Exceptions;
using DocumentChatbot.Web.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DocumentChatbot.Web.Authorization;

namespace DocumentChatbot.Web.Controllers;

[ApiController]
[Route("chat")]
[Authorize(Policy = AppPolicies.StudentOnly)]
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
                1,
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
        catch (HttpRequestException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ToProblem($"Dịch vụ RAG không khả dụng: {exception.Message}", StatusCodes.Status503ServiceUnavailable));
        }
        catch (TaskCanceledException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ToProblem("Dịch vụ RAG phản hồi quá thời gian.", StatusCodes.Status503ServiceUnavailable));
        }
    }

    [HttpPatch("sessions/{sessionId:guid}")]
    [ProducesResponseType<ChatSessionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatSessionResponse>> RenameSession(
        Guid sessionId,
        RenameChatSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await chatService.RenameSessionAsync(
                userContext.UserId,
                sessionId,
                request.Title,
                cancellationToken);
            return Ok(ChatSessionResponse.From(session));
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

    [HttpDelete("sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await chatService.DeleteSessionAsync(
                userContext.UserId,
                sessionId,
                cancellationToken);
            return NoContent();
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
        Title = status switch
        {
            StatusCodes.Status404NotFound => "Không tìm thấy phiên hội thoại",
            StatusCodes.Status503ServiceUnavailable => "Dịch vụ RAG không khả dụng",
            _ => "Dữ liệu không hợp lệ"
        },
        Detail = detail,
        Status = status
    };
}
