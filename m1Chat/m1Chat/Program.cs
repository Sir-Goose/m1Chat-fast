using MudBlazor.Services;
using m1Chat.Components;
// using m1Chat.Client.Pages; // Not needed for server setup
// using m1Chat.Client.Services; // Client service, not for server DI for controllers

// Assuming your server-side Completion service is in this namespace
using m1Chat.Services;


var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services (if your server also serves Blazor Server/SSR parts that use MudBlazor)
builder.Services.AddMudServices();

// Register HttpClient factory - good practice
builder.Services.AddHttpClient();

// *** IMPORTANT: Register services for API Controllers ***
builder.Services.AddControllers();

// *** IMPORTANT: Register your SERVER-SIDE service that the controller uses ***
// Replace 'Completion' with the actual class name if different, and ensure its namespace is imported.
builder.Services.AddScoped<Completion>(); // Or AddTransient/AddSingleton as appropriate

// Add services for Blazor components (if this project also hosts Blazor UI)
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
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

// Serves static files from wwwroot (including your Blazor WASM app)
app.UseStaticFiles(); // Ensure this is present if MapStaticAssets isn't a custom extension doing this. Standard is UseStaticFiles().

app.UseAntiforgery(); // Antiforgery middleware is enabled. More on this below.

// *** IMPORTANT: Map API Controllers ***
// This should typically come before MapRazorComponents if your API routes might overlap
// with Blazor routes, though with "/api/" prefix it's usually fine.
app.MapControllers();

// Map Blazor components
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(m1Chat.Client._Imports).Assembly); // For finding client-side routable components

app.Run();
