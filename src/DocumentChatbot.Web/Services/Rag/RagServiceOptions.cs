namespace DocumentChatbot.Web.Services;

public sealed class RagServiceOptions
{
    public const string SectionName = "RagService";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string AskPath { get; set; } = "/ask";
    public string ServiceToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 60;
}
