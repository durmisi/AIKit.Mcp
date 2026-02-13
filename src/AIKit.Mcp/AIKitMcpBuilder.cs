using AIKit.Mcp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.IO.Pipelines;
using System.Reflection;

namespace AIKit.Mcp;

/// <summary>
/// Represents the type of transport used for MCP communication.
/// </summary>
public enum TransportType
{
    /// <summary>
    /// Standard input/output transport.
    /// </summary>
    Stdio,

    /// <summary>
    /// HTTP-based transport.
    /// </summary>
    Http,

    /// <summary>
    /// Stream-based transport.
    /// </summary>
    Stream
}

/// <summary>
/// Fluent builder for configuring AIKit MCP server options.
/// </summary>
public sealed class AIKitMcpBuilder
{
    private IServiceCollection _services { get; }

    private IMcpServerBuilder _mcpServerBuilder { get; }

    // Server metadata
    /// <summary>
    /// Gets or sets the name of the MCP server.
    /// </summary>
    public string? ServerName { get; set; }

    /// <summary>
    /// Gets or sets the version of the MCP server.
    /// </summary>
    public string? ServerVersion { get; set; }

    // Transport (private)
    private TransportType _transportType = TransportType.Stdio;

    private bool _transportConfigured = false;
    private HttpTransportOptions? _httpOptions;
    private Stream? _streamInput;
    private Stream? _streamOutput;

    // Per-session tools
    private PerSessionToolRegistry _toolRegistry = new();

    // Discovery
    private bool _autoDiscovery = false;

    /// <summary>
    /// Gets or sets the assembly to scan for tools, resources, and prompts.
    /// </summary>
    public Assembly? Assembly { get; set; }

    // Features
    /// <summary>
    /// Gets or sets a value indicating whether to enable development features.
    /// </summary>
    public bool EnableDevelopmentFeatures { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable completion.
    /// </summary>
    public bool EnableCompletion { get; set; }

    /// <summary>
    /// Gets or sets the type of task store to use. Default is InMemory.
    /// </summary>
    public TaskStoreType TaskStoreType { get; set; } = TaskStoreType.InMemory;

    // Custom
    /// <summary>
    /// Gets or sets the message filter function.
    /// </summary>
    public Func<McpMessageFilter>? MessageFilter { get; set; }

    // Per-session configuration
    /// <summary>
    /// Gets or sets the function to configure session options per request.
    /// </summary>
    public Func<HttpContext, McpServerOptions, CancellationToken, Task>? ConfigureSessionOptions { get; set; }

    // Logging and Observability
    private LoggingOptions? _loggingOptions;

    private OpenTelemetryOptions? _openTelemetryOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIKitMcpBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public AIKitMcpBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _mcpServerBuilder = services.AddMcpServer();
        _services.AddSingleton(_toolRegistry);
    }

    /// <summary>
    /// Configures the server to use Stdio transport.
    /// </summary>
    public AIKitMcpBuilder WithStdioTransport()
    {
        if (_transportConfigured)
            throw new InvalidOperationException("Transport has already been configured. Only one transport type can be set.");

        _transportType = TransportType.Stdio;
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
        _transportType = TransportType.Http;
        _transportConfigured = true;

        return this;
    }

    /// <summary>
    /// Configures the server to use stream transport with the specified input and output streams.
    /// </summary>
    public AIKitMcpBuilder WithStreamTransport(Stream input, Stream output)
    {
        if (_transportConfigured)
            throw new InvalidOperationException("Transport has already been configured. Only one transport type can be set.");

        _streamInput = input ?? throw new ArgumentNullException(nameof(input));
        _streamOutput = output ?? throw new ArgumentNullException(nameof(output));
        _transportType = TransportType.Stream;
        _transportConfigured = true;
        return this;
    }

    /// <summary>
    /// Enables automatic discovery of tools, resources, and prompts from the assembly.
    /// </summary>
    public AIKitMcpBuilder WithAutoDiscovery()
    {
        _autoDiscovery = true;
        return this;
    }

