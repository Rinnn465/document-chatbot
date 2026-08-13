using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DocumentChatbot.Web.Authorization;

namespace DocumentChatbot.Web.Controllers;

[Authorize(Policy = AppPolicies.StudentOnly)]
public sealed class ChatPageController : Controller
{
    [HttpGet("/Chat")]
    public IActionResult Index() => View();
}
