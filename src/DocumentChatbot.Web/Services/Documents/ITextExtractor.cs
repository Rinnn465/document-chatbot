using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public interface ITextExtractor
{
    ExtractedDocument Extract(Stream fileStream, DocumentType fileType);
}
