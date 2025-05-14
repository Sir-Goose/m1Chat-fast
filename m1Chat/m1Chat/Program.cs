using System;
using System.Net.Http;
using m1Chat.Authentication;
using m1Chat.Client.Services;
using MudBlazor.Services;
using m1Chat.Components;
using m1Chat.Services;
using m1Chat.Data;
using m1Chat.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// --- Services Registration --- // 

// 1) Make HttpContext available in DI
builder.Services.AddHttpContextAccessor();

// 2) Your other scoped services
builder.Services.AddScoped<UserService>();
builder.Services.AddMudServices();
builder.Services.AddScoped<ChatCompletionService>();

// 3) Our dynamic‐BaseAddress HttpClient
builder.Services.AddScoped<HttpClient>(sp =>
{
    var accessor    = sp.GetRequiredService<IHttpContextAccessor>();
    var httpContext = accessor.HttpContext;
    if (httpContext?.Request != null)
    {
        var req      = httpContext.Request;
        var scheme   = req.Scheme;           // "http" or "https"
        var host     = req.Host.Value;       // "localhost:5000" etc.
        var basePath = req.PathBase.Value;   // e.g. "/myapp" or ""
        // ensure trailing slash
        var uri = $"{scheme}://{host}{basePath}/";
        return new HttpClient { BaseAddress = new Uri(uri) };
    }

    // fallback (unlikely to be used):
    return new HttpClient();
});

// 4) EF Core
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite(builder.Configuration
        .GetConnectionString("DefaultConnection")));

// 5) Your OpenRouter completion service
builder.Services.AddScoped<Completion>();

// 6) ChatService (uses the HttpClient we just registered)
builder.Services.AddScoped<ChatService>();

// 7) MVC controllers
builder.Services.AddControllers();

// --- Conditional Authentication Registration --- //
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = DevAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme    = DevAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
        DevAuthenticationHandler.SchemeName, _ => { });
}
else
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CloudflareAccessAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme    = CloudflareAccessAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, CloudflareAccessAuthenticationHandler>(
        CloudflareAccessAuthenticationHandler.SchemeName, _ => { });
}

builder.Services.AddAuthorization();

// 8) Blazor‐WASM interactive components
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// --- Hybrid Database Initialization ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch
    {
        db.Database.EnsureCreated();
    }
}

// --- Middleware Pipeline ---

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseMiddleware<CloudflareUserSyncMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.MapRazorComponents<App>()
   .AddInteractiveWebAssemblyRenderMode()
   .AddAdditionalAssemblies(typeof(m1Chat.Client._Imports).Assembly);

app.Run();
