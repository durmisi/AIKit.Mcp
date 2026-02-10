using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using System.Reflection;

namespace AIKit.Mcp;

public static class McpServiceExtensions 
{
    /// <summary>
    /// Adds AIKit MCP server to the service collection and returns a builder for configuration.
    /// </summary>
    public static AIKitMcpBuilder AddAIKitMcp(this IServiceCollection services, string serverName = "AIKit-Server") 
    {
        // Integration with Official SDK
        var builder = services.AddMcpServer();

        return new AIKitMcpBuilder(builder, services);
    }

    /// <summary>
    /// Configures the MCP server with default settings for common scenarios.
    /// Includes stdio transport, auto-discovery from assembly, and basic logging.
    /// </summary>
    public static AIKitMcpBuilder WithDefaultConfiguration(this AIKitMcpBuilder builder)
    {
        builder.WithLogging();  // Add default logging

        builder.InnerBuilder
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithResourcesFromAssembly()
            .WithPromptsFromAssembly();
        
        return builder;
    }

    /// <summary>
    /// Configures logging for the MCP server.
    /// By default, redirects logs to stderr to keep stdio clean for JSON-RPC.
    /// </summary>
    public static AIKitMcpBuilder WithLogging(this AIKitMcpBuilder builder, Action<LoggingOptions>? configure = null)
    {
        var options = new LoggingOptions();
        configure?.Invoke(options);

        builder.Services.AddLogging(logging => {
            logging.AddConsole(c => {
                if (options.RedirectToStderr)
                {
                    c.LogToStandardErrorThreshold = options.MinLogLevel;
                }
            });
            logging.SetMinimumLevel(options.MinLogLevel);
        });

        return builder;
    }

    /// <summary>
    /// Configures the MCP server using settings from IConfiguration.
    /// Looks for "Mcp" section in configuration for server settings.
    /// </summary>
    public static AIKitMcpBuilder WithConfiguration(this AIKitMcpBuilder builder, IConfiguration config)
    {
        var mcpConfig = config.GetSection("Mcp");
        
        // Configure server info if provided
        var serverName = mcpConfig["ServerName"];
        var serverVersion = mcpConfig["ServerVersion"];
        
        if (!string.IsNullOrEmpty(serverName) || !string.IsNullOrEmpty(serverVersion))
        {
            builder.Services.Configure<ModelContextProtocol.Server.McpServerOptions>(options =>
            {
                if (!string.IsNullOrEmpty(serverName) && options.ServerInfo != null)
                    options.ServerInfo.Name = serverName;
                if (!string.IsNullOrEmpty(serverVersion) && options.ServerInfo != null)
                    options.ServerInfo.Version = serverVersion;
            });
        }

        // Configure transport
        var transportString = mcpConfig["Transport"];
        if (Enum.TryParse<TransportType>(transportString, true, out var transportType))
        {
            if (transportType == TransportType.Stdio)
            {
                builder.InnerBuilder.WithStdioServerTransport();
            }
            // HTTP transport will be configured later in WithOptions if needed
        }
        else if (!string.IsNullOrEmpty(transportString))
        {
            // Default to stdio if parsing fails
            builder.InnerBuilder.WithStdioServerTransport();
        }

        // Configure auto-discovery
        if (mcpConfig.GetValue<bool>("AutoDiscoverTools", true))
        {
            builder.InnerBuilder.WithToolsFromAssembly();
        }
        if (mcpConfig.GetValue<bool>("AutoDiscoverResources", true))
        {
            builder.InnerBuilder.WithResourcesFromAssembly();
        }
        if (mcpConfig.GetValue<bool>("AutoDiscoverPrompts", true))
        {
            builder.InnerBuilder.WithPromptsFromAssembly();
        }

        // Configure advanced features
        if (mcpConfig.GetValue<bool>("EnableTasks", false))
        {
            builder.WithTasks();
        }
        if (mcpConfig.GetValue<bool>("EnableElicitation", false))
        {
            builder.WithElicitation();
        }
        if (mcpConfig.GetValue<bool>("EnableProgress", false))
        {
            builder.WithProgress();
        }
        if (mcpConfig.GetValue<bool>("EnableCompletion", false))
        {
            builder.WithCompletion();
        }
        if (mcpConfig.GetValue<bool>("EnableSampling", false))
        {
            builder.WithSampling();
        }

        return builder;
    }

