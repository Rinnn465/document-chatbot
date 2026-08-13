using DocumentChatbot.Data;
using DocumentChatbot.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DocumentChatbot.Web.Services;

public sealed class UserAccountService(
    DocumentChatbotDbContext dbContext,
    IPasswordHasher<UserEntity> passwordHasher) : IUserAccountService
{
    public async Task<UserAccount?> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(item => item.Role)
            .SingleOrDefaultAsync(
                item => item.Email.ToLower() == normalizedEmail && item.IsActive,
                cancellationToken);

        if (user?.PasswordHash is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return new UserAccount(
            user.UserId,
            user.Email,
            user.DisplayName,
            user.Role.Name);
    }
}
