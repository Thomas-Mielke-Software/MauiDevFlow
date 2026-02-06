using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MauiDevFlow.Blazor;

Console.WriteLine("=== MAUI Blazor WebView Debug Tools - Test Console ===\n");

// Parse command line arguments
var host = "localhost";
var port = 9222;
var runServer = false;
var directWs = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i].ToLowerInvariant())
    {
        case "--host" or "-h" when i + 1 < args.Length && !args[i + 1].StartsWith("-"):
            host = args[++i];
            break;
        case "--port" or "-p" when i + 1 < args.Length:
            port = int.Parse(args[++i]);
            break;
        case "--server" or "-s":
            runServer = true;
            break;
        case "--ws" or "-w":
            directWs = true;
            break;
        case "--help":
            PrintHelp();
            return;
    }
}

Console.WriteLine($"Host: {host}");
Console.WriteLine($"Port: {port}");
Console.WriteLine();

if (runServer)
{
    await RunServerModeAsync(port);
}
else if (directWs)
{
    await RunDirectWebSocketTestAsync(host, port);
}
else
{
    await RunClientModeAsync(host, port);
}

async Task RunServerModeAsync(int serverPort)
{
    Console.WriteLine("=== Server Mode ===");
    Console.WriteLine("Starting Chobitsu WebSocket bridge...\n");
    
    using var bridge = new ChobitsuWebSocketBridge(serverPort);
    
    bridge.OnClientConnected += (id) => 
        Console.WriteLine($"[Bridge] Client connected: {id}");
    
    bridge.OnClientDisconnected += (id) => 
        Console.WriteLine($"[Bridge] Client disconnected: {id}");
    
    bridge.OnMessageFromClient += (id, message) =>
    {
        Console.WriteLine($"[Bridge] Message from {id}: {message.Substring(0, Math.Min(100, message.Length))}...");
        // In a real app, this would forward to the WebView
    };
    
    try
    {
        bridge.Start();
        
        Console.WriteLine("\nServer running. Press Enter to stop...");
        Console.WriteLine($"\nTo connect:");
        Console.WriteLine($"  - Chrome DevTools: chrome://inspect, configure localhost:{serverPort}");
        Console.WriteLine($"  - CDP endpoint: http://localhost:{serverPort}/json");
        Console.WriteLine($"  - WebSocket: ws://localhost:{serverPort}/");
        
        Console.ReadLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error starting server: {ex.Message}");
    }
    
    await bridge.StopAsync();
}

async Task RunDirectWebSocketTestAsync(string wsHost, int wsPort)
{
    Console.WriteLine("=== Direct WebSocket Test Mode ===");
    Console.WriteLine($"Connecting to ws://{wsHost}:{wsPort}/...\n");
    
    using var ws = new ClientWebSocket();
    
    try
    {
        await ws.ConnectAsync(new Uri($"ws://{wsHost}:{wsPort}/"), CancellationToken.None);
        Console.WriteLine("✓ WebSocket connected!\n");
        
        // Start receiving messages in background
        var receiveTask = Task.Run(async () =>
        {
            var buffer = new byte[16384];
            while (ws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"\n[RECV] {message}");
                        Console.Write("\nCDP Command (or 'q' to quit): ");
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[RECV ERROR] {ex.Message}");
                    break;
                }
            }
        });

        Console.WriteLine("Enter CDP commands as JSON. Examples:");
        Console.WriteLine("  {\"id\":1,\"method\":\"Runtime.evaluate\",\"params\":{\"expression\":\"1+1\"}}");
        Console.WriteLine("  {\"id\":2,\"method\":\"Page.navigate\",\"params\":{\"url\":\"https://example.com\"}}");
        Console.WriteLine("  {\"id\":3,\"method\":\"Runtime.evaluate\",\"params\":{\"expression\":\"document.title\"}}");
        Console.WriteLine("\nOr type shortcuts:");
        Console.WriteLine("  title    - Get page title");
        Console.WriteLine("  url      - Get current URL");
        Console.WriteLine("  eval <js>- Evaluate JavaScript");
        Console.WriteLine("  dom      - Get DOM info");
        Console.WriteLine("  q        - Quit\n");

        int cmdId = 1;
        while (ws.State == WebSocketState.Open)
        {
            Console.Write("CDP Command (or 'q' to quit): ");
            var input = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(input)) continue;
            if (input.ToLower() == "q") break;

            string message;
            
            // Handle shortcuts
            if (input.ToLower() == "title")
            {
                message = JsonSerializer.Serialize(new { id = cmdId++, method = "Runtime.evaluate", @params = new { expression = "document.title" } });
            }
            else if (input.ToLower() == "url")
            {
                message = JsonSerializer.Serialize(new { id = cmdId++, method = "Runtime.evaluate", @params = new { expression = "window.location.href" } });
            }
            else if (input.ToLower().StartsWith("eval "))
            {
                var js = input.Substring(5);
                message = JsonSerializer.Serialize(new { id = cmdId++, method = "Runtime.evaluate", @params = new { expression = js } });
            }
            else if (input.ToLower() == "dom")
            {
                message = JsonSerializer.Serialize(new { id = cmdId++, method = "Runtime.evaluate", @params = new { expression = "document.body.innerHTML.substring(0, 500)" } });
            }
            else
            {
                message = input; // Assume raw JSON
            }

            Console.WriteLine($"[SEND] {message}");
            var bytes = Encoding.UTF8.GetBytes(message);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ WebSocket error: {ex.Message}");
    }
}

