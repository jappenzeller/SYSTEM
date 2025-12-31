using System.Net;
using System.Text;
using System.Text.Json;
using SYSTEM.HeadlessClient.Mcp;

namespace SYSTEM.HeadlessClient.Api;

/// <summary>
/// Simple HTTP server for QAI command API.
/// Allows external control and status queries for testing.
/// </summary>
public class CommandServer : IDisposable
{
    private HttpListener _listener;
    private readonly CancellationTokenSource _cts;
    private readonly CommandHandler _handler;
    private McpServer? _mcpServer;
    private Task? _listenTask;

    public int Port { get; }
    public bool IsRunning { get; private set; }

    public CommandServer(CommandHandler handler, int port = 8080)
    {
        _handler = handler;
        Port = port;
        _listener = new HttpListener();
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Set the MCP server for handling MCP protocol requests.
    /// </summary>
    public void SetMcpServer(McpServer mcpServer)
    {
        _mcpServer = mcpServer;
    }

    public void Start()
    {
        if (IsRunning) return;

        // Try http://+: first (for external access like NLB health checks)
        _listener.Prefixes.Add($"http://+:{Port}/");

        try
        {
            _listener.Start();
            IsRunning = true;
            Console.WriteLine($"[API] Command server started on port {Port} (all interfaces)");
            Console.WriteLine($"[API] Endpoints: /status, /sources, /mine/start, /mine/stop, /move, /scan, /walk, /plan, /mcp");

            _listenTask = Task.Run(ListenLoop);
        }
        catch (HttpListenerException)
        {
            // Fall back to localhost-only binding (no admin required on Windows)
            Console.WriteLine($"[API] External binding failed, trying localhost...");

            // Create a fresh listener for localhost
            _listener.Close();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");

            try
            {
                _listener.Start();
                IsRunning = true;
                Console.WriteLine($"[API] Command server started on localhost:{Port}");
                Console.WriteLine($"[API] Endpoints: /status, /sources, /mine/start, /mine/stop, /move, /scan, /walk, /plan, /mcp");
                _listenTask = Task.Run(ListenLoop);
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"[API] Failed to start command server: {ex.Message}");
                Console.WriteLine($"[API] Port {Port} may be in use by another application");
            }
        }
    }

    private async Task ListenLoop()
    {
        while (IsRunning && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context));
            }
            catch (HttpListenerException) when (_cts.Token.IsCancellationRequested)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] Error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var path = request.Url?.AbsolutePath ?? "/";
            var method = request.HttpMethod;

            // Strip /qai prefix if present (for CloudFront routing)
            if (path.StartsWith("/qai", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(4); // Remove "/qai"
            if (string.IsNullOrEmpty(path) || path == "/")
                path = "/status"; // Default to status

            Console.WriteLine($"[API] {method} {path}");

            object? result = null;
            int statusCode = 200;

            switch (path.ToLower())
            {
                case "/status":
                    result = _handler.GetStatus();
                    break;

                case "/sources":
                    result = _handler.GetSources();
                    break;

                case "/mine/start":
                    if (method == "POST")
                    {
                        var body = await ReadBody(request);
                        result = _handler.StartMining(body);
                    }
                    else
                    {
                        statusCode = 405;
                        result = new { error = "Method not allowed" };
                    }
                    break;

                case "/mine/stop":
                    if (method == "POST")
                    {
                        result = _handler.StopMining();
                    }
                    else
                    {
                        statusCode = 405;
                        result = new { error = "Method not allowed" };
                    }
                    break;

                case "/move":
                    if (method == "POST")
                    {
                        var body = await ReadBody(request);
                        result = _handler.Move(body);
                    }
                    else
                    {
                        statusCode = 405;
                        result = new { error = "Method not allowed" };
                    }
                    break;

                case "/scan":
                    if (method == "POST")
                    {
                        result = _handler.ForceScan();
                    }
                    else
                    {
                        statusCode = 405;
                        result = new { error = "Method not allowed" };
                    }
                    break;

                case "/walk":
                    if (method == "POST")
                    {
                        var body = await ReadBody(request);
                        result = _handler.Walk(body);
                    }
                    else if (method == "GET")
                    {
                        result = _handler.GetWalkingStatus();
                    }
                    else
                    {
                        statusCode = 405;
                        result = new { error = "Method not allowed" };
                    }
                    break;

                case "/plan":
                    if (method == "POST")
                    {
                        var body = await ReadBody(request);
                        result = _handler.ExecutePlan(body);
                    }
                    else if (method == "GET")
                    {
                        result = _handler.GetPlanStatus();
                    }
                    else
                    {
                        statusCode = 405;
                        result = new { error = "Method not allowed" };
                    }
                    break;

                case "/mcp":
                    if (method == "POST")
                    {
                        if (_mcpServer == null)
                        {
                            statusCode = 503;
                            result = new { error = "MCP server not initialized" };
                        }
                        else
                        {
                            var body = await ReadBody(request);
                            try
                            {
                                var mcpRequest = JsonSerializer.Deserialize<McpRequest>(body, new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });
                                if (mcpRequest == null)
                                {
                                    result = McpResponse.Failure(McpError.ParseError, "Failed to parse request", null);
                                }
                                else
                                {
                                    result = _mcpServer.HandleRequest(mcpRequest);
                                }
                            }
                            catch (JsonException ex)
                            {
                                result = McpResponse.Failure(McpError.ParseError, ex.Message, null);
                            }
                        }
                    }
                    else
                    {
                        statusCode = 405;
                        result = new { error = "Method not allowed. MCP requires POST." };
                    }
                    break;

                default:
                    statusCode = 404;
                    result = new { error = "Not found", availableEndpoints = new[]
                    {
                        "GET /status",
                        "GET /sources",
                        "POST /mine/start",
                        "POST /mine/stop",
                        "POST /move",
                        "POST /scan",
                        "GET /walk",
                        "POST /walk",
                        "GET /plan",
                        "POST /plan",
                        "POST /mcp (JSON-RPC 2.0)"
                    }};
                    break;
            }

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            var buffer = Encoding.UTF8.GetBytes(json);

            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            var error = JsonSerializer.Serialize(new { error = ex.Message });
            var buffer = Encoding.UTF8.GetBytes(error);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
        }
        finally
        {
            response.Close();
        }
    }

    private static async Task<string> ReadBody(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        return await reader.ReadToEndAsync();
    }

    public void Stop()
    {
        if (!IsRunning) return;

        Console.WriteLine("[API] Stopping command server...");
        IsRunning = false;
        _cts.Cancel();
        _listener.Stop();
    }

    public void Dispose()
    {
        Stop();
        _listener.Close();
        _cts.Dispose();
    }
}
