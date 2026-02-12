using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

namespace AIKit.Mcp;

/// <summary>
/// Extension methods for WebApplication to add MCP endpoints.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Maps MCP server endpoints with authentication and authorization setup if configured.
    /// </summary>
    /// <param name="app">The WebApplication instance.</param>
    /// <param name="path">The route pattern for MCP endpoints. Defaults to "/mcp" if null.</param>
    /// <returns>An IEndpointConventionBuilder for further configuration.</returns>
    public static IEndpointConventionBuilder UseAIKitMcp(this WebApplication app, string? path = null)
    {
        string pattern = path ?? "/mcp";
        var hasAuth = app.Services.GetService<IAuthenticationHandlerProvider>() != null;
        if (hasAuth)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            return app.MapMcp(pattern).RequireAuthorization();
        }
        else
        {
            return app.MapMcp(pattern);
        }
    }
}