async Task RunClientModeAsync(string clientHost, int clientPort)
{
    Console.WriteLine("=== Client Mode ===");
    Console.WriteLine($"Connecting to Chobitsu at {clientHost}:{clientPort}...\n");
    
    // First check if the endpoint is available
    Console.WriteLine("Checking CDP endpoint...");
    try
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var response = await httpClient.GetStringAsync($"http://{clientHost}:{clientPort}/json");
        Console.WriteLine($"CDP endpoint responded:\n{response}\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"CDP endpoint not available: {ex.Message}");
        Console.WriteLine("\nMake sure:");
        Console.WriteLine("1. Your MAUI app is running with Chobitsu injected");
        Console.WriteLine("2. The WebSocket bridge is started (run with --server flag)");
        Console.WriteLine("3. The port is not blocked by firewall");
        Console.WriteLine("\nSetup instructions:");
        Console.WriteLine(WebViewDriverFactory.GetSetupInstructions());
        return;
    }

    var options = new ChobitsuConnectionOptions
    {
        Host = clientHost,
        Port = clientPort,
        TimeoutSeconds = 60
    };

    try
    {
        using var driver = WebViewDriverFactory.Create(options);
        await driver.ConnectAsync();

        Console.WriteLine("✓ Successfully connected!\n");

        await RunInteractiveTestsAsync(driver);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n✗ Connection failed: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"  Inner: {ex.InnerException.Message}");
        }
        Console.WriteLine("\nTip: Try --ws flag for direct WebSocket testing without ChromeDriver.");
    }
}

async Task RunInteractiveTestsAsync(IWebViewDriver driver)
{
    while (true)
    {
        Console.WriteLine("\n--- WebView Test Menu ---");
        Console.WriteLine("1. Get page title");
        Console.WriteLine("2. Get current URL");
        Console.WriteLine("3. Execute JavaScript");
        Console.WriteLine("4. Find element by CSS selector");
        Console.WriteLine("5. Take screenshot");
        Console.WriteLine("6. Get page source (via JS)");
        Console.WriteLine("q. Quit");
        Console.Write("\nChoice: ");

        var choice = Console.ReadLine()?.Trim().ToLowerInvariant();

        try
        {
            switch (choice)
            {
                case "1":
                    var title = await driver.GetTitleAsync();
                    Console.WriteLine($"\nPage Title: {title}");
                    break;

                case "2":
                    var url = await driver.GetUrlAsync();
                    Console.WriteLine($"\nCurrent URL: {url}");
                    break;

                case "3":
                    Console.Write("Enter JavaScript to execute: ");
                    var script = Console.ReadLine();
                    if (!string.IsNullOrEmpty(script))
                    {
                        var result = await driver.ExecuteScriptAsync<object>(script);
                        Console.WriteLine($"\nResult: {result}");
                    }
                    break;

                case "4":
                    Console.Write("Enter CSS selector: ");
                    var selector = Console.ReadLine();
                    if (!string.IsNullOrEmpty(selector))
                    {
                        var elements = await driver.FindElementsAsync(selector);
                        Console.WriteLine($"\nFound {elements.Count} element(s)");
                        foreach (var el in elements.Take(5))
                        {
                            var text = el.Text;
                            Console.WriteLine($"  - {el.TagName}: {(text.Length > 50 ? text.Substring(0, 50) + "..." : text)}");
                        }
                    }
                    break;

                case "5":
                    var screenshot = await driver.TakeScreenshotAsync();
                    var filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    await File.WriteAllBytesAsync(filename, screenshot);
                    Console.WriteLine($"\nScreenshot saved to: {Path.GetFullPath(filename)}");
                    break;

                case "6":
                    var source = await driver.ExecuteScriptAsync<string>("return document.documentElement.outerHTML");
                    Console.WriteLine($"\nPage source ({source?.Length ?? 0} chars):");
                    Console.WriteLine(source?.Substring(0, Math.Min(500, source?.Length ?? 0)) + "...");
                    break;

                case "q":
                    Console.WriteLine("\nDisconnecting...");
                    await driver.DisconnectAsync();
                    Console.WriteLine("Done.");
                    return;

                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }
}

void PrintHelp()
{
    Console.WriteLine("""
        MAUI Blazor WebView Debug Tools - Console

        Usage: MauiDevFlow.Console [options]

        Modes:
          (default)      Client mode - connect via ChromeDriver (requires ChromeDriver)
          --server, -s   Server mode - run the WebSocket bridge
          --ws, -w       Direct WebSocket test mode (no ChromeDriver needed)

        Options:
          --host, -h <host>   Host to connect to (default: localhost)
          --port, -p <port>   Port number (default: 9222)
          --help              Show this help

        Examples:
          # Run in server mode (starts WebSocket bridge)
          MauiDevFlow.Console --server

          # Direct WebSocket test (recommended for debugging)
          MauiDevFlow.Console --ws --port 9222

          # Connect via ChromeDriver
          MauiDevFlow.Console --host localhost --port 9222

        Setup Instructions:
        """ + "\n" + WebViewDriverFactory.GetSetupInstructions());
}
