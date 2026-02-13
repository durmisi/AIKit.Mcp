using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;

namespace AIKit.Mcp;

/// <summary>
/// Specifies the type of task store to use.
/// </summary>
public enum TaskStoreType
{
    /// <summary>
    /// Uses an in-memory task store (default).
    /// </summary>
    InMemory = 0,

    /// <summary>
    /// Uses a file-based task store.
    /// </summary>
    FileBased = 1
}

/// <summary>
/// Configuration options for HTTP transport.
/// </summary>
public class HttpTransportOptions
{
    /// <summary>
    /// The base path for HTTP transport endpoints.
    /// </summary>
    public string? HttpBasePath { get; set; }

    /// <summary>
    /// Authentication configuration.
    /// </summary>
    public AuthenticationOptions? AuthOptions { get; set; }

    /// <summary>
    /// Callback to configure session options per MCP session.
    /// </summary>
    public Func<HttpContext, McpServerOptions, CancellationToken, Task>? ConfigureSessionOptions { get; set; }

    /// <summary>
    /// Configures OAuth 2.0 authentication.
    /// </summary>
    /// <param name="configure">Action to configure OAuth options.</param>
    /// <returns>The current options instance for chaining.</returns>
    public HttpTransportOptions WithOAuth(Action<OAuthAuth> configure)
    {
        var oauth = new OAuthAuth();
        configure(oauth);
        AuthOptions = oauth;
        return this;
    }

    /// <summary>
    /// Configures JWT Bearer authentication.
    /// </summary>
    /// <param name="configure">Action to configure JWT options.</param>
    /// <returns>The current options instance for chaining.</returns>
    public HttpTransportOptions WithJwtAuth(Action<JwtAuth> configure)
    {
        var jwt = new JwtAuth();
        configure(jwt);
        AuthOptions = jwt;
        return this;
    }

    /// <summary>
    /// Configures custom authentication.
    /// </summary>
    /// <param name="configure">Action to configure custom auth options.</param>
    /// <returns>The current options instance for chaining.</returns>
    public HttpTransportOptions WithCustomAuth(Action<CustomAuth> configure)
    {
        var custom = new CustomAuth();
        configure(custom);
        AuthOptions = custom;
        return this;
    }

    /// <summary>
    /// Configures MCP authentication with JWT Bearer and resource metadata.
    /// </summary>
    /// <param name="configure">Action to configure MCP auth options.</param>
    /// <returns>The current options instance for chaining.</returns>
    public HttpTransportOptions WithMcpAuth(Action<McpAuth> configure)
    {
        var mcp = new McpAuth();
        configure(mcp);
        AuthOptions = mcp;
        return this;
    }
}

/// <summary>
/// Base class for authentication options.
/// </summary>
public abstract class AuthenticationOptions
{
    /// <summary>
    /// The authentication scheme to use (e.g., "Bearer", "oauth", "jwt").
    /// </summary>
    public string? AuthenticationScheme { get; set; }
}

/// <summary>
/// OAuth 2.0 authentication options.
/// </summary>
public class OAuthAuth : AuthenticationOptions
{
    /// <summary>
    /// OAuth 2.0 client ID.
    /// </summary>
    public string? OAuthClientId { get; set; }

    /// <summary>
    /// OAuth 2.0 client secret.
    /// </summary>
    public string? OAuthClientSecret { get; set; }

    /// <summary>
    /// OAuth 2.0 redirect URI.
    /// </summary>
    public Uri? OAuthRedirectUri { get; set; }

    /// <summary>
    /// OAuth 2.0 authorization server URL.
    /// </summary>
    public Uri? OAuthAuthorizationServerUrl { get; set; }

    /// <summary>
    /// OAuth 2.0 scopes.
    /// </summary>
    public List<string> OAuthScopes { get; set; } = new();

    /// <summary>
    /// OAuth 2.0 authorization redirect delegate.
    /// </summary>
    public Func<Uri, Uri, CancellationToken, Task<string?>>? OAuthAuthorizationRedirectDelegate { get; set; }

    /// <summary>
    /// Protected resource URI.
    /// </summary>
    public Uri? ProtectedResource { get; set; }

    /// <summary>
    /// Protected authorization servers.
    /// </summary>
    public List<Uri> ProtectedAuthorizationServers { get; set; } = new();

    /// <summary>
    /// Protected scopes supported.
    /// </summary>
    public List<string> ProtectedScopesSupported { get; set; } = new();

    /// <summary>
    /// Protected resource name.
    /// </summary>
    public string? ProtectedResourceName { get; set; }

