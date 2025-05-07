using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Http; 
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();
builder.Services.AddHttpClient();  

await builder.Build().RunAsync();