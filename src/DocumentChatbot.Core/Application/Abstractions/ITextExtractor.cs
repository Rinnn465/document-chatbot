using DocumentChatbot.Core.Domain;

namespace DocumentChatbot.Core.Application.Abstractions;

public interface ITextExtractor
{
    string Extract(Stream fileStream, DocumentType fileType);
}
