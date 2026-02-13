using Microsoft.Extensions.DependencyInjection;
using AIKit.Mcp;
using AIKit.Mcp.PerSessionTools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.IO.Pipelines;

// Create in-memory pipes for client-server communication
var clientToServerPipe = new Pipe();
var serverToClientPipe = new Pipe();

// Set up DI container with AIKit.Mcp
var services = new ServiceCollection();
services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "AIKit.Mcp.Sample";
    mcp.ServerVersion = "1.0.0";
    mcp.WithStreamTransport(clientToServerPipe.Reader.AsStream(), serverToClientPipe.Writer.AsStream());
    mcp.WithTools<EchoTool>();
});

var serviceProvider = services.BuildServiceProvider();

// Get the MCP server from DI
var server = serviceProvider.GetRequiredService<McpServer>();

// Start the server
_ = server.RunAsync();

// Create the client
await using var client = await McpClient.CreateAsync(
    new StreamClientTransport(clientToServerPipe.Writer.AsStream(), serverToClientPipe.Reader.AsStream()));

Console.WriteLine("Client connected. Testing tools...");

// List tools
var tools = await client.ListToolsAsync();
Console.WriteLine($"Available tools: {string.Join(", ", tools.Select(t => t.Name))}");

// Call the echo tool
var result = await client.CallToolAsync("echo", new Dictionary<string, object?> { ["message"] = "Hello from AIKit.Mcp!" });
Console.WriteLine($"Tool result: {result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text}");

Console.WriteLine("Test completed successfully.");