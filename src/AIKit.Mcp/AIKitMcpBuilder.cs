using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;

namespace AIKit.Mcp;

public sealed class AIKitMcpBuilder
{
    private IServiceCollection _services { get; }

    private IMcpServerBuilder _mcpServerBuilder { get; }

    // Server metadata
    public string? ServerName { get; set; }

    public string? ServerVersion { get; set; }

    // Transport (private)
    private bool _isHttpTransport = false;

    private bool _transportConfigured = false;
    private HttpTransportOptions? _httpOptions;

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

    // Custom
    public Func<McpMessageFilter>? MessageFilter { get; set; }

    // Logging and Observability
    private LoggingOptions? _loggingOptions;

    private OpenTelemetryOptions? _openTelemetryOptions;

    public AIKitMcpBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _mcpServerBuilder = services.AddMcpServer();
    }

    /// <summary>
    /// Configures the server to use Stdio transport.
    /// </summary>
    public AIKitMcpBuilder WithStdioTransport()
    {
        if (_transportConfigured)
            throw new InvalidOperationException("Transport has already been configured. Only one transport type can be set.");

        _isHttpTransport = false;
        _transportConfigured = true;
        return this;
    }

    /// <summary>
    /// Configures the server to use HTTP transport with the specified options.
    /// </summary>
    public AIKitMcpBuilder WithHttpTransport(Action<HttpTransportOptions> configure)
    {
        if (_transportConfigured)
            throw new InvalidOperationException("Transport has already been configured. Only one transport type can be set.");

        var options = new HttpTransportOptions();
        configure?.Invoke(options);

        _httpOptions = options;
        _isHttpTransport = true;
        _transportConfigured = true;

        return this;
    }

    /// <summary>
    /// Finalizes the configuration and returns the SDK builder.
    /// Orchestrates the transition from AIKit Options to the underlying MCP SDK.
    /// </summary>
    public IMcpServerBuilder Build()
    {
        ConfigureMetadata();
        ConfigureTransport();
        ConfigureAuthentication();
        ConfigureDiscovery();
        ConfigureFeatures();
        ConfigureDiagnostics();
        ConfigureLogging();

        return _mcpServerBuilder;
    }

    /// <summary>
    /// Sets up default logging to stderr.
    /// </summary>
    public AIKitMcpBuilder WithLogging(Action<LoggingOptions>? configure = null)
    {
        var logOptions = new LoggingOptions();
        configure?.Invoke(logOptions);
        _loggingOptions = logOptions;
        return this;
    }

    /// <summary>
    /// Configures OpenTelemetry for tracing, metrics, and logging.
    /// </summary>
    public AIKitMcpBuilder WithOpenTelemetry(Action<OpenTelemetryOptions>? configure = null)
    {
        var otelOptions = new OpenTelemetryOptions();
        configure?.Invoke(otelOptions);
        _openTelemetryOptions = otelOptions;
        return this;
    }

    /// <summary>
    /// Registers a custom Task Store implementation.
    /// </summary>
    public AIKitMcpBuilder WithTaskStore<TTaskStore>() where TTaskStore : class,
        IMcpTaskStore
    {
        _services.AddSingleton<IMcpTaskStore, TTaskStore>();
        return this;
    }

    /// <summary>
    /// Discovers tools, resources, and prompts from the calling assembly.
    /// </summary>
    public AIKitMcpBuilder WithAllFromAssembly(Assembly? assembly = null)
    {
        var target = assembly ?? Assembly.GetCallingAssembly();
        _mcpServerBuilder.WithToolsFromAssembly(target)
                        .WithResourcesFromAssembly(target)
                        .WithPromptsFromAssembly(target);
        return this;
    }

    private void ConfigureMetadata()
    {
        if (string.IsNullOrEmpty(this.ServerName) && string.IsNullOrEmpty(this.ServerVersion)) return;

        _services.Configure<McpServerOptions>(opt =>
        {
            if (opt.ServerInfo == null) return;
            if (!string.IsNullOrEmpty(this.ServerName)) opt.ServerInfo.Name = this.ServerName;
            if (!string.IsNullOrEmpty(this.ServerVersion)) opt.ServerInfo.Version = this.ServerVersion;
        });
    }

    private void ConfigureTransport()
    {
        if (_isHttpTransport)
            _mcpServerBuilder.WithHttpTransport();
        else
            _mcpServerBuilder.WithStdioServerTransport();
    }

    private void ConfigureDiscovery()
    {
        var assembly = this.Assembly ?? Assembly.GetCallingAssembly();

        if (this.AutoDiscoverTools)
        {
            _mcpServerBuilder.WithToolsFromAssembly(assembly);
        }

        if (this.AutoDiscoverResources)
        {
            _mcpServerBuilder.WithResourcesFromAssembly(assembly);
        }

        if (this.AutoDiscoverPrompts) _mcpServerBuilder.WithPromptsFromAssembly(assembly);
    }

    private void ConfigureFeatures()
    {
        // Core Task Support
        _services.AddSingleton<IMcpTaskStore, InMemoryMcpTaskStore>();

        // Map Booleans to SDK Capabilities and Handlers
        _services.Configure<McpServerOptions>(opt =>
        {
            opt.Capabilities ??= new();

            if (this.MessageFilter != null)
                opt.Filters.IncomingMessageFilters.Add(this.MessageFilter());
        });

        if (this.EnableCompletion)
        {
            _mcpServerBuilder.WithCompleteHandler(async (req, ct) => new CompleteResult
            {
                Completion = new Completion { Values = [], HasMore = false, Total = 0 }
            });
        }

        // Add Progress/Sampling capability flags here as needed based on SDK version
    }

    private void ConfigureDiagnostics()
    {
        if (!this.EnableDevelopmentFeatures) return;

        _mcpServerBuilder.AddIncomingMessageFilter(msg =>
        {
            Console.Error.WriteLine($"[DEBUG] IN: {msg}");
            return msg;
        });

        _mcpServerBuilder.AddOutgoingMessageFilter(msg =>
        {
            Console.Error.WriteLine($"[DEBUG] OUT: {msg}");
            return msg;
        });
    }

    private void ConfigureLogging()
    {
        if (_loggingOptions != null)
        {
            _services.AddLogging(logging =>
            {
                logging.AddConsole(c =>
                {
                    if (_loggingOptions.RedirectToStderr)
                        c.LogToStandardErrorThreshold = _loggingOptions.MinLogLevel;
                });
                logging.SetMinimumLevel(_loggingOptions.MinLogLevel);
            });
        }

        if (_openTelemetryOptions != null)
        {
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(_openTelemetryOptions.ServiceName, serviceVersion: _openTelemetryOptions.ServiceVersion);

            _services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(_openTelemetryOptions.ServiceName, serviceVersion: _openTelemetryOptions.ServiceVersion))
                .WithTracing(b =>
                {
                    if (_openTelemetryOptions.EnableTracing)
                    {
                        b.AddSource("*")
                         .AddAspNetCoreInstrumentation()
                         .AddHttpClientInstrumentation();
                        if (_openTelemetryOptions.OtlpExporterConfigurator != null)
                        {
                            b.AddOtlpExporter(_openTelemetryOptions.OtlpExporterConfigurator);
                        }
                        else if (!string.IsNullOrEmpty(_openTelemetryOptions.OtlpEndpoint))
                        {
                            b.AddOtlpExporter(o => o.Endpoint = new Uri(_openTelemetryOptions.OtlpEndpoint));
                        }
                        else
                        {
                            b.AddOtlpExporter();
                        }
                    }
                })
                .WithMetrics(b =>
                {
                    if (_openTelemetryOptions.EnableMetrics)
                    {
                        b.AddMeter("*")
                         .AddAspNetCoreInstrumentation()
                         .AddHttpClientInstrumentation();
                        if (_openTelemetryOptions.OtlpExporterConfigurator != null)
                        {
                            b.AddOtlpExporter(_openTelemetryOptions.OtlpExporterConfigurator);
                        }
                        else if (!string.IsNullOrEmpty(_openTelemetryOptions.OtlpEndpoint))
                        {
                            b.AddOtlpExporter(o => o.Endpoint = new Uri(_openTelemetryOptions.OtlpEndpoint));
                        }
                        else
                        {
                            b.AddOtlpExporter();
                        }
                    }
                })
                .WithLogging(b =>
                {
                    if (_openTelemetryOptions.EnableLogging)
                    {
                        b.SetResourceBuilder(resourceBuilder);
                        if (_openTelemetryOptions.OtlpExporterConfigurator != null)
                        {
                            b.AddOtlpExporter(_openTelemetryOptions.OtlpExporterConfigurator);
                        }
                        else if (!string.IsNullOrEmpty(_openTelemetryOptions.OtlpEndpoint))
                        {
                            b.AddOtlpExporter(o => o.Endpoint = new Uri(_openTelemetryOptions.OtlpEndpoint));
                        }
                        else
                        {
                            b.AddOtlpExporter();
                        }
                    }
                });
        }
    }

    private void ConfigureAuthentication()
    {
        if (!_isHttpTransport || _httpOptions == null || _httpOptions.Authentication == null) return;

        var auth = _httpOptions.Authentication;

        switch (auth)
        {
            case OAuthAuth oauth:
                if (string.IsNullOrEmpty(oauth.OAuthClientId)) throw new InvalidOperationException("OAuthClientId missing.");
                _services.AddAuthentication(a =>
                {
                    a.DefaultChallengeScheme = "McpOAuth";
                    a.DefaultAuthenticateScheme = "Bearer";
                }).AddJwtBearer("Bearer", j => ApplyJwt(j, oauth));
                break;

            case JwtAuth jwt:
                _services.AddAuthentication("Bearer").AddJwtBearer(j => ApplyJwt(j, jwt));
                break;

            case CustomAuth custom:
                if (custom.CustomAuthHandler == null) throw new InvalidOperationException("CustomAuthHandler missing.");
                _services.AddSingleton(custom.CustomAuthHandler);
                break;

            default:
                throw new InvalidOperationException("Unknown authentication type.");
        }
    }

    private void ApplyJwt(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions target, OAuthAuth source)
    {
        target.Authority = source.JwtAuthority ?? target.Authority;
        target.TokenValidationParameters.ValidAudience = source.JwtAudience ?? target.TokenValidationParameters.ValidAudience;
        target.TokenValidationParameters.ValidIssuer = source.JwtIssuer ?? target.TokenValidationParameters.ValidIssuer;
        // Note: ValidationParameters not used here, perhaps extend if needed
    }

    private void ApplyJwt(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions target, JwtAuth source)
    {
        target.Authority = source.JwtAuthority ?? target.Authority;
        target.TokenValidationParameters.ValidAudience = source.JwtAudience ?? target.TokenValidationParameters.ValidAudience;
        target.TokenValidationParameters.ValidIssuer = source.JwtIssuer ?? target.TokenValidationParameters.ValidIssuer;
        // Note: ValidationParameters not used here, perhaps extend if needed
    }
}