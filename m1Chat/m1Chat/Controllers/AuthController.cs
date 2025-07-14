using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace m1Chat.Controllers;

[Route("auth")]
public class AuthController : ControllerBase
{
    [HttpGet("google-login")]
    [AllowAnonymous]
    public IActionResult GoogleLogin(string? returnUrl = "https://chat.mattdev.im/chat")
    {
        var props = new AuthenticationProperties { 
            RedirectUri = returnUrl,
            IsPersistent = true  // Ensure persistent cookie
        };
        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }


    [HttpGet("check")]
    [AllowAnonymous]
    public IActionResult CheckAuthentication()
    {
        return Ok(new { 
            isAuthenticated = User.Identity?.IsAuthenticated ?? false 
        });
    }

    [HttpGet("/signin-google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback()
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        
        if (!result.Succeeded)
        {
            return Redirect("/?error=auth_failed");
        }

        // Sign in the user with cookie authentication
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, result.Principal);
        
        // Redirect to chat page
        return Redirect("/chat");
    }
}