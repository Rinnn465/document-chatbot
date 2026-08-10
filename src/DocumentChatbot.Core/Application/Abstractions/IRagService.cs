using DocumentChatbot.Core.Application.Models;

namespace DocumentChatbot.Core.Application.Abstractions;

public interface IRagService
{
    Task<RagAnswer> AnswerAsync(
        RagQuestion question,
        CancellationToken cancellationToken = default);
}
