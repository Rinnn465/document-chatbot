using DocumentChatbot.Web.Authorization;
using DocumentChatbot.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentChatbot.Web.Controllers;

[Authorize(Policy = AppPolicies.SubjectLeaderOnly)]
public sealed class CoursesController(
    ICourseService courseService,
    IUserContext userContext) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var courses = await courseService.GetManagedCoursesAsync(
            userContext.UserId,
            cancellationToken);
        return View(courses);
    }
}
