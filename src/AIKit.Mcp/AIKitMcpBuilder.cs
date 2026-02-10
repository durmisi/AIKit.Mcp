using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Reflection;

namespace AIKit.Mcp;

public sealed class AIKitMcpBuilder
{
    public IServiceCollection Services { get; }
    public IMcpServerBuilder McpServerBuilder { get; }
    public McpOptions Options { get; } = new();

    private Func<McpMessageFilter>? _messageFilter;

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
    /// Configures the MCP server using a fluent action.
    /// </summary>
    public AIKitMcpBuilder WithOptions(Action<McpOptions> configure)
    {
        configure(Options);
        return this;
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
        if (string.IsNullOrEmpty(Options.ServerName) && string.IsNullOrEmpty(Options.ServerVersion)) return;

        Services.Configure<McpServerOptions>(opt =>
        {
            if (opt.ServerInfo == null) return;
            if (!string.IsNullOrEmpty(Options.ServerName)) opt.ServerInfo.Name = Options.ServerName;
            if (!string.IsNullOrEmpty(Options.ServerVersion)) opt.ServerInfo.Version = Options.ServerVersion;
        });
    }

    private void ConfigureTransport()
    {
        if (Options.Transport == TransportType.Http)
            McpServerBuilder.WithHttpTransport();
        else
            McpServerBuilder.WithStdioServerTransport();
    }

    private void ConfigureDiscovery()
    {
        var assembly = Options.Assembly ?? Assembly.GetCallingAssembly();
        if (Options.AutoDiscoverTools) McpServerBuilder.WithToolsFromAssembly(assembly);
        if (Options.AutoDiscoverResources) McpServerBuilder.WithResourcesFromAssembly(assembly);
        if (Options.AutoDiscoverPrompts) McpServerBuilder.WithPromptsFromAssembly(assembly);
    }

    private void ConfigureFeatures()
    {
        // Core Task Support
        Services.AddSingleton<IMcpTaskStore, InMemoryMcpTaskStore>();

        // Map Booleans to SDK Capabilities and Handlers
        Services.Configure<McpServerOptions>(opt =>
        {
            opt.Capabilities ??= new();
            
            if (Options.MessageFilter != null)
                opt.Filters.IncomingMessageFilters.Add(Options.MessageFilter());
        });

        if (Options.EnableCompletion)
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
        if (!Options.EnableDevelopmentFeatures) return;

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
        if (!Options.RequireAuthentication && string.IsNullOrEmpty(Options.AuthenticationScheme)) return;

        var scheme = Options.AuthenticationScheme?.ToLowerInvariant() ?? "jwt";

        switch (scheme)
        {
            case "oauth":
                if (Options.OAuthOptions == null) throw new InvalidOperationException("OAuthOptions missing.");
                Services.AddAuthentication(a => {
                    a.DefaultChallengeScheme = "McpOAuth";
                    a.DefaultAuthenticateScheme = "Bearer";
                }).AddJwtBearer("Bearer", j => ApplyJwt(j, Options.JwtOptions));
                break;

            case "jwt":
                Services.AddAuthentication("Bearer").AddJwtBearer(j => ApplyJwt(j, Options.JwtOptions));
                break;

            case "custom":
                if (Options.CustomAuthHandler == null) throw new InvalidOperationException("CustomAuthHandler missing.");
                Services.AddSingleton(Options.CustomAuthHandler);
                break;
        }
    }

    private void ApplyJwt(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions target, JwtOptions? source)
    {
        if (source == null) return;
        target.Authority = source.Authority ?? target.Authority;
        target.TokenValidationParameters.ValidAudience = source.Audience ?? target.TokenValidationParameters.ValidAudience;
        target.TokenValidationParameters.ValidIssuer = source.Issuer ?? target.TokenValidationParameters.ValidIssuer;
    }
}