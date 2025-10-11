using CoreServer.Logic;
using CoreServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Register services and logic layer
builder.Services.AddSingleton<IUserRegistry, InMemoryUserRegistry>();
builder.Services.AddSingleton<IChatResponseHandler, DefaultChatResponseHandler>();
builder.Services.AddSingleton<IChatHub, InMemoryChatHub>();
builder.Services.AddSingleton<IRestApiService, RestApiService>();

var app = builder.Build();

// REST API endpoints
app.MapGet("/online", (IRestApiService logic) => Results.Ok(logic.OnlinePing()));
app.MapGet("/version", (IRestApiService logic) => Results.Ok(logic.GetLatestVersion()));
app.MapPost("/register", (HttpRequest req, IRestApiService logic) =>
{
    var id = req.Query["id"].ToString();
    var name = req.Query["name"].ToString();
    logic.Register(id, name); // intentionally left as a stub per requirements
    return Results.NoContent();
});

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

// Optional root endpoint
app.MapGet("/", () => "Core Server is running");

// Viewer page now served as a static file
app.MapGet("/view", () => Results.Redirect("/view.html"));

app.Run();