    /// <summary>
    /// Protected resource documentation.
    /// </summary>
    public Uri? ProtectedResourceDocumentation { get; set; }

    // JWT properties for OAuth (since OAuth uses JWT Bearer)
    /// <summary>
    /// JWT issuer.
    /// </summary>
    public string? JwtIssuer { get; set; }

    /// <summary>
    /// JWT audience.
    /// </summary>
    public string? JwtAudience { get; set; }

    /// <summary>
    /// Authority.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Optional token validation parameters for JWT validation.
    /// </summary>
    public TokenValidationParameters? TokenValidationParameters { get; set; }

/// <summary>
///  Optional JWT Bearer events for handling authentication events during OAuth token validation.
/// </summary>
    public JwtBearerEvents? Events { get; set; }
}

/// <summary>
/// JWT authentication options.
/// </summary>
public class JwtAuth : AuthenticationOptions
{
    /// <summary>
    /// JWT issuer.
    /// </summary>
    public string? JwtIssuer { get; set; }

    /// <summary>
    /// JWT audience.
    /// </summary>
    public string? JwtAudience { get; set; }

    /// <summary>
    /// Authority.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// JWT signing key (for symmetric key validation, when not using authority).
    /// </summary>
    public string? SigningKey { get; set; }

    /// <summary>
    /// Optional token validation parameters for JWT validation.
    /// </summary>
    public TokenValidationParameters? TokenValidationParameters { get; set; }

/// <summary>
///  Optional JWT Bearer events for handling authentication events during OAuth token validation.
/// </summary>
    public JwtBearerEvents? Events { get; set; }
}

/// <summary>
/// Custom authentication options.
/// </summary>
public class CustomAuth : AuthenticationOptions
{
    /// <summary>
    /// The name of the authentication scheme. Defaults to "Custom".
    /// </summary>
    public string SchemeName { get; set; } = "Custom";

    /// <summary>
    /// Action to register the custom authentication scheme with the authentication builder.
    /// The consumer should use this to call AddScheme with the appropriate generic types.
    /// </summary>
    public Action<Microsoft.AspNetCore.Authentication.AuthenticationBuilder>? RegisterScheme { get; set; }
}

/// <summary>
/// MCP authentication options with JWT Bearer and resource metadata.
/// </summary>
public class McpAuth : AuthenticationOptions
{
    /// <summary>
    /// JWT issuer.
    /// </summary>
    public string? JwtIssuer { get; set; }

    /// <summary>
    /// JWT audience.
    /// </summary>
    public string? JwtAudience { get; set; }

    /// <summary>
    /// Authority for JWT token validation.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Optional token validation parameters for JWT validation.
    /// </summary>
    public TokenValidationParameters? TokenValidationParameters { get; set; }

    /// <summary>
    /// Optional JWT Bearer events for handling authentication events.
    /// </summary>
    public JwtBearerEvents? Events { get; set; }

    /// <summary>
    /// Resource metadata for OAuth authorization.
    /// </summary>
    public ModelContextProtocol.Authentication.ProtectedResourceMetadata? ResourceMetadata { get; set; }
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

/// <summary>
/// OpenTelemetry configuration options for observability.
/// </summary>
public class OpenTelemetryOptions
{
    /// <summary>
    /// Whether to enable OpenTelemetry tracing.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Whether to enable OpenTelemetry metrics.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Whether to enable OpenTelemetry logging.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// OTLP exporter endpoint. If null, uses default.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Custom configurator for OTLP exporter. If set, overrides default endpoint configuration.
    /// </summary>
    public Action<OpenTelemetry.Exporter.OtlpExporterOptions>? OtlpExporterConfigurator { get; set; }

    /// <summary>
    /// Service name for OpenTelemetry resource.
    /// </summary>
    public string ServiceName { get; set; } = "AIKit.Mcp.Server";

    /// <summary>
    /// Service version for OpenTelemetry resource.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";
}

/// <summary>
/// Configuration options for file-based task store.
/// </summary>
public class FileBasedTaskStoreOptions
{
    /// <summary>
    /// The directory path where task files are stored.
    /// Defaults to a subdirectory named "tasks" in the current working directory.
    /// </summary>
    public string StoragePath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "tasks");

    /// <summary>
    /// The default time-to-live for tasks.
    /// Defaults to 1 hour.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether to isolate tasks by session.
    /// Defaults to true.
    /// </summary>
    public bool EnableSessionIsolation { get; set; } = true;

    /// <summary>
    /// The file extension for task files.
    /// Defaults to ".json".
    /// </summary>
    public string FileExtension { get; set; } = ".json";
}