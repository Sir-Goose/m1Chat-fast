using System;
using System.Data;
using System.IO;
using System.Net.Http;
using m1Chat.Client;
using m1Chat.Client.Services;
using MudBlazor.Services;
using m1Chat.Components;
using m1Chat.Services;
using m1Chat.Data;
using m1Chat.Hubs;
using m1Chat.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

static bool SqliteTableExists(ChatDbContext db, string tableName)
{
	var connection = db.Database.GetDbConnection();
	var openedHere = connection.State != ConnectionState.Open;

	if (openedHere)
	{
		connection.Open();
	}

	try
	{
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
		var nameParam = command.CreateParameter();
		nameParam.ParameterName = "$name";
		nameParam.Value = tableName;
		command.Parameters.Add(nameParam);

		return command.ExecuteScalar() != null;
	}
	finally
	{
		if (openedHere && connection.State == ConnectionState.Open)
		{
			connection.Close();
		}
	}
}

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(".secrets", optional: true, reloadOnChange: true);

var externalSecretsPath = Environment.GetEnvironmentVariable("M1CHAT_SECRETS_FILE");
if (!string.IsNullOrWhiteSpace(externalSecretsPath))
{
	var fullSecretsPath = Path.GetFullPath(externalSecretsPath);
	var secretsDirectory = Path.GetDirectoryName(fullSecretsPath);
	var secretsFileName = Path.GetFileName(fullSecretsPath);

	if (!string.IsNullOrWhiteSpace(secretsDirectory) && !string.IsNullOrWhiteSpace(secretsFileName))
	{
		builder.Configuration.AddJsonFile(
			new PhysicalFileProvider(secretsDirectory),
			secretsFileName,
			optional: true,
			reloadOnChange: true
		);
	}
}

var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];
var hasGoogleAuth =
	!string.IsNullOrWhiteSpace(googleClientId)
	&& !string.IsNullOrWhiteSpace(googleClientSecret);

// --- Services Registration --- //
// 1) Make HttpContext available in DI
builder.Services.AddHttpContextAccessor();

// 2) Your other scoped services
builder.Services.AddScoped<UserService>();
builder.Services.AddMudServices();
builder.Services.AddScoped<ChatCompletionService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<FileUploadService>();
builder.Services.AddScoped<ChatCacheService>();
builder.Services.AddScoped<SignalRService>();
builder.Services.AddScoped<SvgIcons>();

// 3) Our dynamic‐BaseAddress HttpClient
builder.Services.AddScoped<HttpClient>(sp =>
{
	var accessor = sp.GetRequiredService<IHttpContextAccessor>();
	var httpContext = accessor.HttpContext;
	if (httpContext?.Request != null)
	{
		var req = httpContext.Request;
		var scheme = req.Scheme;           // "http" or "https"
		var host = req.Host.Value;       // "localhost:5000" etc.
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
builder.Services.AddSignalR();

builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ModelPreferencesService>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
	// Cloudflare tunnel/proxies may not appear in KnownNetworks by default.
	options.KnownNetworks.Clear();
	options.KnownProxies.Clear();
});
builder.Services.AddHttpsRedirection(options =>
{
	options.HttpsPort = 443;
});

builder.Services.AddAuthentication(options =>
{
	options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
	if (hasGoogleAuth)
	{
		options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
	}
})
    .AddCookie();

if (hasGoogleAuth)
{
	builder.Services
	    .AddAuthentication()
	    .AddGoogle(googleOptions =>
	    {
		    googleOptions.ClientId = googleClientId;
		    googleOptions.ClientSecret = googleClientSecret;
		    googleOptions.CallbackPath = "/signin-google";
		    googleOptions.SaveTokens = true;
	    });
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
	var hasMigrations = db.Database.GetMigrations().Any();

	if (hasMigrations)
	{
		db.Database.Migrate();
	}
	else
	{
		// No migrations in this project; create schema directly from the model.
		db.Database.EnsureCreated();
	}

	// Repair local dev databases created by older startup logic where only EF metadata tables existed.
	if (!SqliteTableExists(db, "Users"))
	{
		if (app.Environment.IsDevelopment())
		{
			db.Database.EnsureDeleted();
			db.Database.EnsureCreated();
		}
		else
		{
			throw new InvalidOperationException("Database schema is missing required table: Users.");
		}
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

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<UserCreationOrSyncMiddleware>();

app.MapControllers();
app.MapStaticAssets();
app.MapHub<ChatHub>("/chathub");

app.MapRazorComponents<App>()
   .AddInteractiveWebAssemblyRenderMode()
   .AddAdditionalAssemblies(typeof(m1Chat.Client._Imports).Assembly);

app.Run();
