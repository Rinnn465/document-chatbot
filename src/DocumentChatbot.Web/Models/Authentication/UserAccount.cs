namespace DocumentChatbot.Web.Models;

public sealed record UserAccount(
    Guid UserId,
    string Email,
    string DisplayName,
    string RoleName);
