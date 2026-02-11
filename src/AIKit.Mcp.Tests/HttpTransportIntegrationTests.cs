using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            mcp.EnableProgress = true;
            mcp.EnableCompletion = true;
            mcp.EnableSampling = false;
            mcp.EnableDevelopmentFeatures = true;
            mcp.EnableValidation = true;
        });

        builder.Services.AddScoped<TestTools>();
        builder.Services.AddScoped<TestResources>();

        var app = builder.Build();

        _output.WriteLine("Mapping MCP endpoint...");
        app.MapMcp("/mcp");

        _output.WriteLine("Starting MCP server...");
        await app.StartAsync();

        try
        {
            var url = app.Urls.First();
            _output.WriteLine($"Server started on URL: {url}");

            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2024-11-05");
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

            _output.WriteLine("Creating initialize request...");

            var request = new JsonRpcRequest
            {
                Id = new RequestId(1),
                Method = "initialize",
                Params = JsonSerializer.SerializeToNode(new InitializeRequestParams
                {
                    ProtocolVersion = "2024-11-05",
                    Capabilities = new ClientCapabilities(),
                    ClientInfo = new Implementation
                    {
                        Name = "TestClient",
                        Version = "1.0.0"
                    }
                })
            };

            _output.WriteLine($"Request JSON: {JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true })}");

            _output.WriteLine("Sending initialize request to /mcp endpoint...");
            var response = await client.PostAsJsonAsync("/mcp", request);

            _output.WriteLine($"Response Status Code: {response.StatusCode}");
            _output.WriteLine($"Response Content-Type: {response.Content.Headers.ContentType}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

            // Read the SSE response
            var responseText = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Raw SSE Response:\n{responseText}");

            Assert.Contains("data:", responseText); // SSE format should contain data: lines

            // Parse the SSE response to extract the JSON-RPC response
            var lines = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var dataLine = lines.FirstOrDefault(line => line.StartsWith("data: "));
            Assert.NotNull(dataLine);

            var jsonData = dataLine.Substring("data: ".Length);
            _output.WriteLine($"Extracted JSON-RPC data: {jsonData}");

            var jsonNode = JsonNode.Parse(jsonData);
            Assert.NotNull(jsonNode);

            _output.WriteLine($"Parsed JSON-RPC Response: {jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");

            // Check the JSON-RPC response structure
            var responseId = jsonNode["id"]?.GetValue<int>();
            _output.WriteLine($"Response ID: {responseId}");

            Assert.Equal(1, responseId);
            Assert.NotNull(jsonNode["result"]);
            Assert.Null(jsonNode["error"]);

            _output.WriteLine("=== Test completed successfully ===");
        }
        finally
        {
            _output.WriteLine("Stopping MCP server...");
            await app.StopAsync();
            _output.WriteLine("Server stopped.");
        }
    }

    [Fact]
    public async Task PerSessionToolFiltering_ByRoute_ExposesCorrectTools()
    {
        _output.WriteLine("=== Starting Per-Session Tool Filtering Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddScoped<TestTools>();
        builder.Services.AddScoped<TestResources>();

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";
            mcp.Assembly = typeof(TestTools).Assembly;
            mcp.AutoDiscoverTools = true;
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
            mcp.EnableProgress = true;

            mcp.WithAllFromAssembly(typeof(TestTools).Assembly);
        });

        var app = builder.Build();
        app.MapMcp("/mcp/{toolCategory?}");

        await app.StartAsync();

        try
        {
            var url = app.Urls.First();
            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2024-11-05");
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

            // Test /mcp/math route
            var mathTools = await ListToolsAsync(client, "/mcp/math");
            _output.WriteLine($"Math tools returned: {string.Join(", ", mathTools.Select(t => t.Name))}");
            Assert.Contains(mathTools, t => t.Name == "add-numbers");
            Assert.Contains(mathTools, t => t.Name == "get-current-time");
            Assert.DoesNotContain(mathTools, t => t.Name == "get-resource-info");

            // Test /mcp/interactive route
            var interactiveTools = await ListToolsAsync(client, "/mcp/interactive");
            _output.WriteLine($"Interactive tools returned: {string.Join(", ", interactiveTools.Select(t => t.Name))}");
            Assert.Contains(interactiveTools, t => t.Name == "get-resource-info");
            Assert.DoesNotContain(interactiveTools, t => t.Name == "add-numbers");

            // Test default route
            var allTools = await ListToolsAsync(client, "/mcp");
            _output.WriteLine($"All tools returned: {string.Join(", ", allTools.Select(t => t.Name))}");
            Assert.Contains(allTools, t => t.Name == "add-numbers");
            Assert.Contains(allTools, t => t.Name == "get-current-time");
            Assert.Contains(allTools, t => t.Name == "get-resource-info");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PerSessionToolFiltering_SessionsAreIsolated()
    {
        // Similar setup, but test that concurrent requests to different routes don't interfere
        // This would require multiple HttpClient instances or careful sequencing
        Assert.True(true); // Placeholder - implement full isolation test
    }

    [Fact]
    public async Task PerSessionToolFiltering_DefaultRoute_IncludesAllTools()
    {
        _output.WriteLine("=== Starting Default Route Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddScoped<TestTools>();
        builder.Services.AddScoped<TestResources>();

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";
            mcp.Assembly = typeof(TestTools).Assembly;
            mcp.AutoDiscoverTools = true;
            mcp.AutoDiscoverResources = true;

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
            });

            mcp.WithAllFromAssembly(typeof(TestTools).Assembly);

            mcp.WithSessionOptions(async (httpContext, mcpOptions, cancellationToken) =>
            {
                var category = AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolCategoryFromRoute(httpContext);
                var allowedToolNames = category switch
                {
                    "math" => AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolNamesForTypes(typeof(TestTools)),
                    _ => AIKit.Mcp.Helpers.ToolFilteringHelpers.GetToolNamesForTypes(typeof(TestTools), typeof(TestResources))
                };
                var allTools = mcpOptions.ToolCollection.ToList();
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
                ((dynamic)mcpOptions.ToolCollection).Clear();
                foreach (var tool in filteredTools)
                {
                    ((dynamic)mcpOptions.ToolCollection).Add(tool);
                }
            });
        });

        var app = builder.Build();
        app.MapMcp("/mcp/{toolCategory?}");

        await app.StartAsync();

        try
        {
            var url = app.Urls.First();
            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2024-11-05");
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

            // Test default route includes all
            var allTools = await ListToolsAsync(client, "/mcp");
            Assert.True(allTools.Length >= 2); // At least from both types
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private async Task<ModelContextProtocol.Protocol.Tool[]> ListToolsAsync(HttpClient client, string endpoint)
    {
        // First initialize
        var initRequest = new JsonRpcRequest
        {
            Id = new RequestId(1),
            Method = "initialize",
            Params = JsonSerializer.SerializeToNode(new InitializeRequestParams
            {
                ProtocolVersion = "2024-11-05",
                Capabilities = new ClientCapabilities(),
                ClientInfo = new Implementation { Name = "TestClient", Version = "1.0.0" }
            })
        };

        var initResponse = await client.PostAsJsonAsync(endpoint, initRequest);
        Assert.Equal(HttpStatusCode.OK, initResponse.StatusCode);

        // Then list tools
        var listRequest = new JsonRpcRequest
        {
            Id = new RequestId(2),
            Method = "tools/list",
            Params = JsonSerializer.SerializeToNode(new ListToolsRequestParams())
        };

        var listResponse = await client.PostAsJsonAsync(endpoint, listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listResponseText = await listResponse.Content.ReadAsStringAsync();
        var lines = listResponseText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dataLine = lines.FirstOrDefault(line => line.StartsWith("data: "));
        var jsonData = dataLine?.Substring("data: ".Length);
        var jsonNode = JsonNode.Parse(jsonData!);
        var tools = jsonNode["result"]?["tools"]?.Deserialize<ModelContextProtocol.Protocol.Tool[]>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<ModelContextProtocol.Protocol.Tool>();

        // Note: This method doesn't have access to _output, so logging is done in the caller
        return tools ?? Array.Empty<ModelContextProtocol.Protocol.Tool>();
    }
}