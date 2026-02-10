using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
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

public sealed class AIKitMcpBuilder
{
    public IServiceCollection Services { get; }
    
    public IMcpServerBuilder McpServerBuilder { get; }
    
    // Server metadata
    public string? ServerName { get; set; }
    public string? ServerVersion { get; set; }
    
    // Transport
    public TransportType Transport { get; set; } = TransportType.Stdio;
    public string? HttpBasePath { get; set; }
    
    // Discovery
    public bool AutoDiscoverTools { get; set; } = true;
    public bool AutoDiscoverResources { get; set; } = true;
    public bool AutoDiscoverPrompts { get; set; } = true;
    public Assembly? Assembly { get; set; }
    
    // Features
    public bool EnableDevelopmentFeatures { get; set; }
    public bool EnableValidation { get; set; }
    public bool EnableProgress { get; set; }
    public bool EnableCompletion { get; set; }
    public bool EnableSampling { get; set; }
    
    // Authentication
    public bool RequireAuthentication { get; set; }
    public string? AuthenticationScheme { get; set; }
    
    // OAuth options
    public string? OAuthClientId { get; set; }
    public string? OAuthClientSecret { get; set; }
    public Uri? OAuthRedirectUri { get; set; }
    public Uri? OAuthAuthorizationServerUrl { get; set; }
    public List<string> OAuthScopes { get; set; } = new();
    public Func<Uri, Uri, CancellationToken, Task<string?>>? OAuthAuthorizationRedirectDelegate { get; set; }
    
    // JWT options
    public string? JwtIssuer { get; set; }
    public string? JwtAudience { get; set; }
    public string? JwtAuthority { get; set; }
    public Dictionary<string, object> JwtValidationParameters { get; set; } = new();
    
    // Protected resource metadata
    public Uri? ProtectedResource { get; set; }
    public List<Uri> ProtectedAuthorizationServers { get; set; } = new();
    public List<string> ProtectedScopesSupported { get; set; } = new();
    public string? ProtectedResourceName { get; set; }
    public Uri? ProtectedResourceDocumentation { get; set; }
    
    // Custom
    public Func<IServiceProvider, Task>? CustomAuthHandler { get; set; }
    public Func<McpMessageFilter>? MessageFilter { get; set; }

