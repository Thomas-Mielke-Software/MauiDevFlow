using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;

namespace MauiDevFlow.Blazor;

/// <summary>
/// A WebSocket server that bridges external Chrome DevTools Protocol clients
/// to the Chobitsu instance running inside the WebView.
/// 
/// This server:
/// 1. Accepts WebSocket connections from external tools (Chrome DevTools, Selenium, etc.)
/// 2. Forwards CDP messages to the WebView via a callback
/// 3. Receives CDP responses from the WebView and sends them back to clients
/// </summary>
public class ChobitsuWebSocketBridge : IDisposable
{
    private TcpListener? _tcpListener;
    private readonly ConcurrentDictionary<string, TcpClient> _connections = new();
    private readonly ConcurrentDictionary<string, NetworkStream> _streams = new();
    private readonly int _port;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _disposed;

    /// <summary>
    /// Called when a CDP message is received from an external client.
    /// The MAUI app should forward this to the WebView's Chobitsu instance.
    /// </summary>
    public event Action<string, string>? OnMessageFromClient;

    /// <summary>
    /// Called when a new client connects.
    /// </summary>
    public event Action<string>? OnClientConnected;

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public event Action<string>? OnClientDisconnected;
    
    /// <summary>
    /// Optional logging callback for debugging.
    /// </summary>
    public Action<string>? LogCallback { get; set; }

    /// <summary>
    /// Gets whether the server is currently running.
    /// </summary>
    public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;

    /// <summary>
    /// Gets the port the server is listening on.
    /// </summary>
    public int Port => _port;

    /// <summary>
    /// Gets the number of connected clients.
    /// </summary>
    public int ConnectionCount => _connections.Count;

    public ChobitsuWebSocketBridge(int port = 9222)
    {
        _port = port;
    }
    
    private void Log(string message)
    {
        var fullMessage = $"[Bridge] {message}";
        Console.WriteLine(fullMessage);
        // Try both ways to ensure we get output
        try { LogCallback?.Invoke(fullMessage); } catch { }
        // Also write to a temp file for debugging
        try { 
            var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mauibridge.log");
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {fullMessage}\n");
        } catch { }
    }

    /// <summary>
    /// Starts the WebSocket server.
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        Log($"Starting WebSocket server on port {_port}...");

