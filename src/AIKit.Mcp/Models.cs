using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Http;
using System.Threading;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.Authentication;

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
    public AuthenticationOptions? Authentication { get; set; }

    /// <summary>
    /// Callback to configure session options per MCP session.
    /// </summary>
    public Func<HttpContext, McpServerOptions, CancellationToken, Task>? ConfigureSessionOptions { get; set; }
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
    /// JWT authority.
    /// </summary>
    public string? JwtAuthority { get; set; }

    /// <summary>
    /// JWT validation parameters.
    /// </summary>
    public Dictionary<string, object> JwtValidationParameters { get; set; } = new();
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
    /// JWT authority.
    /// </summary>
    public string? JwtAuthority { get; set; }

    /// <summary>
    /// JWT validation parameters.
    /// </summary>
    public Dictionary<string, object> JwtValidationParameters { get; set; } = new();
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
/// Configuration for tool categories used in per-session filtering.
/// </summary>
public class ToolCategoryConfig
{
    /// <summary>
    /// The category name (e.g., "math", "time").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The tool types to include in this category.
    /// </summary>
    public Type[] ToolTypes { get; set; } = Array.Empty<Type>();
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