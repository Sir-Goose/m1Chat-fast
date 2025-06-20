using m1Chat.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; // Add this using directive

namespace m1Chat.Middleware;

public class UserCreationOrSyncMiddleware
{
    private readonly RequestDelegate _next;

    public UserCreationOrSyncMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ChatDbContext db)
    {
        // Skip user creation for authentication endpoints
        if (context.Request.Path.StartsWithSegments("/signin-google") || 
            context.Request.Path.StartsWithSegments("/auth"))
        {
            await _next(context);
            return;
        }
        // Check if the user is authenticated and has an email claim
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var email = context.User.FindFirst(ClaimTypes.Email)?.Value;

            if (!string.IsNullOrEmpty(email))
            {
                // Ensure user exists in our database
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    db.Users.Add(new m1Chat.Data.User { Email = email });
                    await db.SaveChangesAsync();
                }
            }
        }
        await _next(context);
    }
}