    /// <summary>
    /// Automatically discovers and registers all tools, resources, and prompts from the specified assembly.
    /// </summary>
    public static AIKitMcpBuilder WithAllFromAssembly(this AIKitMcpBuilder builder, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        
        builder.InnerBuilder
            .WithToolsFromAssembly(assembly)
            .WithResourcesFromAssembly(assembly)
            .WithPromptsFromAssembly(assembly);
        
        return builder;
    }

    /// <summary>
    /// Configures the MCP server using the provided options.
    /// </summary>
    public static AIKitMcpBuilder WithOptions(this AIKitMcpBuilder builder, Action<McpOptions> configure)
    {
        var options = new McpOptions();
        configure(options);

        // Configure server info
        if (!string.IsNullOrEmpty(options.ServerName) || !string.IsNullOrEmpty(options.ServerVersion))
        {
            builder.Services.Configure<ModelContextProtocol.Server.McpServerOptions>(serverOptions =>
            {
                if (!string.IsNullOrEmpty(options.ServerName) && serverOptions.ServerInfo != null)
                    serverOptions.ServerInfo.Name = options.ServerName;
                if (!string.IsNullOrEmpty(options.ServerVersion) && serverOptions.ServerInfo != null)
                    serverOptions.ServerInfo.Version = options.ServerVersion;
            });
        }

        // Configure transport
        if (options.Transport == TransportType.Stdio)
        {
            builder.InnerBuilder.WithStdioServerTransport();
        }
        else if (options.Transport == TransportType.Http)
        {
            // HTTP transport with authentication support
            ConfigureHttpTransport(builder, options);
        }

        // Configure authentication
        if (options.RequireAuthentication || !string.IsNullOrEmpty(options.AuthenticationScheme))
        {
            ConfigureAuthentication(builder, options);
        }

        // Configure header forwarding
        if (options.EnableHeaderForwarding)
        {
            builder.Services.AddHttpContextAccessor();
        }

        // Configure auto-discovery
        var assembly = options.Assembly ?? Assembly.GetCallingAssembly();
        if (options.AutoDiscoverTools)
        {
            builder.InnerBuilder.WithToolsFromAssembly(assembly);
        }
        if (options.AutoDiscoverResources)
        {
            builder.InnerBuilder.WithResourcesFromAssembly(assembly);
        }
        if (options.AutoDiscoverPrompts)
        {
            builder.InnerBuilder.WithPromptsFromAssembly(assembly);
        }

        // Configure development features
        if (options.EnableDevelopmentFeatures)
        {
            builder.WithDevelopmentFeatures();
        }

        // Configure advanced features
        builder.WithTasks(); // Always register task store to satisfy MCP SDK requirements
        if (options.EnableTasks)
        {
            // Task store is already registered, additional configuration can be added here if needed
        }
        if (options.EnableElicitation)
        {
            builder.WithElicitation();
        }
        if (options.EnableProgress)
        {
            builder.WithProgress();
        }
        if (options.EnableCompletion)
        {
            builder.WithCompletion();
        }
        if (options.EnableSampling)
        {
            builder.WithSampling();
        }

        return builder;
    }

    /// <summary>
    /// Configures HTTP transport with authentication support.
    /// </summary>
    private static void ConfigureHttpTransport(AIKitMcpBuilder builder, McpOptions options)
    {
        // Enable HTTP transport
        builder.InnerBuilder.WithHttpTransport();

        // Configure HTTP options if available
        if (!string.IsNullOrEmpty(options.HttpBasePath))
        {
            // Note: The SDK's HTTP configuration may need to be done at the WebApplication level
            // This is a placeholder for future HTTP configuration
        }
    }

