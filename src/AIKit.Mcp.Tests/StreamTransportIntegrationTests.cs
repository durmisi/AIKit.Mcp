using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.IO.Pipelines;
using Xunit.Abstractions;

namespace AIKit.Mcp.Tests;

[Collection("Integration")]
public class StreamTransportIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public StreamTransportIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Server_Starts_And_Responds_To_Initialize_Stream()
    {
        _output.WriteLine("=== Starting MCP Server Stream Transport Initialization Test ===");

        // Create pipes for in-memory communication
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();

        var services = new ServiceCollection();
        services.AddHttpContextAccessor();

        _output.WriteLine("Configuring MCP server services...");

        services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";

            mcp.WithStreamTransport(clientToServerPipe.Reader.AsStream(), serverToClientPipe.Writer.AsStream());

            mcp.Assembly = typeof(StreamTransportIntegrationTests).Assembly;
            mcp.WithAutoDiscovery();

            mcp.EnableCompletion = true;

            mcp.EnableDevelopmentFeatures = true;
        });

        var provider = services.BuildServiceProvider();

        _output.WriteLine("Getting server from services...");
        var server = provider.GetRequiredService<McpServer>();

        using var cts = new CancellationTokenSource();

        _output.WriteLine("Starting server...");
        var serverTask = server.RunAsync(cts.Token);

        // Give server time to start
        await Task.Delay(100);

        _output.WriteLine("Creating client...");
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServerPipe.Writer.AsStream(), serverToClientPipe.Reader.AsStream()));

        _output.WriteLine("Listing tools...");
        var tools = await client.ListToolsAsync();

        Assert.NotEmpty(tools);
        foreach (var tool in tools)
        {
            _output.WriteLine($"Tool: {tool.Name}");
        }

        // Test tool invocation if available
        var calculatorTool = tools.FirstOrDefault(t => t.Name.Contains("Calculator"));
        if (calculatorTool != null)
        {
            _output.WriteLine("Invoking calculator tool...");
            var result = await calculatorTool.InvokeAsync(new() { ["expression"] = "2 + 3" });
            Assert.NotNull(result);
            _output.WriteLine($"Result: {result}");
        }

        _output.WriteLine("Shutting down server...");
        cts.Cancel();

        await serverTask;

        _output.WriteLine("Test completed successfully.");
    }
}