        try
        {
            _tcpListener = new TcpListener(IPAddress.Loopback, _port);
            _tcpListener.Start();
            Log($"WebSocket server listening on port {_port}");;
            Console.WriteLine($"[ChobitsuBridge] Connect via: ws://localhost:{_port}/");
            Console.WriteLine($"[ChobitsuBridge] CDP endpoint: http://localhost:{_port}/json");

            _listenerTask = AcceptConnectionsAsync(_cts.Token);
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException(
                $"Cannot start WebSocket server on port {_port}. Port may be in use.",
                ex);
        }
    }

    /// <summary>
    /// Stops the WebSocket server.
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _cts?.Cancel();

        // Close all connections
        foreach (var (id, client) in _connections)
        {
            try
            {
                client.Close();
            }
            catch { }
        }
        _connections.Clear();
        _streams.Clear();

        _tcpListener?.Stop();

        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask;
            }
            catch (OperationCanceledException) { }
        }

        Console.WriteLine("[ChobitsuBridge] Server stopped");
    }

    /// <summary>
    /// Sends a CDP message to all connected clients.
    /// Call this when Chobitsu in the WebView produces a message.
    /// </summary>
    public async Task SendToClientsAsync(string message)
    {
        foreach (var (id, stream) in _streams)
        {
            try
            {
                if (_connections.TryGetValue(id, out var client) && client.Connected)
                {
                    var frame = CreateWebSocketFrame(message);
                    await stream.WriteAsync(frame);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChobitsuBridge] Error sending to client {id}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Sends a CDP message to a specific client.
    /// </summary>
    public async Task SendToClientAsync(string connectionId, string message)
    {
        if (_streams.TryGetValue(connectionId, out var stream) && 
            _connections.TryGetValue(connectionId, out var client) && client.Connected)
        {
            var preview = message.Length > 100 ? message.Substring(0, 100) + "..." : message;
            Log($"Sending response: {preview}");
            var frame = CreateWebSocketFrame(message);
            await stream.WriteAsync(frame);
        }
        else
        {
            Log($"WARNING: Could not send to {connectionId} - client not connected");
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var client = await _tcpListener.AcceptTcpClientAsync(ct);
                _ = HandleConnectionAsync(client, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChobitsuBridge] Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct)
    {
        var connectionId = Guid.NewGuid().ToString("N");
        var stream = client.GetStream();
        var buffer = new byte[16384];

        try
        {
            // Read HTTP request
            var bytesRead = await stream.ReadAsync(buffer, ct);
            var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            // Parse request line
            var lines = request.Split('\n');
            var requestLine = lines[0].Trim();
            var parts = requestLine.Split(' ');
            var path = parts.Length > 1 ? parts[1].TrimEnd('/') : "/"; // Normalize trailing slash

            // Handle HTTP endpoints
            if (path == "/json" || path == "/json/list")
            {
                await SendHttpResponseAsync(stream, GetJsonTargetList());
                client.Close();
                return;
            }

            if (path == "/json/version")
            {
                await SendHttpResponseAsync(stream, GetVersionInfo());
                client.Close();
                return;
            }

            // Handle WebSocket upgrade
            if (request.Contains("Upgrade: websocket", StringComparison.OrdinalIgnoreCase))
            {
                var key = ExtractWebSocketKey(request);
                if (key != null)
                {
                    await SendWebSocketHandshakeAsync(stream, key);
                    
                    _connections[connectionId] = client;
                    _streams[connectionId] = stream;
                    Log($"WS client connected: {connectionId}, path: {path}");
                    OnClientConnected?.Invoke(connectionId);

                    // Handle WebSocket messages
                    await HandleWebSocketMessagesAsync(connectionId, stream, ct);
                }
                else
                {
                    Log($"WS handshake failed - no key found");
                }
            }
            else
            {
                Log($"Unknown HTTP request: {request.Substring(0, Math.Min(100, request.Length))}");
                // Unknown request
                await SendHttpResponseAsync(stream, "Not Found", 404);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChobitsuBridge] Connection error: {ex.Message}");
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            _streams.TryRemove(connectionId, out _);
            
            if (_connections.ContainsKey(connectionId))
            {
                Console.WriteLine($"[ChobitsuBridge] Client disconnected: {connectionId}");
                OnClientDisconnected?.Invoke(connectionId);
            }

            try { client.Close(); } catch { }
        }
    }

    private async Task HandleWebSocketMessagesAsync(string connectionId, NetworkStream stream, CancellationToken ct)
    {
        Log($"Starting WS message handler for {connectionId}");
        var buffer = new byte[16384];
        var remainingBuffer = new List<byte>();

        while (!ct.IsCancellationRequested && _connections.TryGetValue(connectionId, out var client) && client.Connected)
        {
            try
            {
                Log($"Waiting to read from {connectionId}...");
                var bytesRead = await stream.ReadAsync(buffer, ct);
                Log($"Received {bytesRead} bytes from {connectionId}");
                
                if (bytesRead == 0)
                {
                    Log($"Connection {connectionId} closed (0 bytes read)");
                    break;
                }

                // Combine with any remaining bytes from previous read
                remainingBuffer.AddRange(buffer.Take(bytesRead));
                var processBuffer = remainingBuffer.ToArray();
                
                // Process all complete frames in the buffer
                int offset = 0;
                while (offset < processBuffer.Length)
                {
                    var (message, opcode, consumed) = DecodeWebSocketFrameWithOffset(processBuffer, offset, processBuffer.Length - offset);
                    
                    if (consumed == 0)
                    {
                        // Incomplete frame, wait for more data
                        break;
                    }
                    
                    Log($"Decoded frame: opcode={opcode}, msgLen={message?.Length ?? 0}, consumed={consumed}");
                    offset += consumed;
                    
                    if (opcode == 8) // Close frame
                    {
                        Log($"Close frame received from {connectionId}");
                        remainingBuffer.Clear();
                        return;
                    }

                    if (opcode == 1 && !string.IsNullOrEmpty(message)) // Text frame
                    {
                        await ProcessWebSocketMessageAsync(connectionId, message);
                    }
                }
                
                // Keep any unprocessed bytes for next iteration
                remainingBuffer = remainingBuffer.Skip(offset).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Error reading from client: {ex.Message}");
                break;
            }
        }
    }

    private async Task ProcessWebSocketMessageAsync(string connectionId, string message)
    {
        try
        {
            var preview = message.Substring(0, Math.Min(150, message.Length));
            Log($"ProcessMsg: {preview}");
            
            // Parse the message to check for sessionId
            string? sessionId = null;
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(message);
                if (json.RootElement.TryGetProperty("sessionId", out var sessionProp))
                {
                    sessionId = sessionProp.GetString();
                }
            }
            catch { /* Ignore parse errors, continue with null sessionId */ }
            
            var isBrowser = message.Contains("\"method\":\"Browser.");
            var isTarget = message.Contains("\"method\":\"Target.");
            var isPage = message.Contains("\"method\":\"Page.");
            Log($"Routing: Browser={isBrowser}, Target={isTarget}, Page={isPage}");
            
            // Intercept Browser.* methods - Chobitsu doesn't implement them
            if (isBrowser)
            {
                Log("-> Browser method intercepted");
                await HandleBrowserMethodAsync(connectionId, message, sessionId);
            }
            // Intercept Target.* methods - Chobitsu doesn't implement them
            else if (isTarget)
            {
                Log("-> Target method intercepted");
                await HandleTargetMethodAsync(connectionId, message, sessionId);
            }
            // Intercept Page.* methods that Chobitsu doesn't implement well
            else if (isPage)
            {
                Log("-> Page method intercepted");
                await HandlePageMethodAsync(connectionId, message, sessionId);
            }
            // Intercept specific setup methods that Playwright needs but Chobitsu doesn't handle
            else if (message.Contains("\"method\":\"Log.enable\"") ||
                     message.Contains("\"method\":\"Runtime.enable\"") ||
                     message.Contains("\"method\":\"Runtime.runIfWaitingForDebugger\"") ||
                     message.Contains("\"method\":\"Network.enable\"") ||
                     message.Contains("\"method\":\"Emulation."))
            {
                Log("-> Generic method intercepted");
                await HandleGenericMethodAsync(connectionId, message, sessionId);
            }
            // Intercept Accessibility.* methods - Chobitsu doesn't implement them
            else if (message.Contains("\"method\":\"Accessibility."))
            {
                Log("-> Accessibility method intercepted");
                await HandleAccessibilityMethodAsync(connectionId, message, sessionId);
            }
            else
            {
                Log("-> Forwarding to Chobitsu");
                // Forward to Chobitsu for actual work (Runtime.evaluate, DOM.*, etc.)
                OnMessageFromClient?.Invoke(connectionId, message);
            }
            Log("ProcessMsg completed");
        }
        catch (Exception ex)
        {
            Log($"ERROR in ProcessMsg: {ex.Message}");
        }
    }

    private string CreateResponse(int id, string result, string? sessionId = null)
    {
        if (sessionId != null)
        {
            return $"{{\"id\":{id},\"result\":{result},\"sessionId\":\"{sessionId}\"}}";
        }
        return $"{{\"id\":{id},\"result\":{result}}}";
    }

    private async Task HandleGenericMethodAsync(string connectionId, string message, string? sessionId)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(message);
            var id = json.RootElement.GetProperty("id").GetInt32();
            var method = json.RootElement.GetProperty("method").GetString();
            
            Console.WriteLine($"[ChobitsuBridge] Handling generic {method}");
            
            // Return empty result for most methods
            var response = CreateResponse(id, "{}", sessionId);
            await SendToClientAsync(connectionId, response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChobitsuBridge] Error handling generic method: {ex.Message}");
        }
    }

    private async Task HandleBrowserMethodAsync(string connectionId, string message, string? sessionId)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(message);
            var id = json.RootElement.GetProperty("id").GetInt32();
            var method = json.RootElement.GetProperty("method").GetString();
            
            Console.WriteLine($"[ChobitsuBridge] Handling {method}");
            
            string response;
            if (method == "Browser.getVersion")
            {
                var result = """
                    {
                        "protocolVersion": "1.3",
                        "product": "MAUI Blazor WebView/1.0",
                        "revision": "1.0.0",
                        "userAgent": "MauiDevFlow/1.0",
                        "jsVersion": "N/A"
                    }
                    """;
                response = CreateResponse(id, result, sessionId);
            }
            else
            {
                // For all other Browser.* methods, return empty result
                response = CreateResponse(id, "{}", sessionId);
            }
            
            await SendToClientAsync(connectionId, response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChobitsuBridge] Error handling Browser method: {ex.Message}");
        }
    }

    private async Task HandleTargetMethodAsync(string connectionId, string message, string? sessionId)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(message);
            var id = json.RootElement.GetProperty("id").GetInt32();
            var method = json.RootElement.GetProperty("method").GetString();
            
            Console.WriteLine($"[ChobitsuBridge] Handling {method}");
            
            // Return appropriate responses for Target.* methods
            string response;
            if (method == "Target.getTargets")
            {
                var result = """
                    {
                        "targetInfos": [{
                            "targetId": "maui-blazor-webview",
                            "type": "page",
                            "title": "MAUI Blazor App",
                            "url": "about:blank",
                            "attached": true,
                            "browserContextId": "default-context"
                        }]
                    }
                    """;
                response = CreateResponse(id, result, sessionId);
            }
            else if (method == "Target.getTargetInfo")
            {
                var result = """
                    {
                        "targetInfo": {
                            "targetId": "maui-blazor-webview",
                            "type": "page",
                            "title": "MAUI Blazor App",
                            "url": "about:blank",
                            "attached": true,
                            "browserContextId": "default-context"
                        }
                    }
                    """;
                response = CreateResponse(id, result, sessionId);
            }
            else if (method == "Target.attachToTarget")
            {
                var result = """{"sessionId": "maui-session-1"}""";
                response = CreateResponse(id, result, sessionId);
            }
            else if (method == "Target.setAutoAttach")
            {
                // After setAutoAttach on the browser session (no sessionId), emit Target.attachedToTarget
                response = CreateResponse(id, "{}", sessionId);
                await SendToClientAsync(connectionId, response);
                
                // Only emit attachedToTarget event for the initial browser-level setAutoAttach (no sessionId)
                // The nested one (with sessionId) shouldn't re-emit the event
                if (sessionId == null)
                {
                    // Small delay before sending event
                    await Task.Delay(50);
                    
                    // Send Target.attachedToTarget event for the existing page
                    var attachedEvent = """
                        {
                            "method": "Target.attachedToTarget",
                            "params": {
                                "sessionId": "maui-session-1",
                                "targetInfo": {
                                    "targetId": "maui-blazor-webview",
                                    "type": "page",
                                    "title": "MAUI Blazor App",
                                    "url": "about:blank",
                                    "attached": true,
                                    "browserContextId": "default-context"
                                },
                                "waitingForDebugger": false
                            }
                        }
                        """;
                    Console.WriteLine($"[ChobitsuBridge] Sending attachedToTarget event");
                    await SendToClientAsync(connectionId, attachedEvent);
                }
                return; // Already sent response
            }
            else if (method == "Target.setDiscoverTargets")
            {
                response = CreateResponse(id, "{}", sessionId);
                await SendToClientAsync(connectionId, response);
                
                // Small delay before sending event
                await Task.Delay(50);
                
                // Send Target.targetCreated event for the existing page
                var createdEvent = """
                    {
                        "method": "Target.targetCreated",
                        "params": {
                            "targetInfo": {
                                "targetId": "maui-blazor-webview",
                                "type": "page",
                                "title": "MAUI Blazor App",
                                "url": "about:blank",
                                "attached": false,
                                "browserContextId": "default-context"
                            }
                        }
                    }
                    """;
                await SendToClientAsync(connectionId, createdEvent);
                return; // Already sent response
            }
            else if (method == "Target.createBrowserContext")
            {
                var result = """{"browserContextId": "default-context"}""";
                response = CreateResponse(id, result, sessionId);
            }
            else if (method == "Target.createTarget")
            {
                var result = """{"targetId": "maui-blazor-webview"}""";
                response = CreateResponse(id, result, sessionId);
            }
            else if (method == "Target.getBrowserContexts")
            {
                // Return the default browser context
                var result = """{"browserContextIds": ["default-context"]}""";
                response = CreateResponse(id, result, sessionId);
            }
            else
            {
                // Generic empty result for other Target methods
                response = CreateResponse(id, "{}", sessionId);
            }
            
            await SendToClientAsync(connectionId, response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChobitsuBridge] Error handling Target method: {ex.Message}");
        }
    }

    private async Task HandlePageMethodAsync(string connectionId, string message, string? sessionId)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(message);
            var id = json.RootElement.GetProperty("id").GetInt32();
            var method = json.RootElement.GetProperty("method").GetString();
            
            Console.WriteLine($"[ChobitsuBridge] Handling {method}");
            
            string? response = null;
            
            if (method == "Page.getFrameTree")
            {
                var result = """
                    {
                        "frameTree": {
                            "frame": {
                                "id": "main-frame",
                                "loaderId": "loader-1",
                                "url": "about:blank",
                                "domainAndRegistry": "",
                                "securityOrigin": "://",
                                "mimeType": "text/html",
                                "adFrameStatus": {
                                    "adFrameType": "none"
                                },
                                "secureContextType": "InsecureScheme",
                                "crossOriginIsolatedContextType": "NotIsolated",
                                "gatedAPIFeatures": []
                            },
                            "childFrames": []
                        }
                    }
                    """;
                response = CreateResponse(id, result, sessionId);
            }
            else if (method == "Page.enable" || method == "Page.disable" ||
                     method == "Page.setInterceptFileChooserDialog")
            {
                response = CreateResponse(id, "{}", sessionId);
            }
            else if (method == "Page.addScriptToEvaluateOnNewDocument")
            {
                // Must return an identifier
                var result = """{"identifier": "1"}""";
                response = CreateResponse(id, result, sessionId);
            }
            else if (method == "Page.setLifecycleEventsEnabled")
            {
                response = CreateResponse(id, "{}", sessionId);
                await SendToClientAsync(connectionId, response);
                
                // Emit lifecycle events to indicate page is loaded
                var enabled = json.RootElement.TryGetProperty("params", out var paramsEl) &&
                              paramsEl.TryGetProperty("enabled", out var enabledEl) &&
                              enabledEl.GetBoolean();
                
                if (enabled && sessionId != null)
                {
                    await Task.Delay(10);
                    
                    // Send lifecycle events for an already-loaded page
                    var domLoadedEvent = $$"""
                        {
                            "method": "Page.lifecycleEvent",
                            "params": {
                                "frameId": "main-frame",
                                "loaderId": "loader-1",
                                "name": "DOMContentLoaded",
                                "timestamp": {{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0}}
                            },
                            "sessionId": "{{sessionId}}"
                        }
                        """;
                    await SendToClientAsync(connectionId, domLoadedEvent);
                    
                    var loadEvent = $$"""
                        {
                            "method": "Page.lifecycleEvent",
                            "params": {
                                "frameId": "main-frame",
                                "loaderId": "loader-1",
                                "name": "load",
                                "timestamp": {{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0}}
                            },
                            "sessionId": "{{sessionId}}"
                        }
                        """;
                    await SendToClientAsync(connectionId, loadEvent);
                    
                    // Also send frameStoppedLoading which Playwright may wait for
                    var stoppedEvent = $$"""
                        {
                            "method": "Page.frameStoppedLoading",
                            "params": {
                                "frameId": "main-frame"
                            },
                            "sessionId": "{{sessionId}}"
                        }
                        """;
                    await SendToClientAsync(connectionId, stoppedEvent);
                    
                    // And loadEventFired
                    var loadFiredEvent = $$"""
                        {
                            "method": "Page.loadEventFired",
                            "params": {
                                "timestamp": {{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0}}
                            },
                            "sessionId": "{{sessionId}}"
                        }
                        """;
                    await SendToClientAsync(connectionId, loadFiredEvent);
                }
                return; // Already sent response
            }
            
            if (response != null)
            {
                await SendToClientAsync(connectionId, response);
            }
            else
            {
                // Forward to Chobitsu for methods it might support
                OnMessageFromClient?.Invoke(connectionId, message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChobitsuBridge] Error handling Page method: {ex.Message}");
        }
    }

    private async Task HandleAccessibilityMethodAsync(string connectionId, string message, string? sessionId)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(message);
            var id = json.RootElement.GetProperty("id").GetInt32();
            var method = json.RootElement.GetProperty("method").GetString();
            
            Console.WriteLine($"[ChobitsuBridge] Handling {method}");
            
            string response;
            
            if (method == "Accessibility.enable" || method == "Accessibility.disable")
            {
                response = CreateResponse(id, "{}", sessionId);
            }
            else if (method == "Accessibility.getFullAXTree")
            {
                // Return a minimal accessibility tree with just a document root
                var result = """
                    {
                        "nodes": [{
                            "nodeId": "1",
                            "ignored": false,
                            "role": { "type": "role", "value": "RootWebArea" },
                            "name": { "type": "computedString", "value": "MAUI Blazor App" },
                            "childIds": []
                        }]
                    }
                    """;
                response = CreateResponse(id, result, sessionId);
            }
            else if (method == "Accessibility.getPartialAXTree" || method == "Accessibility.queryAXTree")
            {
                // Return empty nodes for partial queries
                var result = """{"nodes": []}""";
                response = CreateResponse(id, result, sessionId);
            }
            else
            {
                // Default empty result for other Accessibility methods
                response = CreateResponse(id, "{}", sessionId);
            }
            
            await SendToClientAsync(connectionId, response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChobitsuBridge] Error handling Accessibility method: {ex.Message}");
        }
    }

    private string? ExtractWebSocketKey(string request)
    {
        var match = Regex.Match(request, @"Sec-WebSocket-Key:\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private async Task SendWebSocketHandshakeAsync(NetworkStream stream, string key)
    {
        var acceptKey = Convert.ToBase64String(
            System.Security.Cryptography.SHA1.HashData(
                Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

        var response = $"HTTP/1.1 101 Switching Protocols\r\n" +
                       $"Upgrade: websocket\r\n" +
                       $"Connection: Upgrade\r\n" +
                       $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

        var bytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(bytes);
    }

    private async Task SendHttpResponseAsync(NetworkStream stream, string body, int statusCode = 200)
    {
        var statusText = statusCode == 200 ? "OK" : "Not Found";
        var response = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                       $"Content-Type: application/json\r\n" +
                       $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
                       $"Connection: close\r\n\r\n" +
                       body;

        var bytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(bytes);
    }

    private string GetJsonTargetList()
    {
        return $$"""
        [{
            "description": "MAUI Blazor WebView",
            "devtoolsFrontendUrl": "devtools://devtools/bundled/inspector.html?ws=localhost:{{_port}}/",
            "id": "maui-blazor-webview",
            "title": "MAUI Blazor App",
            "type": "page",
            "url": "about:blank",
            "webSocketDebuggerUrl": "ws://localhost:{{_port}}/"
        }]
        """;
    }

    private string GetVersionInfo()
    {
        return $$"""
        {
            "Browser": "MAUI Blazor WebView/1.0",
            "Protocol-Version": "1.3",
            "User-Agent": "MauiDevFlow",
            "V8-Version": "N/A",
            "WebKit-Version": "N/A",
            "webSocketDebuggerUrl": "ws://localhost:{{_port}}/devtools/browser"
        }
        """;
    }

    private byte[] CreateWebSocketFrame(string message)
    {
        var payload = Encoding.UTF8.GetBytes(message);
        var frame = new List<byte>();

        // FIN + text opcode
        frame.Add(0x81);

        // Payload length
        if (payload.Length <= 125)
        {
            frame.Add((byte)payload.Length);
        }
        else if (payload.Length <= 65535)
        {
            frame.Add(126);
            frame.Add((byte)(payload.Length >> 8));
            frame.Add((byte)(payload.Length & 0xFF));
        }
        else
        {
            frame.Add(127);
            for (int i = 7; i >= 0; i--)
            {
                frame.Add((byte)(payload.Length >> (i * 8) & 0xFF));
            }
        }

        frame.AddRange(payload);
        return frame.ToArray();
    }

    private (string message, int opcode) DecodeWebSocketFrame(byte[] buffer, int length)
    {
        var (message, opcode, _) = DecodeWebSocketFrameWithOffset(buffer, 0, length);
        return (message, opcode);
    }

    private (string message, int opcode, int consumed) DecodeWebSocketFrameWithOffset(byte[] buffer, int startOffset, int length)
    {
        if (length < 2) return (string.Empty, 0, 0);

        var opcode = buffer[startOffset] & 0x0F;
        var masked = (buffer[startOffset + 1] & 0x80) != 0;
        var payloadLength = buffer[startOffset + 1] & 0x7F;
        
        var headerOffset = 2;
        
        if (payloadLength == 126)
        {
            if (length < 4) return (string.Empty, 0, 0); // Not enough data
            payloadLength = (buffer[startOffset + 2] << 8) | buffer[startOffset + 3];
            headerOffset = 4;
        }
        else if (payloadLength == 127)
        {
            if (length < 10) return (string.Empty, 0, 0); // Not enough data
            // 64-bit length - simplified for reasonable messages
            payloadLength = (int)((buffer[startOffset + 6] << 24) | (buffer[startOffset + 7] << 16) | 
                                 (buffer[startOffset + 8] << 8) | buffer[startOffset + 9]);
            headerOffset = 10;
        }

        byte[]? mask = null;
        if (masked)
        {
            if (headerOffset + 4 > length) return (string.Empty, 0, 0); // Not enough data
            mask = new byte[4];
            Array.Copy(buffer, startOffset + headerOffset, mask, 0, 4);
            headerOffset += 4;
        }

        int totalFrameLength = headerOffset + payloadLength;
        if (totalFrameLength > length)
        {
            // Incomplete frame
            return (string.Empty, opcode, 0);
        }

        var payload = new byte[payloadLength];
        Array.Copy(buffer, startOffset + headerOffset, payload, 0, payloadLength);

        if (masked && mask != null)
        {
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] ^= mask[i % 4];
            }
        }

        return (Encoding.UTF8.GetString(payload), opcode, totalFrameLength);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _tcpListener?.Stop();
        _cts?.Dispose();
    }
}
