namespace DocumentChatbot.Infrastructure.Rag;

public sealed class RagServiceOptions
{
    public const string SectionName = "RagService";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string AskPath { get; set; } = "/ask";
    public int TimeoutSeconds { get; set; } = 60;
}