    public AIKitMcpBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        McpServerBuilder = services.AddMcpServer();
    }

    /// <summary>
    /// Finalizes the configuration and returns the SDK builder.
    /// </summary>
    public IMcpServerBuilder Build()
    {
        ApplyOptionsToSdk();
        return McpServerBuilder;
    }


    /// <summary>
    /// Sets up default logging to stderr.
    /// </summary>
    public AIKitMcpBuilder WithLogging(Action<LoggingOptions>? configure = null)
    {
        var logOptions = new LoggingOptions();
        configure?.Invoke(logOptions);

        Services.AddLogging(logging =>
        {
            logging.AddConsole(c =>
            {
                if (logOptions.RedirectToStderr)
                    c.LogToStandardErrorThreshold = logOptions.MinLogLevel;
            });
            logging.SetMinimumLevel(logOptions.MinLogLevel);
        });

        return this;
    }

    /// <summary>
    /// Registers a custom Task Store implementation.
    /// </summary>
    public AIKitMcpBuilder WithTaskStore<TTaskStore>() where TTaskStore : class, 
        IMcpTaskStore
    {
        Services.AddSingleton<IMcpTaskStore, TTaskStore>();
        return this;
    }

    /// <summary>
    /// Discovers tools, resources, and prompts from the calling assembly.
    /// </summary>
    public AIKitMcpBuilder WithAllFromAssembly(Assembly? assembly = null)
    {
        var target = assembly ?? Assembly.GetCallingAssembly();
        McpServerBuilder.WithToolsFromAssembly(target)
                        .WithResourcesFromAssembly(target)
                        .WithPromptsFromAssembly(target);
        return this;
    }

    /// <summary>
    /// Orchestrates the transition from AIKit Options to the underlying MCP SDK.
    /// </summary>
    private void ApplyOptionsToSdk()
    {
        ConfigureMetadata();
        ConfigureTransport();
        ConfigureAuthentication();
        ConfigureDiscovery();
        ConfigureFeatures();
        ConfigureDiagnostics();
    }

    private void ConfigureMetadata()
    {
        if (string.IsNullOrEmpty(this.ServerName) && string.IsNullOrEmpty(this.ServerVersion)) return;

        Services.Configure<McpServerOptions>(opt =>
        {
            if (opt.ServerInfo == null) return;
            if (!string.IsNullOrEmpty(this.ServerName)) opt.ServerInfo.Name = this.ServerName;
            if (!string.IsNullOrEmpty(this.ServerVersion)) opt.ServerInfo.Version = this.ServerVersion;
        });
    }

    private void ConfigureTransport()
    {
        if (this.Transport == TransportType.Http)
            McpServerBuilder.WithHttpTransport();
        else
            McpServerBuilder.WithStdioServerTransport();
    }

    private void ConfigureDiscovery()
    {
        var assembly = this.Assembly ?? Assembly.GetCallingAssembly();
        if (this.AutoDiscoverTools) McpServerBuilder.WithToolsFromAssembly(assembly);
        if (this.AutoDiscoverResources) McpServerBuilder.WithResourcesFromAssembly(assembly);
        if (this.AutoDiscoverPrompts) McpServerBuilder.WithPromptsFromAssembly(assembly);
    }

    private void ConfigureFeatures()
    {
        // Core Task Support
        Services.AddSingleton<IMcpTaskStore, InMemoryMcpTaskStore>();

        // Map Booleans to SDK Capabilities and Handlers
        Services.Configure<McpServerOptions>(opt =>
        {
            opt.Capabilities ??= new();
            
            if (this.MessageFilter != null)
                opt.Filters.IncomingMessageFilters.Add(this.MessageFilter());
        });

        if (this.EnableCompletion)
        {
            McpServerBuilder.WithCompleteHandler(async (req, ct) => new CompleteResult
            {
                Completion = new Completion { Values = [], HasMore = false, Total = 0 }
            });
        }

        // Add Progress/Sampling capability flags here as needed based on SDK version
    }

    private void ConfigureDiagnostics()
    {
        if (!this.EnableDevelopmentFeatures) return;

        McpServerBuilder.AddIncomingMessageFilter(msg => {
            Console.Error.WriteLine($"[DEBUG] IN: {msg}");
            return msg;
        });

        McpServerBuilder.AddOutgoingMessageFilter(msg => {
            Console.Error.WriteLine($"[DEBUG] OUT: {msg}");
            return msg;
        });
    }

    private void ConfigureAuthentication()
    {
        if (!this.RequireAuthentication && string.IsNullOrEmpty(this.AuthenticationScheme)) return;

        var scheme = this.AuthenticationScheme?.ToLowerInvariant() ?? "jwt";

        switch (scheme)
        {
            case "oauth":
                if (string.IsNullOrEmpty(this.OAuthClientId)) throw new InvalidOperationException("OAuthClientId missing.");
                Services.AddAuthentication(a => {
                    a.DefaultChallengeScheme = "McpOAuth";
                    a.DefaultAuthenticateScheme = "Bearer";
                }).AddJwtBearer("Bearer", j => ApplyJwt(j, this));
                break;

            case "jwt":
                Services.AddAuthentication("Bearer").AddJwtBearer(j => ApplyJwt(j, this));
                break;

            case "custom":
                if (this.CustomAuthHandler == null) throw new InvalidOperationException("CustomAuthHandler missing.");
                Services.AddSingleton(this.CustomAuthHandler);
                break;
        }
    }

    private void ApplyJwt(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions target, AIKitMcpBuilder source)
    {
        target.Authority = source.JwtAuthority ?? target.Authority;
        target.TokenValidationParameters.ValidAudience = source.JwtAudience ?? target.TokenValidationParameters.ValidAudience;
        target.TokenValidationParameters.ValidIssuer = source.JwtIssuer ?? target.TokenValidationParameters.ValidIssuer;
        // Note: ValidationParameters not used here, perhaps extend if needed
    }
}