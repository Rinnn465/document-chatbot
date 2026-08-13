using System.Security.Claims;

namespace DocumentChatbot.Web.Services;

public sealed class HttpUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("HTTP user context is unavailable.");

    public Guid UserId => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier),
        out var userId)
            ? userId
            : throw new InvalidOperationException("Authenticated user id is unavailable.");

    public string DisplayName => User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
}
