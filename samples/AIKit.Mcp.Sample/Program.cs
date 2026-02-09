// See https://aka.ms/new-console-template for more information
using AIKit.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// Add console logging for startup messages
builder.Logging.AddConsole();

// Add AIKit MCP with STDIO transport
builder.Services.AddAIKitMcp()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();  // Automatically discover tools, resources, and prompts from assembly

// Register business logic classes (services will be resolved via DI)
builder.Services.AddScoped<MyTools>();
builder.Services.AddScoped<MyResources>();
builder.Services.AddScoped<MyPrompts>();

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("AIKit MCP Server is starting...");

await host.RunAsync();

// Example tool class
[McpServerToolType]
public class MyTools
{
    [McpServerTool(Name = "echo")]
    public string Echo(string message) => $"Echo: {message}";

    [McpServerTool]
    public int Add(int a, int b) => a + b;
}

// Example resource class
[McpServerToolType]
public class MyResources
{
    [McpServerResource(UriTemplate = "file://config", Name = "Config File")]
    public string GetConfig() => "key=value";
}

// Example prompt class
[McpServerToolType]
public class MyPrompts
{
    [McpServerPrompt(Name = "greeting")]
    public string GetGreeting(string name) => $"Hello {name}!";
}
