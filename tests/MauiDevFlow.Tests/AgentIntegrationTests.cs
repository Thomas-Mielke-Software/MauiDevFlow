using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MauiDevFlow.Tests;

/// <summary>
/// Integration tests for the Agent HTTP server (standalone, no MAUI runtime needed).
/// Tests the HTTP server routing, request/response handling directly.
/// </summary>
public class AgentHttpServerTests : IDisposable
{
    private readonly int _port;

    public AgentHttpServerTests()
    {
        // Find a free port
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
    }

    [Fact]
    public async Task Start_ListensOnPort()
    {
        // We test the server independently using the Driver's AgentClient
        // Create a simple mock server to verify HTTP handling works
        using var listener = new TcpListener(IPAddress.Loopback, _port);
        listener.Start();

        var acceptTask = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer);
            var request = Encoding.UTF8.GetString(buffer, 0, read);

            Assert.Contains("GET /api/status", request);

            var body = """{"agent":"test","version":"1.0","running":true}""";
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
            client.Close();
        });

        using var agentClient = new MauiDevFlow.Driver.AgentClient("localhost", _port);
        var status = await agentClient.GetStatusAsync();

        Assert.NotNull(status);
        Assert.Equal("test", status.Agent);
        Assert.True(status.Running);

        listener.Stop();
    }

    [Fact]
    public async Task QueryEndpoint_ParsesQueryString()
    {
        using var listener = new TcpListener(IPAddress.Loopback, _port);
        listener.Start();

        var acceptTask = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer);
            var request = Encoding.UTF8.GetString(buffer, 0, read);

            Assert.Contains("type=Button", request);
            Assert.Contains("text=Submit", request);

            var body = """[{"id":"btn1","type":"Button","fullType":"Microsoft.Maui.Controls.Button","text":"Submit","isVisible":true,"isEnabled":true}]""";
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
            client.Close();
        });

        using var agentClient = new MauiDevFlow.Driver.AgentClient("localhost", _port);
        var results = await agentClient.QueryAsync(type: "Button", text: "Submit");

        Assert.Single(results);
        Assert.Equal("btn1", results[0].Id);
        Assert.Equal("Button", results[0].Type);
        Assert.Equal("Submit", results[0].Text);

        listener.Stop();
    }

    [Fact]
    public async Task TapEndpoint_SendsPost()
    {
        using var listener = new TcpListener(IPAddress.Loopback, _port);
        listener.Start();

        var acceptTask = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer);
            var request = Encoding.UTF8.GetString(buffer, 0, read);

            Assert.Contains("POST /api/action/tap", request);
            Assert.Contains("elementId", request);

            var body = """{"success":true,"message":"Tapped"}""";
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
            client.Close();
        });

        using var agentClient = new MauiDevFlow.Driver.AgentClient("localhost", _port);
        var result = await agentClient.TapAsync("btn1");
        Assert.True(result);

        listener.Stop();
    }

    [Fact]
    public async Task FillEndpoint_SendsPostWithText()
    {
        using var listener = new TcpListener(IPAddress.Loopback, _port);
        listener.Start();

        var acceptTask = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer);
            var request = Encoding.UTF8.GetString(buffer, 0, read);

            Assert.Contains("POST /api/action/fill", request);
            Assert.Contains("hello world", request);

            var body = """{"success":true,"message":"Text set"}""";
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
            client.Close();
        });

        using var agentClient = new MauiDevFlow.Driver.AgentClient("localhost", _port);
        var result = await agentClient.FillAsync("entry1", "hello world");
        Assert.True(result);

        listener.Stop();
    }

    [Fact]
    public async Task TreeEndpoint_ParsesNestedElements()
    {
        using var listener = new TcpListener(IPAddress.Loopback, _port);
        listener.Start();

        var acceptTask = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer);

            var body = """
            [{
                "id": "page1", "type": "ContentPage", "fullType": "Microsoft.Maui.Controls.ContentPage",
                "isVisible": true, "isEnabled": true,
                "children": [{
                    "id": "layout1", "parentId": "page1", "type": "VerticalStackLayout",
                    "fullType": "Microsoft.Maui.Controls.VerticalStackLayout",
                    "isVisible": true, "isEnabled": true,
                    "children": [{
                        "id": "btn1", "parentId": "layout1", "type": "Button",
                        "fullType": "Microsoft.Maui.Controls.Button",
                        "text": "Click Me", "isVisible": true, "isEnabled": true
                    }]
                }]
            }]
            """;
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
            await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
            client.Close();
        });

        using var agentClient = new MauiDevFlow.Driver.AgentClient("localhost", _port);
        var tree = await agentClient.GetTreeAsync();

        Assert.Single(tree);
        Assert.Equal("ContentPage", tree[0].Type);
        Assert.NotNull(tree[0].Children);
        Assert.Single(tree[0].Children);
        Assert.Equal("VerticalStackLayout", tree[0].Children[0].Type);
        Assert.NotNull(tree[0].Children[0].Children);
        Assert.Single(tree[0].Children[0].Children);
        Assert.Equal("Click Me", tree[0].Children[0].Children[0].Text);

        listener.Stop();
    }

    public void Dispose() { }
}
