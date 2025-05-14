using m1Chat.Authentication;
using m1Chat.Client.Services;
using MudBlazor.Services;
using m1Chat.Components;
using m1Chat.Services;
using m1Chat.Data;
using m1Chat.Middleware;
using m1Chat.Authentication; 
using m1Chat.Middleware;    
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// --- Services Registration ---
builder.Services.AddScoped<UserService>();
// MudBlazor (if needed for server-side rendering)
builder.Services.AddMudServices();

// HttpClient for DI
builder.Services.AddHttpClient();
builder.Services.AddScoped<ChatCompletionService>();

// Register EF Core with SQLite
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register your server-side Completion service
builder.Services.AddScoped<Completion>();

// Add API Controllers
builder.Services.AddControllers();

// Add Authentication/Authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CloudflareAccessAuthenticationHandler.SchemeName;
    options.DefaultChallengeScheme = CloudflareAccessAuthenticationHandler.SchemeName;
})
.AddScheme<AuthenticationSchemeOptions, CloudflareAccessAuthenticationHandler>(
    CloudflareAccessAuthenticationHandler.SchemeName, options => { }
);

builder.Services.AddAuthorization();

// Add Blazor components (for interactive SSR or hybrid scenarios)
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// --- Automatic Database Migration ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    db.Database.Migrate();
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
