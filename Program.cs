using CoreServer.Logic;
using CoreServer.Services;
using CoreServer.Models;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Attempt to fetch and merge config.json from Azure Blob Storage before building services
var blobServiceUri = builder.Configuration["BlobServiceUri"];
if (!string.IsNullOrWhiteSpace(blobServiceUri))
{
    try
    {
        var credential = new DefaultAzureCredential();
        var tempBlobServiceClient = new BlobServiceClient(new Uri(blobServiceUri), credential);
        var cfgContainer = builder.Configuration["ConfigContainerName"] ?? "config";
        var cfgBlobName = builder.Configuration["ConfigBlobName"] ?? "config.json";
        var cfgBlob = tempBlobServiceClient.GetBlobContainerClient(cfgContainer).GetBlobClient(cfgBlobName);
        if (await cfgBlob.ExistsAsync())
        {
            var download = await cfgBlob.DownloadContentAsync();
            var json = download.Value.Content.ToString();
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            builder.Configuration.AddJsonStream(ms);
        }
        Console.WriteLine("Config fetched");
    }
    catch
    {
        // Ignore config fetch errors; app will proceed with local configuration
        Console.WriteLine("Proceeding with local config");
    }
}
else
{
    Console.WriteLine("No blob service URI provided");
}

builder.Services.AddSingleton<IChatResponseHandler, ActionChatResponseHandler>();

// Register all chat actions automatically so every IChatAction is checked
var actionInterface = typeof(IChatAction);
var assembly = actionInterface.Assembly;
foreach (var type in assembly.GetTypes())
{
    if (type.IsClass && !type.IsAbstract && actionInterface.IsAssignableFrom(type))
    {
        builder.Services.AddSingleton(typeof(IChatAction), type);
    }
}

builder.Services.AddSingleton<IChatHub, InMemoryChatHub>();
builder.Services.AddSingleton<IRestApiService, RestApiService>();
builder.Services.AddSingleton<CoreServer.Services.IAuthService, CoreServer.Services.AuthService>();
builder.Services.AddSingleton<CoreServer.Services.ITokenService, CoreServer.Services.TokenService>();

// Register BlobServiceClient if configured
blobServiceUri = builder.Configuration["BlobServiceUri"];
if (!string.IsNullOrWhiteSpace(blobServiceUri))
{
    builder.Services.AddSingleton(new BlobServiceClient(new Uri(blobServiceUri), new DefaultAzureCredential()));
    // background blob logger
}
else
{
    // still add background service but it will no-op if BlobServiceClient is null
    builder.Services.AddSingleton<BlobServiceClient?>(_ => null);
}

builder.Services.AddHostedService<BlobLogBackgroundService>();

// using System.Threading.RateLimiting; // Uncomment for rate limiting

// builder.Services.AddRateLimiter(options =>
// {
//     options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
//         RateLimitPartition.GetFixedWindowLimiter(
//             partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
//             factory: _ => new FixedWindowRateLimiterOptions
//             {
//                 AutoReplenishment = true,
//                 PermitLimit = 10,
//                 QueueLimit = 0,
//                 Window = TimeSpan.FromSeconds(10)
//             }));
// });

var app = builder.Build();

// Initialize database objects for auth (no-op if not configured)
var authInit = app.Services.GetRequiredService<CoreServer.Services.IAuthService>();
await authInit.InitializeAsync();

// REST API endpoints
app.MapGet("/online", (IRestApiService logic) => Results.Ok(logic.OnlinePing()));
app.MapGet("/version", (IRestApiService logic) => Results.Ok(logic.GetLatestVersion()));

// Auth endpoints
app.MapPost("/auth/register", async (HttpRequest req, IAuthService auth) =>
{
    var body = await System.Text.Json.JsonSerializer.DeserializeAsync<RegisterRequest>(req.Body);
    if (body is null || string.IsNullOrWhiteSpace(body.DisplayName) || string.IsNullOrWhiteSpace(body.Password))
        return Results.BadRequest(new { error = "DisplayName and Password are required" });
    var (ok, error) = await auth.RegisterAsync(body.DisplayName.Trim(), body.Password, body.Email?.Trim());
    if (!ok) return Results.Conflict(new { error });
    return Results.Created("/auth/login", new { message = "Registered" });
});

app.MapPost("/auth/login", async (HttpRequest req, IAuthService auth, ITokenService tokens) =>
{
    var body = await System.Text.Json.JsonSerializer.DeserializeAsync<LoginRequest>(req.Body);
    if (body is null || string.IsNullOrWhiteSpace(body.DisplayName) || string.IsNullOrWhiteSpace(body.Password))
        return Results.BadRequest(new { error = "DisplayName and Password are required" });
    var (ok, userId, email, displayName, error) = await auth.LoginAsync(body.DisplayName.Trim(), body.Password);
    if (!ok || userId is null || string.IsNullOrWhiteSpace(displayName)) return Results.Unauthorized();
    var token = tokens.GenerateToken(userId, email, displayName);
    return Results.Ok(new { token, userId, email, displayName });
});

app.MapPost("/register/device", (HttpRequest req, IRestApiService logic) =>
{
    var id = req.Query["id"].ToString();
    var name = req.Query["name"].ToString();
    var mac = req.Query["mac"].ToString();
    logic.RegisterDevice(id, name, mac);
    return Results.Ok();
});


// Chat history endpoint: returns last 20 items; optional onlyMessages=true omits Event entries
app.MapGet("/chat/history", (HttpRequest req, IChatHub hub) =>
{
    var includeEvents = bool.TryParse(req.Query["includeEvents"], out var flag) && flag;
    var messages = hub.GetHistory(20, includeEvents);
    return Results.Ok(new { messages });
});

// Enforce HTTPS/HSTS in production (uncomment to enable)
// if (!app.Environment.IsDevelopment())
// {
//     app.UseHsts();
// }
// app.UseHttpsRedirection();

// Enable rate limiting (uncomment along with AddRateLimiter above)
// app.UseRateLimiter();

// WebSocket chat endpoint
app.UseWebSockets();

// Enable serving static files from wwwroot
app.UseStaticFiles();

app.Map("/ws/chat", async (HttpContext context, IChatHub hub) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await hub.HandleConnectionAsync(socket, context.RequestAborted);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

// Root endpoint
app.MapGet("/", () =>
{
    var asm = Assembly.GetExecutingAssembly();
    var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    var asmVer = asm.GetName().Version?.ToString();
    var version = !string.IsNullOrWhiteSpace(infoVer) ? infoVer : (asmVer ?? "unknown");
    return $"Core Server is running (v{version})";
});

// Viewer page now served as a static file
app.MapGet("/view", () => Results.Redirect("/view.html"));

app.Run();
