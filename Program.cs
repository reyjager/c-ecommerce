using Microsoft.EntityFrameworkCore;
using MyMvcProject.Data;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebSockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add WebSocket support
builder.Services.AddWebSockets(options =>
{
    options.KeepAliveInterval = TimeSpan.FromMinutes(2);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Enable WebSockets
app.UseWebSockets();

// List of active WebSocket connections for hot reload
var activeConnections = new List<WebSocket>();

// Hot reload WebSocket endpoint
if (app.Environment.IsDevelopment())
{
    // WebSocket endpoint for hot reload
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/hot-reload")
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                activeConnections.Add(webSocket);

                // Keep the connection open
                try
                {
                    var buffer = new byte[1024 * 4];
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    while (!result.CloseStatus.HasValue)
                    {
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    }

                    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                }
                catch
                {
                    // Connection closed or error
                }
                finally
                {
                    activeConnections.Remove(webSocket);
                }
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        }
        else if (context.Request.Path == "/hot-reload-sse")
        {
            // Server-Sent Events endpoint
            context.Response.Headers.Add("Content-Type", "text/event-stream");
            context.Response.Headers.Add("Cache-Control", "no-cache");
            context.Response.Headers.Add("Connection", "keep-alive");

            // Send initial connection message
            await context.Response.WriteAsync("data: connected\n\n");
            await context.Response.Body.FlushAsync();

            // Keep connection open
            var tcs = new TaskCompletionSource<bool>();
            context.RequestAborted.Register(() => tcs.TrySetResult(true));
            await tcs.Task;
        }
        else if (context.Request.Path == "/hot-reload-poll")
        {
            // Simple polling endpoint
            context.Response.Headers.Add("Cache-Control", "no-cache");
            await context.Response.WriteAsync("no-change");
        }
        else if (context.Request.Path == "/trigger-reload")
        {
            // Endpoint to manually trigger reload (for testing)
            await TriggerHotReload();
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("Reload triggered");
        }
        else
        {
            await next();
        }
    });

    // File system watcher for hot reload
    var contentRoot = app.Environment.ContentRootPath;
    var fileWatcher = new FileSystemWatcher(contentRoot);
    fileWatcher.IncludeSubdirectories = true;
    fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
    fileWatcher.Changed += async (sender, e) => await OnFileChanged(e.FullPath);
    fileWatcher.Created += async (sender, e) => await OnFileChanged(e.FullPath);
    fileWatcher.Renamed += async (sender, e) => await OnFileChanged(e.FullPath);
    fileWatcher.EnableRaisingEvents = true;

    // Helper method to trigger hot reload
    async Task TriggerHotReload()
    {
        var reloadMessage = Encoding.UTF8.GetBytes("reload");

        // Send reload message to all WebSocket connections
        foreach (var socket in activeConnections.ToList())
        {
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.SendAsync(new ArraySegment<byte>(reloadMessage), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    // Connection might be closed
                }
            }
        }
    }

    // Helper method to handle file changes
    async Task OnFileChanged(string path)
    {
        // Only trigger reload for relevant file types
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var relevantExtensions = new[] { ".cshtml", ".cs", ".css", ".js" };

        if (relevantExtensions.Contains(extension))
        {
            // Add a small delay to ensure file is fully written
            await Task.Delay(100);
            await TriggerHotReload();
        }
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();