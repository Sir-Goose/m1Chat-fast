using m1Chat.Client;
using m1Chat.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();
// Add authentication services
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddAuthorizationCore();

// Register HttpClient with BaseAddress for DI
builder.Services.AddScoped(sp => new HttpClient
{
	BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
	Timeout = TimeSpan.FromMinutes(5)
});

// Register other services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<ChatCompletionService>();
builder.Services.AddScoped<FileUploadService>();
builder.Services.AddScoped<ChatCacheService>();
builder.Services.AddScoped<SignalRService>();
builder.Services.AddScoped<SvgIcons>();
builder.Services.AddScoped<ModelPreferencesService>();

await builder.Build().RunAsync();
