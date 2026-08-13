using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public interface IUserAccountService
{
    Task<UserAccount?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}
