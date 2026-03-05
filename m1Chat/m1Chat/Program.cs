using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using m1Chat.Client;
using m1Chat.Client.Services;
using MudBlazor.Services;
using m1Chat.Components;
using m1Chat.Services;
using m1Chat.Data;
using m1Chat.Hubs;
using m1Chat.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;

static bool UsersTableIsQueryable(ChatDbContext db)
{
	try
	{
		_ = db.Users.AsNoTracking().Select(user => user.Id).Any();
		return true;
	}
	catch (SqliteException ex) when (
		ex.SqliteErrorCode == 1
		&& ex.Message.Contains("no such table: Users", StringComparison.OrdinalIgnoreCase)
	)
	{
		return false;
	}
}

static void AddFlattenedJsonConfig(ConfigurationManager configuration, string path)
{
	if (!File.Exists(path))
	{
		return;
	}

	using var document = JsonDocument.Parse(File.ReadAllText(path));
	var flattenedSettings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

	static void FlattenElement(JsonElement element, string prefix, IDictionary<string, string?> target)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				foreach (var property in element.EnumerateObject())
				{
					var key = string.IsNullOrEmpty(prefix)
						? property.Name
						: $"{prefix}:{property.Name}";
					FlattenElement(property.Value, key, target);
				}
				break;
			case JsonValueKind.Array:
				var index = 0;
				foreach (var item in element.EnumerateArray())
				{
					FlattenElement(item, $"{prefix}:{index}", target);
					index++;
				}
				break;
			case JsonValueKind.String:
				target[prefix] = element.GetString();
				break;
			case JsonValueKind.Number:
			case JsonValueKind.True:
			case JsonValueKind.False:
				target[prefix] = element.ToString();
				break;
			case JsonValueKind.Null:
				target[prefix] = null;
				break;
		}
	}

	FlattenElement(document.RootElement, string.Empty, flattenedSettings);
	configuration.AddInMemoryCollection(flattenedSettings);
}

var builder = WebApplication.CreateBuilder(args);

var localSecretsPath = Path.Combine(builder.Environment.ContentRootPath, ".secrets");
AddFlattenedJsonConfig(builder.Configuration, localSecretsPath);

var externalSecretsPath = Environment.GetEnvironmentVariable("M1CHAT_SECRETS_FILE");
if (!string.IsNullOrWhiteSpace(externalSecretsPath))
{
	var fullSecretsPath = Path.GetFullPath(externalSecretsPath);
	AddFlattenedJsonConfig(builder.Configuration, fullSecretsPath);
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
		    googleOptions.ClientId = googleClientId!;
		    googleOptions.ClientSecret = googleClientSecret!;
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
	var sqliteConnectionString = db.Database.GetConnectionString() ?? db.Database.GetDbConnection().ConnectionString;
	var sqliteConnectionBuilder = new SqliteConnectionStringBuilder(sqliteConnectionString);
	var databasePath = sqliteConnectionBuilder.DataSource;
	var databaseFileExisted = !string.IsNullOrWhiteSpace(databasePath)
		&& File.Exists(Path.GetFullPath(databasePath));
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
	if (databaseFileExisted && !UsersTableIsQueryable(db))
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
