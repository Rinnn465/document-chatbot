using DocumentChatbot.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentChatbot.Web.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole(AppRoles.Student))
        {
            return RedirectToAction("Index", "ChatPage");
        }

        if (User.IsInRole(AppRoles.SubjectLeader))
        {
            return RedirectToAction("Index", "Courses");
        }

        return RedirectToAction("AccessDenied", "Account");
    }
}