    /// <summary>
    /// Creates a pair of streams for in-memory communication between client and server.
    /// Returns (clientInput, clientOutput) for the client side.
    /// Use the returned streams with WithStreamTransport on the server.
    /// </summary>
    public static (Stream ClientInput, Stream ClientOutput) CreateInMemoryPipePair()
    {
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        return (serverToClientPipe.Reader.AsStream(), clientToServerPipe.Writer.AsStream());
    }

    /// <summary>
    /// Configures the session options callback for per-session filtering.
    /// </summary>
    public AIKitMcpBuilder WithSessionOptions(Func<HttpContext, McpServerOptions, CancellationToken, Task> configure)
    {
        this.ConfigureSessionOptions = configure;
        return this;
    }

    /// <summary>
    /// Registers a tool type with the MCP server.
    /// </summary>
    public AIKitMcpBuilder WithTools<T>() where T : class
    {
        _mcpServerBuilder.WithTools<T>();
        return this;
    }

    /// <summary>
    /// Registers a tool type for per-session filtering under the specified category.
    /// </summary>
    /// <typeparam name="T">The tool type to register.</typeparam>
    /// <param name="category">The category under which to register the tool.</param>
    /// <returns>The builder instance for chaining.</returns>
    public AIKitMcpBuilder WithTools<T>(string category) where T : class
    {
        if (string.IsNullOrEmpty(category))
            throw new ArgumentException("Category cannot be null or empty.", nameof(category));

        if (!_toolRegistry.CategorizedTools.ContainsKey(category))
            _toolRegistry.CategorizedTools[category] = new();
        _toolRegistry.CategorizedTools[category].Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Registers a resource type with the MCP server.
    /// </summary>
    public AIKitMcpBuilder WithResources<T>() where T : class
    {
        _mcpServerBuilder.WithResources<T>();
        return this;
    }

    /// <summary>
    /// Registers a prompt type with the MCP server.
    /// </summary>
    public AIKitMcpBuilder WithPrompts<T>() where T : class
    {
        _mcpServerBuilder.WithPrompts<T>();
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
    /// Configures and registers a file-based task store with optional custom options.
    /// </summary>
    /// <param name="configure">Optional action to configure the file-based task store options.</param>
    /// <returns>The builder instance for chaining.</returns>
    public AIKitMcpBuilder WithFileBasedTaskStore(Action<FileBasedTaskStoreOptions>? configure = null)
    {
        var options = new FileBasedTaskStoreOptions();
        configure?.Invoke(options);
        _services.AddSingleton(options);
        TaskStoreType = TaskStoreType.FileBased;

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

    /// <summary>
    /// Gets tools for a specific type using reflection, similar to the MCP SDK sample.
    /// </summary>
    /// <typeparam name="T">The tool type to scan for methods with McpServerToolAttribute.</typeparam>
    /// <returns>An array of McpServerTool instances.</returns>
    public static McpServerTool[] GetToolsForType<T>() where T : class
    {
        var tools = new List<McpServerTool>();
        var toolType = typeof(T);
        var methods = toolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Any());
        foreach (var method in methods)
        {
            try
            {
                var tool = McpServerTool.Create(method, target: null, new McpServerToolCreateOptions());
                tools.Add(tool);
            }
            catch (Exception ex)
            {
                // Log error but continue with other tools
                // Note: In a real app, use ILogger, but for library, perhaps throw or ignore
                throw new InvalidOperationException($"Failed to create tool {toolType.Name}.{method.Name}: {ex.Message}", ex);
            }
        }
        return tools.ToArray();
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
        if (_transportType == TransportType.Http)
        {
            _mcpServerBuilder.WithHttpTransport(options =>
            {
                // Set ConfigureSessionOptions from builder or httpOptions
                var sessionOptionsCallback = this.ConfigureSessionOptions ?? _httpOptions?.ConfigureSessionOptions;
                if (sessionOptionsCallback != null)
                    options.ConfigureSessionOptions = sessionOptionsCallback;
            });
        }
        else if (_transportType == TransportType.Stream)
        {
            _mcpServerBuilder.WithStreamServerTransport(_streamInput!, _streamOutput!);
        }
        else
            _mcpServerBuilder.WithStdioServerTransport();
    }

    private void ConfigureDiscovery()
    {
        var assembly = this.Assembly ?? Assembly.GetCallingAssembly();

        if (_autoDiscovery)
        {
            _mcpServerBuilder.WithToolsFromAssembly(assembly);
            _mcpServerBuilder.WithResourcesFromAssembly(assembly);
            _mcpServerBuilder.WithPromptsFromAssembly(assembly);
        }
    }

    private void ConfigureFeatures()
    {
        // Core Task Support
        switch (TaskStoreType)
        {
            case TaskStoreType.FileBased:
                _services.AddSingleton<IMcpTaskStore, FileBasedMcpTaskStore>();
                break;

            default:
                _services.AddSingleton<IMcpTaskStore, InMemoryMcpTaskStore>();
                break;
        }

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
        if (_transportType != TransportType.Http || _httpOptions == null || _httpOptions.AuthOptions == null) return;

        var auth = _httpOptions.AuthOptions;

        _services.AddAuthorization();

        switch (auth)
        {
            case JwtAuth jwt:
                _services
                    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(j => ApplyJwt(j, jwt));
                break;

            case CustomAuth custom:
                var builder = _services
                    .AddAuthentication(a => a.DefaultAuthenticateScheme = custom.SchemeName);
                if (custom.RegisterScheme != null)
                {
                    custom.RegisterScheme(builder);
                }
                else
                {
                    throw new InvalidOperationException("CustomAuth requires a RegisterScheme action to configure the authentication scheme.");
                }
                break;

            case McpAuth mcp:
                _services
                    .AddAuthentication(options =>
                    {
                        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
                        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    })
                    .AddJwtBearer(options => ApplyJwt(options, mcp))
                    .AddMcp(options =>
                    {
                        if (mcp.ResourceMetadata == null)
                        {
                            throw new InvalidOperationException("McpAuth requires ResourceMetadata to be set for the MCP authentication scheme.");
                        }

                        options.ResourceMetadata = mcp.ResourceMetadata;
                    });
                break;

            default:
                throw new InvalidOperationException("Unknown authentication type.");
        }
    }

    private void ApplyJwt(JwtBearerOptions target, JwtAuth source)
    {
        if (source.TokenValidationParameters != null)
        {
            target.TokenValidationParameters = source.TokenValidationParameters;
        }
        else
        {
            target.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
            };
        }

        if (source.Events != null)
        {
            target.Events = source.Events;
        }

        target.Authority = source.Authority ?? target.Authority;

        if (string.IsNullOrEmpty(target.TokenValidationParameters.ValidAudience))
        {
            target.TokenValidationParameters.ValidAudience = source.JwtAudience;
        }

        if (string.IsNullOrEmpty(target.TokenValidationParameters.ValidIssuer))
        {
            target.TokenValidationParameters.ValidIssuer = source.JwtIssuer;
        }

        target.TokenValidationParameters.ClockSkew = TimeSpan.Zero;

        if (!string.IsNullOrEmpty(source.SigningKey))
        {
            target.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(source.SigningKey));
        }
    }

    private void ApplyJwt(JwtBearerOptions target, McpAuth source)
    {
        if (source.TokenValidationParameters != null)
        {
            target.TokenValidationParameters = source.TokenValidationParameters;
        }
        else
        {
            target.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
            };
        }

        if (source.Events != null)
        {
            target.Events = source.Events;
        }

        target.Authority = source.Authority ?? target.Authority;

        if (string.IsNullOrEmpty(target.TokenValidationParameters.ValidIssuer))
        {
            target.TokenValidationParameters.ValidIssuer = target.Authority;
        }

        target.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
    }
}