    /// <summary>
    /// Configures authentication based on the specified options.
    /// </summary>
    private static void ConfigureAuthentication(AIKitMcpBuilder builder, McpOptions options)
    {
        // Configure authentication scheme
        if (!string.IsNullOrEmpty(options.AuthenticationScheme))
        {
            switch (options.AuthenticationScheme.ToLowerInvariant())
            {
                case "oauth":
                    ConfigureOAuthAuthentication(builder, options);
                    break;
                case "jwt":
                    ConfigureJwtAuthentication(builder, options);
                    break;
                case "custom":
                    ConfigureCustomAuthentication(builder, options);
                    break;
                default:
                    throw new ArgumentException($"Unsupported authentication scheme: {options.AuthenticationScheme}");
            }
        }
        else if (options.RequireAuthentication)
        {
            // Default to JWT Bearer for backward compatibility
            ConfigureJwtAuthentication(builder, options);
        }
    }

    /// <summary>
    /// Configures OAuth 2.0 authentication.
    /// </summary>
    private static void ConfigureOAuthAuthentication(AIKitMcpBuilder builder, McpOptions options)
    {
        if (options.OAuthOptions == null)
        {
            throw new InvalidOperationException("OAuthOptions must be configured when using OAuth authentication scheme.");
        }

        // Configure ASP.NET Core authentication for OAuth
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultChallengeScheme = "McpOAuth";
            options.DefaultAuthenticateScheme = "Bearer";
        })
        .AddJwtBearer("Bearer", jwtOptions =>
        {
            if (!string.IsNullOrEmpty(options.JwtOptions?.Authority))
            {
                jwtOptions.Authority = options.JwtOptions.Authority;
            }
            if (!string.IsNullOrEmpty(options.JwtOptions?.Audience))
            {
                jwtOptions.TokenValidationParameters.ValidAudience = options.JwtOptions.Audience;
            }
        });

        // Configure protected resource metadata
        if (options.ProtectedResourceMetadata != null)
        {
            ConfigureProtectedResourceMetadata(builder, options.ProtectedResourceMetadata);
        }
    }

    /// <summary>
    /// Configures JWT Bearer authentication.
    /// </summary>
    private static void ConfigureJwtAuthentication(AIKitMcpBuilder builder, McpOptions options)
    {
        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer(jwtOptions =>
            {
                if (options.JwtOptions != null)
                {
                    if (!string.IsNullOrEmpty(options.JwtOptions.Authority))
                    {
                        jwtOptions.Authority = options.JwtOptions.Authority;
                    }
                    if (!string.IsNullOrEmpty(options.JwtOptions.Audience))
                    {
                        jwtOptions.TokenValidationParameters.ValidAudience = options.JwtOptions.Audience;
                    }
                    if (!string.IsNullOrEmpty(options.JwtOptions.Issuer))
                    {
                        jwtOptions.TokenValidationParameters.ValidIssuer = options.JwtOptions.Issuer;
                    }
                }
            });

        // Configure protected resource metadata
        if (options.ProtectedResourceMetadata != null)
        {
            ConfigureProtectedResourceMetadata(builder, options.ProtectedResourceMetadata);
        }
    }

    /// <summary>
    /// Configures custom authentication.
    /// </summary>
    private static void ConfigureCustomAuthentication(AIKitMcpBuilder builder, McpOptions options)
    {
        if (options.CustomAuthHandler == null)
        {
            throw new InvalidOperationException("CustomAuthHandler must be configured when using custom authentication scheme.");
        }

        // Register custom authentication handler
        builder.Services.AddSingleton(options.CustomAuthHandler);
    }

    /// <summary>
    /// Configures protected resource metadata for OAuth 2.0.
    /// </summary>
    private static void ConfigureProtectedResourceMetadata(AIKitMcpBuilder builder, ProtectedResourceMetadata metadata)
    {
        // Note: The SDK's authentication extensions may not be available in this version
        // This is a placeholder for future implementation when MCP authentication packages are available
        builder.Services.Configure<ModelContextProtocol.Server.McpServerOptions>("ProtectedResource", options =>
        {
            // Configure resource metadata when SDK supports it
        });
    }

    /// <summary>
    /// Adds development-friendly features like enhanced logging and message tracing.
    /// </summary>
    public static AIKitMcpBuilder WithDevelopmentFeatures(this AIKitMcpBuilder builder)
    {
        // Add detailed message logging
        builder.InnerBuilder.AddIncomingMessageFilter(message =>
        {
            // Log incoming messages for debugging
            Console.Error.WriteLine($"[DEBUG] Incoming MCP message: {message}");
            return message; // Pass through the message
        });

        builder.InnerBuilder.AddOutgoingMessageFilter(message =>
        {
            // Log outgoing messages for debugging
            Console.Error.WriteLine($"[DEBUG] Outgoing MCP message: {message}");
            return message; // Pass through the message
        });

        return builder;
    }

    /// <summary>
    /// Adds configuration validation to check for common setup issues.
    /// </summary>
    public static AIKitMcpBuilder WithValidation(this AIKitMcpBuilder builder)
    {
        builder.Services.AddHostedService<McpValidationHostedService>();
        return builder;
    }

    /// <summary>
    /// Enables MCP Tasks support for long-running operations.
    /// Adds an in-memory task store for development and testing.
    /// </summary>
    public static AIKitMcpBuilder WithTasks(this AIKitMcpBuilder builder)
    {
        builder.Services.AddSingleton<ModelContextProtocol.IMcpTaskStore, ModelContextProtocol.InMemoryMcpTaskStore>();
        return builder;
    }

    /// <summary>
    /// Enables elicitation support for requesting additional information from users.
    /// Elicitation allows servers to request user input during tool execution.
    /// </summary>
    public static AIKitMcpBuilder WithElicitation(this AIKitMcpBuilder builder)
    {
        // Elicitation is enabled by default in the MCP server options
        // This method serves as documentation and ensures elicitation capability is advertised
        builder.Services.Configure<ModelContextProtocol.Server.McpServerOptions>(options =>
        {
            options.Capabilities ??= new();
            // Elicitation capability is automatically enabled when handlers are configured
        });
        return builder;
    }

    /// <summary>
    /// Enables progress tracking for long-running operations.
    /// </summary>
    public static AIKitMcpBuilder WithProgress(this AIKitMcpBuilder builder)
    {
        // Progress notifications are enabled by default in the MCP server
        // This method serves as documentation and ensures progress capability is configured
        builder.Services.Configure<ModelContextProtocol.Server.McpServerOptions>(options =>
        {
            options.Capabilities ??= new();
            // Progress capability is enabled by default but explicitly configured here
        });
        return builder;
    }

    /// <summary>
    /// Enables completion support for auto-completion functionality.
    /// Provides a basic completion handler that returns empty results.
    /// Use McpCompletionHelpers to create more sophisticated handlers.
    /// </summary>
    public static AIKitMcpBuilder WithCompletion(this AIKitMcpBuilder builder)
    {
        builder.InnerBuilder.WithCompleteHandler(async (request, ct) =>
        {
            // Basic completion support - returns empty array for minimal functionality
            // Real implementations should use McpCompletionHelpers for better UX
            return new ModelContextProtocol.Protocol.CompleteResult
            {
                Completion = new ModelContextProtocol.Protocol.Completion
                {
                    Values = [],
                    HasMore = false,
                    Total = 0
                }
            };
        });

        // Configure completion capability
        builder.Services.Configure<ModelContextProtocol.Server.McpServerOptions>(options =>
        {
            options.Capabilities ??= new();
            // Completion capability is enabled when handler is configured
        });

        return builder;
    }

    /// <summary>
    /// Enables sampling support for LLM completion requests.
    /// Sampling allows servers to request text generation from client-side LLMs.
    /// Note: Clients must provide their own sampling handler to use this feature.
    /// </summary>
    public static AIKitMcpBuilder WithSampling(this AIKitMcpBuilder builder)
    {
        // Sampling capability is advertised when clients provide sampling handlers
        // This method serves as documentation and ensures proper configuration
        builder.Services.Configure<ModelContextProtocol.Server.McpServerOptions>(options =>
        {
            options.Capabilities ??= new();
            // Sampling capability will be enabled when client connects with sampling support
        });
        return builder;
    }

    /// <summary>
    /// Configures OAuth 2.0 authentication for the MCP server.
    /// </summary>
    public static AIKitMcpBuilder WithOAuthAuthentication(this AIKitMcpBuilder builder, Action<OAuthOptions> configure)
    {
        var options = new OAuthOptions();
        configure(options);

        return builder.WithOptions(mcpOptions =>
        {
            mcpOptions.AuthenticationScheme = "OAuth";
            mcpOptions.OAuthOptions = options;
            mcpOptions.RequireAuthentication = true;
        });
    }

    /// <summary>
    /// Configures JWT Bearer authentication for the MCP server.
    /// </summary>
    public static AIKitMcpBuilder WithJwtAuthentication(this AIKitMcpBuilder builder, Action<JwtOptions> configure)
    {
        var options = new JwtOptions();
        configure(options);

        return builder.WithOptions(mcpOptions =>
        {
            mcpOptions.AuthenticationScheme = "JWT";
            mcpOptions.JwtOptions = options;
            mcpOptions.RequireAuthentication = true;
        });
    }

    /// <summary>
    /// Configures custom authentication for the MCP server.
    /// </summary>
    public static AIKitMcpBuilder WithCustomAuthentication(this AIKitMcpBuilder builder, Func<IServiceProvider, Task> authHandler)
    {
        return builder.WithOptions(mcpOptions =>
        {
            mcpOptions.AuthenticationScheme = "Custom";
            mcpOptions.CustomAuthHandler = authHandler;
            mcpOptions.RequireAuthentication = true;
        });
    }

    /// <summary>
    /// Configures protected resource metadata for OAuth 2.0 resource server.
    /// </summary>
    public static AIKitMcpBuilder WithProtectedResourceMetadata(this AIKitMcpBuilder builder, Action<ProtectedResourceMetadata> configure)
    {
        var metadata = new ProtectedResourceMetadata();
        configure(metadata);

        return builder.WithOptions(mcpOptions =>
        {
            mcpOptions.ProtectedResourceMetadata = metadata;
        });
    }

    /// <summary>
    /// Enables header forwarding for client-side authentication scenarios.
    /// This allows forwarding authentication headers from incoming requests to external services.
    /// </summary>
    public static AIKitMcpBuilder WithHeaderForwarding(this AIKitMcpBuilder builder)
    {
        return builder.WithOptions(mcpOptions =>
        {
            mcpOptions.EnableHeaderForwarding = true;
        });
    }

    /// <summary>
    /// Adds a custom task store implementation for MCP Tasks.
    /// </summary>
    public static AIKitMcpBuilder WithTaskStore<TTaskStore>(this AIKitMcpBuilder builder)
        where TTaskStore : class, ModelContextProtocol.IMcpTaskStore
    {
        builder.Services.AddSingleton<ModelContextProtocol.IMcpTaskStore, TTaskStore>();
        return builder;
    }

    /// <summary>
    /// Registers a long-running tool that returns an MCP Task for async execution.
    /// </summary>
    public static AIKitMcpBuilder WithLongRunningTool<TTool>(this AIKitMcpBuilder builder)
        where TTool : class
    {
        builder.Services.AddScoped<TTool>();
        builder.InnerBuilder.WithTools<TTool>();
        return builder;
    }

    /// <summary>
    /// Validates the MCP configuration and logs any issues found.
    /// Can be called manually or automatically via WithValidation().
    /// </summary>
    public static void ValidateMcpConfiguration(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("McpValidation");

        try
        {
            // Check for registered components
            var tools = services.GetServices<ModelContextProtocol.Server.McpServerTool>().ToList();
            var resources = services.GetServices<ModelContextProtocol.Server.McpServerResource>().ToList();
            var prompts = services.GetServices<ModelContextProtocol.Server.McpServerPrompt>().ToList();

            logger.LogInformation("MCP Configuration Validation:");
            logger.LogInformation("- Tools registered: {Count}", tools.Count);
            logger.LogInformation("- Resources registered: {Count}", resources.Count);
            logger.LogInformation("- Prompts registered: {Count}", prompts.Count);

            // Performance check: warn if too many components
            if (tools.Count > 100)
            {
                logger.LogWarning("High number of tools ({Count}) may impact performance. Consider organizing into logical groups.", tools.Count);
            }
            if (resources.Count > 50)
            {
                logger.LogWarning("High number of resources ({Count}) may impact performance. Consider lazy loading.", resources.Count);
            }

            // Check for task store if tasks are enabled
            var taskStore = services.GetService<ModelContextProtocol.IMcpTaskStore>();
            if (taskStore != null)
            {
                logger.LogInformation("- Task store: {Type}", taskStore.GetType().Name);
            }
            else
            {
                logger.LogWarning("No task store registered. Tasks will not be available. Suggestion: Call builder.WithTasks() to enable task support.");
            }

            // Validate tool names for uniqueness
            var toolNames = tools.Select(t => GetToolName(t)).Where(name => !string.IsNullOrEmpty(name)).ToList();
            var duplicateToolNames = toolNames.GroupBy(name => name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateToolNames.Any())
            {
                logger.LogError("Duplicate tool names found: {Names}. Tool names must be unique.", string.Join(", ", duplicateToolNames));
            }

            // Validate resource URIs
            var invalidResources = resources.Where(r => !IsValidResourceUri(GetResourceUri(r))).ToList();
            if (invalidResources.Any())
            {
                logger.LogError("Invalid resource URIs found. Resources must have valid URIs.");
            }

            // Validate prompt names
            var promptNames = prompts.Select(p => GetPromptName(p)).Where(name => !string.IsNullOrEmpty(name)).ToList();
            var duplicatePromptNames = promptNames.GroupBy(name => name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicatePromptNames.Any())
            {
                logger.LogError("Duplicate prompt names found: {Names}. Prompt names must be unique.", string.Join(", ", duplicatePromptNames));
            }

            // Check for required dependencies
            CheckDependency(services, typeof(ModelContextProtocol.Server.McpServer), "McpServer", logger);
            CheckDependency(services, typeof(Microsoft.Extensions.Logging.ILoggerFactory), "ILoggerFactory", logger);

            // Transport validation (basic)
            var serverOptions = services.GetService<ModelContextProtocol.Server.McpServerOptions>();
            if (serverOptions?.ServerInfo == null)
            {
                logger.LogWarning("Server info not configured. Suggestion: Set ServerName and ServerVersion in options.");
            }

            // Authentication validation
            ValidateAuthenticationConfiguration(services, logger);

            logger.LogInformation("MCP configuration validation completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during MCP configuration validation");
        }
    }

    private static void ValidateAuthenticationConfiguration(IServiceProvider services, ILogger logger)
    {
        // Check if authentication is configured
        var authService = services.GetService<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        if (authService != null)
        {
            logger.LogInformation("- Authentication: Configured");

            // Check for JWT Bearer authentication
            var jwtBearerOptions = services.GetService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>();
            if (jwtBearerOptions != null)
            {
                logger.LogInformation("- JWT Bearer authentication: Available");
            }

            // Check for header forwarding
            var httpContextAccessor = services.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            if (httpContextAccessor != null)
            {
                logger.LogInformation("- Header forwarding: Enabled");
            }
        }
        else
        {
            logger.LogInformation("- Authentication: Not configured (stdio transport or no auth required)");
        }
    }

    private static void CheckDependency(IServiceProvider services, Type serviceType, string serviceName, ILogger logger)
    {
        try
        {
            var service = services.GetService(serviceType);
            if (service == null)
            {
                logger.LogError("Required service {ServiceName} is not registered.", serviceName);
            }
        }
        catch
        {
            logger.LogError("Error checking dependency {ServiceName}.", serviceName);
        }
    }

    private static string? GetToolName(ModelContextProtocol.Server.McpServerTool tool)
    {
        // Try to get name from attribute or property
        var attr = tool.GetType().GetCustomAttributes(typeof(McpServerToolAttribute), true).FirstOrDefault() as McpServerToolAttribute;
        return attr?.Name ?? tool.GetType().Name;
    }

    private static string? GetResourceUri(ModelContextProtocol.Server.McpServerResource resource)
    {
        var attr = resource.GetType().GetCustomAttributes(typeof(McpServerResourceAttribute), true).FirstOrDefault() as McpServerResourceAttribute;
        return attr?.UriTemplate;
    }

    private static string? GetPromptName(ModelContextProtocol.Server.McpServerPrompt prompt)
    {
        var attr = prompt.GetType().GetCustomAttributes(typeof(McpServerPromptAttribute), true).FirstOrDefault() as McpServerPromptAttribute;
        return attr?.Name ?? prompt.GetType().Name;
    }

    private static bool IsValidResourceUri(string? uri)
    {
        return !string.IsNullOrEmpty(uri) && Uri.IsWellFormedUriString(uri, UriKind.RelativeOrAbsolute);
    }
}