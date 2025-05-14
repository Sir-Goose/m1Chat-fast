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
builder.Services.AddMudServices();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ChatCompletionService>();

builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<Completion>();
builder.Services.AddControllers();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CloudflareAccessAuthenticationHandler.SchemeName;
    options.DefaultChallengeScheme = CloudflareAccessAuthenticationHandler.SchemeName;
})
.AddScheme<AuthenticationSchemeOptions, CloudflareAccessAuthenticationHandler>(
    CloudflareAccessAuthenticationHandler.SchemeName, options => { }
);

builder.Services.AddAuthorization();

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
