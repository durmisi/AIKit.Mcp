using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AIKit.Mcp;

public static class McpServiceExtensions 
{
    public static IMcpServerBuilder AddAIKitMcp(this IServiceCollection services, string serverName = "AIKit-Server") 
    {
        // Integration with Official SDK
        var builder = services.AddMcpServer();

        // Redirect logs to stderr to keep the stdio pipe clean for JSON-RPC
        services.AddLogging(builder => {
            builder.AddConsole(c => c.LogToStandardErrorThreshold = LogLevel.Trace);
        });

        return builder;
    }

}