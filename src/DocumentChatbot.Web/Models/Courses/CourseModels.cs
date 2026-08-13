namespace DocumentChatbot.Web.Models;

public sealed record CourseSummary(
    int CourseId,
    string Code,
    string Name,
    int DocumentCount);

public sealed record CourseDocumentsViewModel(
    CourseSummary Course,
    IReadOnlyList<DocumentSummary> Documents);
