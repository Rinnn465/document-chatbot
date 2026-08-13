using DocumentChatbot.Data;
using DocumentChatbot.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentChatbot.Web.Services;

public sealed class CourseService(DocumentChatbotDbContext dbContext) : ICourseService
{
    public async Task<IReadOnlyList<CourseSummary>> GetManagedCoursesAsync(
        Guid subjectLeaderId,
        CancellationToken cancellationToken = default) =>
        await ManagedCourseEntities(subjectLeaderId)
            .OrderBy(course => course.Code)
            .Select(course => new CourseSummary(
                course.CourseId,
                course.Code,
                course.Name,
                dbContext.Documents.Count(document => document.CourseId == course.CourseId)))
            .ToArrayAsync(cancellationToken);

    public Task<CourseSummary?> GetManagedCourseAsync(
        Guid subjectLeaderId,
        int courseId,
        CancellationToken cancellationToken = default) =>
        ManagedCourseEntities(subjectLeaderId)
            .Where(course => course.CourseId == courseId)
            .Select(course => new CourseSummary(
                course.CourseId,
                course.Code,
                course.Name,
                dbContext.Documents.Count(document => document.CourseId == course.CourseId)))
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<CourseEntity> ManagedCourseEntities(Guid subjectLeaderId) =>
        dbContext.Courses
            .AsNoTracking()
            .Where(course => course.IsActive && course.SubjectLeaderId == subjectLeaderId);
}
