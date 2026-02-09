using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AIKit.Mcp.Sample;

/// <summary>
/// Example resource class for file system access
/// </summary>
[McpServerToolType]
public class FileSystemResources
{
    private readonly ILogger<FileSystemResources> _logger;

    public FileSystemResources(ILogger<FileSystemResources> logger)
    {
        _logger = logger;
    }

    [McpServerResource(UriTemplate = "file://sample/info", Name = "Sample Server Info")]
    public string GetServerInfo()
    {
        var info = new
        {
            Name = "AIKit Sample MCP Server",
            Version = "1.0.0",
            Description = "A sample MCP server demonstrating AIKit.Mcp capabilities",
            Features = new[] { "Math tools", "File system resources", "Conversation prompts" },
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("Providing server info to client");
        return System.Text.Json.JsonSerializer.Serialize(info, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerResource(UriTemplate = "file://sample/capabilities", Name = "Server Capabilities")]
    public string GetCapabilities()
    {
        var capabilities = new
        {
            Tools = new[]
            {
                new { Name = "add", Description = "Basic arithmetic addition" },
                new { Name = "multiply", Description = "Basic arithmetic multiplication" },
                new { Name = "power", Description = "Exponentiation operations" },
                new { Name = "fibonacci", Description = "Fibonacci sequence generation" }
            },
            Resources = new[]
            {
                new { Uri = "file://sample/info", Description = "Server information" },
                new { Uri = "file://sample/capabilities", Description = "Server capabilities" }
            },
            Prompts = new[]
            {
                new { Name = "greeting", Description = "Generate personalized greetings" },
                new { Name = "help", Description = "Get help and usage instructions" },
                new { Name = "math_explanation", Description = "Explain mathematical concepts" }
            }
        };

        _logger.LogInformation("Providing capabilities info to client");
        return System.Text.Json.JsonSerializer.Serialize(capabilities, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}