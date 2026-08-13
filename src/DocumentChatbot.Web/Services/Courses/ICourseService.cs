using DocumentChatbot.Web.Models;

namespace DocumentChatbot.Web.Services;

public interface ICourseService
{
    Task<IReadOnlyList<CourseSummary>> GetManagedCoursesAsync(
        Guid subjectLeaderId,
        CancellationToken cancellationToken = default);

    Task<CourseSummary?> GetManagedCourseAsync(
        Guid subjectLeaderId,
        int courseId,
        CancellationToken cancellationToken = default);
}
