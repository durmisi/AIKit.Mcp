using Microsoft.Extensions.DependencyInjection;

namespace AIKit.Mcp;

/// <summary>
/// Extension methods for IServiceCollection to add AIKit MCP services.
/// </summary>
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