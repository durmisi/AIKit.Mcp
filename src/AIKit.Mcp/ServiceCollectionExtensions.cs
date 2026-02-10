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

    /// <summary>
    /// Adds AIKit MCP services using a configuration section (e.g., appsettings.json).
    /// </summary>
    public static IServiceCollection AddAIKitMcp(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        return services.AddAIKitMcp(builder => 
            builder.WithConfiguration(configuration));
    }
}