using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace AIKit.Mcp;

/// <summary>
/// Builder for configuring AIKit MCP server with fluent API.
/// Wraps the official IMcpServerBuilder to provide enhanced configuration options.
/// </summary>
public class AIKitMcpBuilder
{
    private readonly IMcpServerBuilder _innerBuilder;
    private readonly IServiceCollection _services;
    private Func<McpMessageFilter>? _messageFilter;

    internal AIKitMcpBuilder(IMcpServerBuilder innerBuilder, IServiceCollection services)
    {
        _innerBuilder = innerBuilder;
        _services = services;
    }

    /// <summary>
    /// Gets the underlying IMcpServerBuilder for advanced configuration.
    /// </summary>
    public IMcpServerBuilder InnerBuilder => _innerBuilder;

    /// <summary>
    /// Gets the service collection for direct service registration.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Gets the configured message filter, if any.
    /// </summary>
    internal Func<McpMessageFilter>? MessageFilter => _messageFilter;

    /// <summary>
    /// Adds a message filter to the MCP server for processing incoming JSON-RPC messages.
    /// </summary>
    /// <param name="filter">The filter function to apply to messages.</param>
    /// <returns>The builder for chaining.</returns>
    public AIKitMcpBuilder WithMessageFilter(Func<McpMessageFilter> filter)
    {
        _messageFilter = filter;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured IMcpServerBuilder.
    /// </summary>
    public IMcpServerBuilder Build() => _innerBuilder;
}