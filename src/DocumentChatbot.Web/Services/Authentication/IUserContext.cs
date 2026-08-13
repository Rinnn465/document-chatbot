namespace DocumentChatbot.Web.Services;

public interface IUserContext
{
    Guid UserId { get; }
    string DisplayName { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
}
