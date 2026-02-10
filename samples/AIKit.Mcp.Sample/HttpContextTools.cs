using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace AIKit.Mcp.Sample;

/// <summary>
/// Example tool class demonstrating HTTP context access in ASP.NET Core MCP servers.
/// This tool can access request metadata, authentication information, and HTTP-specific data.
/// Only available when using HTTP transport.
/// </summary>
[McpServerToolType]
public class HttpContextTools
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTools(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets information about the current HTTP request.
    /// </summary>
    /// <returns>Information about the current request including method, path, and user agent.</returns>
    [McpServerTool(Name = "get_request_info")]
    public string GetRequestInfo()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return "No HTTP context available. This tool only works with HTTP transport.";
        }

        var request = context.Request;
        var user = context.User;

        return $"Request Info:\n" +
               $"- Method: {request.Method}\n" +
               $"- Path: {request.Path}\n" +
               $"- Query: {request.QueryString}\n" +
               $"- User Agent: {request.Headers.UserAgent}\n" +
               $"- Content Type: {request.ContentType}\n" +
               $"- Is Authenticated: {user?.Identity?.IsAuthenticated ?? false}\n" +
               $"- User Name: {user?.Identity?.Name ?? "Anonymous"}";
    }

    /// <summary>
    /// Gets the client's IP address and connection information.
    /// </summary>
    /// <returns>Client connection details including IP address and protocol.</returns>
    [McpServerTool(Name = "get_client_info")]
    public string GetClientInfo()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return "No HTTP context available. This tool only works with HTTP transport.";
        }

        var connection = context.Connection;
        var request = context.Request;

        return $"Client Info:\n" +
               $"- Remote IP: {connection.RemoteIpAddress}\n" +
               $"- Remote Port: {connection.RemotePort}\n" +
               $"- Local IP: {connection.LocalIpAddress}\n" +
               $"- Local Port: {connection.LocalPort}\n" +
               $"- Protocol: {request.Protocol}\n" +
               $"- Scheme: {request.Scheme}\n" +
               $"- Host: {request.Host}";
    }

    /// <summary>
    /// Lists all request headers.
    /// </summary>
    /// <returns>A formatted list of all HTTP request headers.</returns>
    [McpServerTool(Name = "list_headers")]
    public string ListHeaders()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            return "No HTTP context available. This tool only works with HTTP transport.";
        }

        var headers = context.Request.Headers;
        if (!headers.Any())
        {
            return "No headers found.";
        }

        var result = "Request Headers:\n";
        foreach (var header in headers.OrderBy(h => h.Key))
        {
            result += $"- {header.Key}: {string.Join(", ", header.Value)}\n";
        }

        return result.TrimEnd();
    }
}