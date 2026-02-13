using Microsoft.AspNetCore.Server.Kestrel.Core;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.IO.Pipelines;
using System.Net;
using Xunit.Abstractions;

namespace AIKit.Mcp.Tests;

public class HttpTransportIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public HttpTransportIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Server_Starts_And_Responds_To_Initialize()
    {
        _output.WriteLine("=== Starting MCP Server Initialization Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        // Use random port to avoid conflicts in parallel tests
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
        });

        _output.WriteLine("Configuring MCP server services...");

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
            });

            mcp.AutoDiscoverPrompts = false;

            mcp.EnableCompletion = true;

            mcp.EnableDevelopmentFeatures = true;

        });

        builder.Services.AddScoped<TestTools>();
        builder.Services.AddScoped<TestResources>();

        var app = builder.Build();

        _output.WriteLine("Mapping MCP endpoint...");
        app.UseAIKitMcp("/mcp");

        _output.WriteLine("Starting MCP server...");
        await app.StartAsync();

        try
        {
            var url = app.Urls.First();
            _output.WriteLine($"Server started on URL: {url}");

            var transport = new HttpClientTransport(new()
            {
                Endpoint = new Uri($"{url}/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            });

            _output.WriteLine("Creating MCP client and connecting...");

            await using var mcpClient = await McpClient.CreateAsync(transport, new McpClientOptions
            {
                ClientInfo = new Implementation
                {
                    Name = "TestClient",
                    Version = "1.0.0"
                }
            });

            _output.WriteLine("MCP client connected successfully - initialize handshake completed");

            // Connection successful, no need to list tools as none are configured in this test

            _output.WriteLine("=== Test completed successfully ===");
        }
        finally
        {
            _output.WriteLine("Stopping MCP server...");
            await app.StopAsync();
            await app.DisposeAsync();
            _output.WriteLine("Server stopped.");
        }
    }

    [Fact]
    public async Task PerSessionToolFiltering_ByRoute_ExposesCorrectTools()
    {
        _output.WriteLine("=== Starting Per-Session Tool Filtering Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        // Use random port to avoid conflicts in parallel tests
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
        });

        builder.Services.AddScoped<TestTools>();
        builder.Services.AddScoped<TestResources>();

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";
            mcp.Assembly = typeof(TestTools).Assembly;
            mcp.WithAutoDiscovery();
            mcp.AutoDiscoverResources = true;

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
            });

            mcp.WithSessionOptions(async (httpContext, mcpOptions, cancellationToken) =>
            {
                var category = AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolCategoryFromRoute(httpContext);
                _output.WriteLine($"Session category: {category}");
                var allowedToolNames = category switch
                {
                    "math" => AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolNamesForTypes(typeof(TestTools)),
                    "interactive" => AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolNamesForTypes(typeof(TestResources)),
                    _ => AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolNamesForTypes(typeof(TestTools), typeof(TestResources))
                };
                _output.WriteLine($"Allowed tool names: {string.Join(", ", allowedToolNames)}");
                var allTools = mcpOptions.ToolCollection.ToList();
                _output.WriteLine($"All tools count: {allTools.Count}");
                var filteredTools = allTools.Where(t =>
                {
                    var protocolToolProperty = t.GetType().GetProperty("ProtocolTool");
                    if (protocolToolProperty != null)
                    {
                        var protocolTool = protocolToolProperty.GetValue(t) as ModelContextProtocol.Protocol.Tool;
                        if (protocolTool != null)
                        {
                            return allowedToolNames.Contains(protocolTool.Name);
                        }
                    }
                    return false;
                }).ToList();
                _output.WriteLine($"Filtered tools count: {filteredTools.Count}");
                var filteredNames = filteredTools.Select(t => (t.GetType().GetProperty("ProtocolTool")?.GetValue(t) as ModelContextProtocol.Protocol.Tool)?.Name).Where(n => n != null);
                _output.WriteLine($"Filtered tool names: {string.Join(", ", filteredNames)}");
                ((dynamic)mcpOptions.ToolCollection).Clear();
                foreach (var tool in filteredTools)
                {
                    ((dynamic)mcpOptions.ToolCollection).Add(tool);
                }
                _output.WriteLine($"ToolCollection after filtering: {((dynamic)mcpOptions.ToolCollection).Count}");
            });

            mcp.AutoDiscoverPrompts = false;


            mcp.WithAllFromAssembly(typeof(TestTools).Assembly);
        });

        var app = builder.Build();
        app.UseAIKitMcp("/mcp/{toolCategory?}");

        await app.StartAsync();

        try
        {
            var url = app.Urls.First();

            // Test /mcp/math route
            var mathTransport = new HttpClientTransport(new()
            {
                Endpoint = new Uri($"{url}/mcp/math"),
                TransportMode = HttpTransportMode.StreamableHttp
            });
            await using var mathClient = await McpClient.CreateAsync(mathTransport, new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "TestClient", Version = "1.0.0" }
            });
            var mathTools = await mathClient.ListToolsAsync();
            _output.WriteLine($"Math tools returned: {string.Join(", ", mathTools.Select(t => t.Name))}");
            Assert.Contains(mathTools, t => t.Name == "add-numbers");
            Assert.Contains(mathTools, t => t.Name == "get-current-time");
            Assert.DoesNotContain(mathTools, t => t.Name == "get-resource-info");

            // Test /mcp/interactive route
            var interactiveTransport = new HttpClientTransport(new()
            {
                Endpoint = new Uri($"{url}/mcp/interactive"),
                TransportMode = HttpTransportMode.StreamableHttp
            });
            await using var interactiveClient = await McpClient.CreateAsync(interactiveTransport, new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "TestClient", Version = "1.0.0" }
            });
            var interactiveTools = await interactiveClient.ListToolsAsync();
            _output.WriteLine($"Interactive tools returned: {string.Join(", ", interactiveTools.Select(t => t.Name))}");
            Assert.Contains(interactiveTools, t => t.Name == "get-resource-info");
            Assert.DoesNotContain(interactiveTools, t => t.Name == "add-numbers");

            // Test default route
            var allTransport = new HttpClientTransport(new()
            {
                Endpoint = new Uri($"{url}/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            });
            await using var allClient = await McpClient.CreateAsync(allTransport, new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "TestClient", Version = "1.0.0" }
            });
            var allTools = await allClient.ListToolsAsync();
            _output.WriteLine($"All tools returned: {string.Join(", ", allTools.Select(t => t.Name))}");
            Assert.Contains(allTools, t => t.Name == "add-numbers");
            Assert.Contains(allTools, t => t.Name == "get-current-time");
            Assert.Contains(allTools, t => t.Name == "get-resource-info");
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task PerSessionToolFiltering_SessionsAreIsolated()
    {
        _output.WriteLine("=== Starting Sessions Isolation Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        // Use random port to avoid conflicts in parallel tests
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
        });

        builder.Services.AddScoped<TestTools>();
        builder.Services.AddScoped<TestResources>();

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";
            mcp.Assembly = typeof(TestTools).Assembly;
            mcp.WithAutoDiscovery();
            mcp.AutoDiscoverResources = true;

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
            });

            mcp.WithSessionOptions(async (httpContext, mcpOptions, cancellationToken) =>
            {
                var category = AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolCategoryFromRoute(httpContext);
                _output.WriteLine($"Session category: {category}");
                var allowedToolNames = category switch
                {
                    "math" => AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolNamesForTypes(typeof(TestTools)),
                    "interactive" => AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolNamesForTypes(typeof(TestResources)),
                    _ => AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolNamesForTypes(typeof(TestTools), typeof(TestResources))
                };
                _output.WriteLine($"Allowed tool names: {string.Join(", ", allowedToolNames)}");
                var allTools = mcpOptions.ToolCollection.ToList();
                _output.WriteLine($"All tools count: {allTools.Count}");
                var filteredTools = allTools.Where(t =>
                {
                    var protocolToolProperty = t.GetType().GetProperty("ProtocolTool");
                    if (protocolToolProperty != null)
                    {
                        var protocolTool = protocolToolProperty.GetValue(t) as ModelContextProtocol.Protocol.Tool;
                        if (protocolTool != null)
                        {
                            return allowedToolNames.Contains(protocolTool.Name);
                        }
                    }
                    return false;
                }).ToList();
                _output.WriteLine($"Filtered tools count: {filteredTools.Count}");
                var filteredNames = filteredTools.Select(t => (t.GetType().GetProperty("ProtocolTool")?.GetValue(t) as ModelContextProtocol.Protocol.Tool)?.Name).Where(n => n != null);
                _output.WriteLine($"Filtered tool names: {string.Join(", ", filteredNames)}");
                ((dynamic)mcpOptions.ToolCollection).Clear();
                foreach (var tool in filteredTools)
                {
                    ((dynamic)mcpOptions.ToolCollection).Add(tool);
                }
                _output.WriteLine($"ToolCollection after filtering: {((dynamic)mcpOptions.ToolCollection).Count}");
            });

            mcp.AutoDiscoverPrompts = false;


            mcp.WithAllFromAssembly(typeof(TestTools).Assembly);
        });

        var app = builder.Build();
        app.UseAIKitMcp("/mcp/{toolCategory?}");

        await app.StartAsync();

        try
        {
            var url = app.Urls.First();

            // Test isolation by making requests to different routes with separate clients
            var mathTransport = new HttpClientTransport(new()
            {
                Endpoint = new Uri($"{url}/mcp/math"),
                TransportMode = HttpTransportMode.StreamableHttp
            });
            var interactiveTransport = new HttpClientTransport(new()
            {
                Endpoint = new Uri($"{url}/mcp/interactive"),
                TransportMode = HttpTransportMode.StreamableHttp
            });

            // Concurrent requests to test isolation
            var mathTask = McpClient.CreateAsync(mathTransport, new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "TestClient", Version = "1.0.0" }
            });
            var interactiveTask = McpClient.CreateAsync(interactiveTransport, new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "TestClient", Version = "1.0.0" }
            });

            var clients = await Task.WhenAll(mathTask, interactiveTask);
            await using var mathClient = clients[0];
            await using var interactiveClient = clients[1];

            var mathToolsTask = mathClient.ListToolsAsync();
            var interactiveToolsTask = interactiveClient.ListToolsAsync();

            var mathTools = await mathToolsTask;
            var interactiveTools = await interactiveToolsTask;

            _output.WriteLine($"Math tools: {string.Join(", ", mathTools.Select(t => t.Name))}");
            _output.WriteLine($"Interactive tools: {string.Join(", ", interactiveTools.Select(t => t.Name))}");

            // Assert isolation: math route should have only math tools
            Assert.Contains(mathTools, t => t.Name == "add-numbers");
            Assert.Contains(mathTools, t => t.Name == "get-current-time");
            Assert.DoesNotContain(mathTools, t => t.Name == "get-resource-info");

            // Interactive route should have only interactive tools
            Assert.Contains(interactiveTools, t => t.Name == "get-resource-info");
            Assert.DoesNotContain(interactiveTools, t => t.Name == "add-numbers");
            Assert.DoesNotContain(interactiveTools, t => t.Name == "get-current-time");
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task PerSessionToolFiltering_DefaultRoute_IncludesAllTools()
    {
        _output.WriteLine("=== Starting Default Route Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        // Use random port to avoid conflicts in parallel tests
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
        });

        builder.Services.AddScoped<TestTools>();
        builder.Services.AddScoped<TestResources>();

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";
            mcp.Assembly = typeof(TestTools).Assembly;
            mcp.WithAutoDiscovery();
            mcp.AutoDiscoverResources = true;

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
            });

            mcp.WithAllFromAssembly(typeof(TestTools).Assembly);

            mcp.WithSessionOptions(async (httpContext, mcpOptions, cancellationToken) =>
            {
                var category = AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolCategoryFromRoute(httpContext);
                _output.WriteLine($"Session category: {category}");
                var allowedToolNames = category switch
                {
                    "math" => AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolNamesForTypes(typeof(TestTools)),
                    _ => AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolNamesForTypes(typeof(TestTools), typeof(TestResources))
                };
                _output.WriteLine($"Allowed tool names: {string.Join(", ", allowedToolNames)}");
                var allTools = mcpOptions.ToolCollection.ToList();
                _output.WriteLine($"All tools count: {allTools.Count}");
                var filteredTools = allTools.Where(t =>
                {
                    var protocolToolProperty = t.GetType().GetProperty("ProtocolTool");
                    if (protocolToolProperty != null)
                    {
                        var protocolTool = protocolToolProperty.GetValue(t) as ModelContextProtocol.Protocol.Tool;
                        if (protocolTool != null)
                        {
                            return allowedToolNames.Contains(protocolTool.Name);
                        }
                    }
                    return false;
                }).ToList();
                _output.WriteLine($"Filtered tools count: {filteredTools.Count}");
                var filteredNames = filteredTools.Select(t => (t.GetType().GetProperty("ProtocolTool")?.GetValue(t) as ModelContextProtocol.Protocol.Tool)?.Name).Where(n => n != null);
                _output.WriteLine($"Filtered tool names: {string.Join(", ", filteredNames)}");
                ((dynamic)mcpOptions.ToolCollection).Clear();
                foreach (var tool in filteredTools)
                {
                    ((dynamic)mcpOptions.ToolCollection).Add(tool);
                }
                _output.WriteLine($"ToolCollection after filtering: {((dynamic)mcpOptions.ToolCollection).Count}");
            });
        });

        var app = builder.Build();
        app.UseAIKitMcp("/mcp/{toolCategory?}");

        await app.StartAsync();

        try
        {
            var url = app.Urls.First();
            var transport = new HttpClientTransport(new()
            {
                Endpoint = new Uri($"{url}/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            });

            await using var mcpClient = await McpClient.CreateAsync(transport, new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "TestClient", Version = "1.0.0" }
            });

            // Test default route includes all
            var allTools = await mcpClient.ListToolsAsync();
            _output.WriteLine($"All tools returned: {string.Join(", ", allTools.Select(t => t.Name))}");
            Assert.True(allTools.Count >= 2); // At least from both types
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Server_With_Stream_Transport_Works_In_Memory()
    {
        _output.WriteLine("=== Starting MCP Server Stream Transport Test ===");

        // Create pipes for in-memory communication
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();

        // Server streams
        var serverInput = clientToServerPipe.Reader.AsStream();
        var serverOutput = serverToClientPipe.Writer.AsStream();

        // Client streams
        var clientInput = serverToClientPipe.Reader.AsStream();
        var clientOutput = clientToServerPipe.Writer.AsStream();

        // Create server manually with stream transport
        var server = McpServer.Create(
            new StreamServerTransport(serverInput, serverOutput),
            new McpServerOptions()
            {
                ServerInfo = new() { Name = "TestServer", Version = "1.0" },
                ToolCollection = [McpServerTool.Create((string arg) => $"Echo: {arg}", new() { Name = "Echo" })]
            });

        // Start the server
        _ = server.RunAsync();

        // Create client with the other end of the pipe
        var transport = new StreamClientTransport(clientOutput, clientInput);

        await using var mcpClient = await McpClient.CreateAsync(transport, new McpClientOptions
        {
            ClientInfo = new Implementation
            {
                Name = "TestClient",
                Version = "1.0.0"
            }
        });

        _output.WriteLine("MCP client connected successfully");

        // Test tools
        var tools = await mcpClient.ListToolsAsync();
        _output.WriteLine($"Tools: {string.Join(", ", tools.Select(t => t.Name))}");

        Assert.NotEmpty(tools);

        // Test invoking a tool
        var echoTool = tools.FirstOrDefault(t => t.Name == "Echo");
        if (echoTool != null)
        {
            _output.WriteLine("Invoking Echo tool with argument 'Hello World'...");
            var result = await echoTool.InvokeAsync(new() { ["arg"] = "Hello World" });
            _output.WriteLine($"Tool result: {result}");
        }
        else
        {
            _output.WriteLine("Echo tool not found");
        }
    }
}
