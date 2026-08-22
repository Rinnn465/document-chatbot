using System.Security.Claims;
using DocumentChatbot.Web.Authorization;
using DocumentChatbot.Web.Services;
using DocumentChatbot.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentChatbot.Web.Controllers;

public sealed class AccountController(IUserAccountService userAccountService) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleHome(User);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await userAccountService.ValidateCredentialsAsync(
            model.Email,
            model.Password,
            cancellationToken);

        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.UserId.ToString()),
            new(ClaimTypes.Name, account.DisplayName),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, account.RoleName)
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true
            });

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return account.RoleName switch
        {
            AppRoles.Student => RedirectToPage("/Assignment2/Chat/Index"),
            AppRoles.SubjectLeader => RedirectToAction("Index", "Courses"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToRoleHome(ClaimsPrincipal user)
    {
        if (user.IsInRole(AppRoles.Student))
        {
            return RedirectToPage("/Assignment2/Chat/Index");
        }

        if (user.IsInRole(AppRoles.SubjectLeader))
        {
            return RedirectToAction("Index", "Courses");
        }

        return RedirectToAction("Index", "Home");
    }
}
