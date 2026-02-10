using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Reflection;

namespace AIKit.Mcp;

/// <summary>
/// Supported transport types for MCP servers.
/// </summary>
public enum TransportType
{
    /// <summary>
    /// Standard input/output transport for command-line applications.
    /// </summary>
    Stdio,

    /// <summary>
    /// HTTP transport for web-based MCP servers.
    /// </summary>
    Http
}

/// <summary>
/// Options for configuring AIKit MCP server.
/// </summary>
public class McpOptions
{
    /// <summary>
    /// The name of the MCP server.
    /// </summary>
    public string? ServerName { get; set; }

    /// <summary>
    /// The version of the MCP server.
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// The transport type for the MCP server.
    /// </summary>
    public TransportType Transport { get; set; } = TransportType.Stdio;

    // Discovery Options

    /// <summary>
    /// Whether to auto-discover tools from assemblies.
    /// </summary>
    public bool AutoDiscoverTools { get; set; } = true;

    /// <summary>
    /// Whether to auto-discover resources from assemblies.
    /// </summary>
    public bool AutoDiscoverResources { get; set; } = true;

    /// <summary>
    /// Whether to auto-discover prompts from assemblies.
    /// </summary>
    public bool AutoDiscoverPrompts { get; set; } = true;

    // Feature Flags

    /// <summary>
    /// Whether to enable development features like message tracing.
    /// </summary>
    public bool EnableDevelopmentFeatures { get; set; }

    /// <summary>
    /// Whether to enable configuration validation.
    /// </summary>
    public bool EnableValidation { get; set; }

    
    /// <summary>
    /// Whether to enable progress tracking for long-running operations.
    /// </summary>
    public bool EnableProgress { get; set; }

    /// <summary>
    /// Whether to enable completion support for auto-completion.
    /// </summary>
    public bool EnableCompletion { get; set; }

    /// <summary>
    /// Whether to enable sampling support for LLM completion requests.
    /// </summary>
    public bool EnableSampling { get; set; }

    // HTTP and Authentication Options

    /// <summary>
    /// The base path for HTTP transport (when Transport is "http").
    /// </summary>
    public string? HttpBasePath { get; set; }

    /// <summary>
    /// Whether to require authentication for HTTP transport.
    /// </summary>
    public bool RequireAuthentication { get; set; }

    /// <summary>
    /// The authentication scheme to use (OAuth, JWT, Custom).
    /// </summary>
    public string? AuthenticationScheme { get; set; }

    /// <summary>
    /// OAuth 2.0 client configuration for client-side authentication.
    /// </summary>
    public OAuthOptions? OAuthOptions { get; set; }

    /// <summary>
    /// JWT Bearer token validation configuration for server-side authentication.
    /// </summary>
    public JwtOptions? JwtOptions { get; set; }

    /// <summary>
    /// Protected resource metadata for OAuth 2.0 resource server configuration.
    /// </summary>
    public ProtectedResourceMetadata? ProtectedResourceMetadata { get; set; }

    /// <summary>
    /// Custom authentication handler delegate for extensibility.
    /// </summary>
    public Func<IServiceProvider, Task>? CustomAuthHandler { get; set; }

    // Assembly

    /// <summary>
    /// The assembly to scan for MCP components. If null, uses the calling assembly.
    /// </summary>
    public Assembly? Assembly { get; set; }

    /// <summary>
    /// Optional message filter to apply to incoming JSON-RPC messages.
    /// </summary>
    public Func<McpMessageFilter>? MessageFilter { get; set; }
}

/// <summary>
/// OAuth 2.0 client configuration options.
/// </summary>
public class OAuthOptions
{
    /// <summary>
    /// The OAuth 2.0 client ID.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The OAuth 2.0 client secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// The redirect URI for the OAuth flow.
    /// </summary>
    public Uri? RedirectUri { get; set; }

    /// <summary>
    /// The OAuth authorization server URL.
    /// </summary>
    public Uri? AuthorizationServerUrl { get; set; }

    /// <summary>
    /// The scopes to request during OAuth flow.
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    /// Custom authorization redirect handler for interactive flows.
    /// </summary>
    public Func<Uri, Uri, CancellationToken, Task<string?>>? AuthorizationRedirectDelegate { get; set; }
}

/// <summary>
/// JWT Bearer token validation options.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// The issuer of the JWT tokens.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// The audience for the JWT tokens.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// The authority URL for token validation.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Additional token validation parameters.
    /// </summary>
    public Dictionary<string, object> ValidationParameters { get; set; } = new();
}

/// <summary>
/// Protected resource metadata for OAuth 2.0.
/// </summary>
public class ProtectedResourceMetadata
{
    /// <summary>
    /// The resource URI.
    /// </summary>
    public Uri? Resource { get; set; }

    /// <summary>
    /// List of authorization server URIs.
    /// </summary>
    public List<Uri> AuthorizationServers { get; set; } = new();

    /// <summary>
    /// Supported scopes for this resource.
    /// </summary>
    public List<string> ScopesSupported { get; set; } = new();

    /// <summary>
    /// Human-readable resource name.
    /// </summary>
    public string? ResourceName { get; set; }

    /// <summary>
    /// URL to resource documentation.
    /// </summary>
    public Uri? ResourceDocumentation { get; set; }
}

/// <summary>
/// Logging configuration options for MCP servers.
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// Whether to redirect logs to stderr to keep stdio clean for JSON-RPC.
    /// Defaults to true for MCP servers.
    /// </summary>
    public bool RedirectToStderr { get; set; } = true;

    /// <summary>
    /// The minimum log level to output.
    /// Defaults to Trace to capture all logs.
    /// </summary>
    public LogLevel MinLogLevel { get; set; } = LogLevel.Trace;
}