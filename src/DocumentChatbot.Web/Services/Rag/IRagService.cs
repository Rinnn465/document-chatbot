using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public interface IRagService
{
    Task<RagAnswer> AnswerAsync(
        RagQuestion question,
        CancellationToken cancellationToken = default);
}
