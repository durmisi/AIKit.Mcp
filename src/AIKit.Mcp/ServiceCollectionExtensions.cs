using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace AIKit.Mcp;

public static class AIKitMcpServiceCollectionExtensions
{
    /// <summary>
    /// Adds AIKit MCP services with a fluent builder.
    /// </summary>
    public static IServiceCollection AddAIKitMcp(
        this IServiceCollection services, 
        Action<AIKitMcpBuilder> configure)
    {
        var builder = new AIKitMcpBuilder(services);
        
        configure(builder);

        builder.Build();

        return services;
    }
}