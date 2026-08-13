using DocumentChatbot.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DocumentChatbot.Web.Pages.Assignment2.Chat;

[Authorize(Policy = AppPolicies.StudentOnly)]
public sealed class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
