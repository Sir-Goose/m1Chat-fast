using m1Chat.Data;
using Microsoft.EntityFrameworkCore;

namespace m1Chat.Middleware;

public class CloudflareUserSyncMiddleware
{
    private readonly RequestDelegate _next;

    public CloudflareUserSyncMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ChatDbContext db)
    {
        var email = context.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (!string.IsNullOrEmpty(email))
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                db.Users.Add(new m1Chat.Data.User { Email = email });
                await db.SaveChangesAsync();
            }
        }
        await _next(context);
    }
}