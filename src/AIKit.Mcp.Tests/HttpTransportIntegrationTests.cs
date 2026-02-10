using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using Xunit;
using Xunit.Abstractions;
using AIKit.Mcp;

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

            mcp.Assembly = typeof(TestStartup).Assembly;
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
}