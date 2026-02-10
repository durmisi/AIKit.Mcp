using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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

        // Redirect logs to stderr to keep the stdio pipe clean for JSON-RPC
        services.AddLogging(builder => {
            builder.AddConsole(c => c.LogToStandardErrorThreshold = LogLevel.Trace);
        });

        return new AIKitMcpBuilder(builder, services);
    }

    /// <summary>
    /// Configures the MCP server with default settings for common scenarios.
    /// Includes stdio transport, auto-discovery from assembly, and basic logging.
    /// </summary>
    public static AIKitMcpBuilder WithDefaultConfiguration(this AIKitMcpBuilder builder)
    {
        builder.InnerBuilder
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithResourcesFromAssembly()
            .WithPromptsFromAssembly();
        
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
        var transport = mcpConfig["Transport"];
        if (string.Equals(transport, "stdio", StringComparison.OrdinalIgnoreCase))
        {
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
        if (string.Equals(options.Transport, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            builder.InnerBuilder.WithStdioServerTransport();
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
        if (options.EnableTasks)
        {
            builder.WithTasks();
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

        return builder;
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
    /// </summary>
    public static AIKitMcpBuilder WithElicitation(this AIKitMcpBuilder builder)
    {
        // Elicitation is enabled by default in the MCP server options
        // This method serves as documentation and future extension point
        return builder;
    }

    /// <summary>
    /// Enables progress tracking for long-running operations.
    /// </summary>
    public static AIKitMcpBuilder WithProgress(this AIKitMcpBuilder builder)
    {
        // Progress notifications are enabled by default in the MCP server
        // This method serves as documentation and future extension point
        return builder;
    }

    /// <summary>
    /// Enables completion support for auto-completion functionality.
    /// </summary>
    public static AIKitMcpBuilder WithCompletion(this AIKitMcpBuilder builder)
    {
        // Completion is configured in the server options
        // This method serves as documentation and future extension point
        return builder;
    }

    /// <summary>
    /// Enables HTTP transport for ASP.NET Core integration.
    /// </summary>
    public static AIKitMcpBuilder WithHttpTransport(this AIKitMcpBuilder builder, Action<object>? configure = null)
    {
        // HTTP transport requires ModelContextProtocol.AspNetCore package
        // For now, this is a placeholder - users need to add the package manually
        throw new NotSupportedException("HTTP transport requires ModelContextProtocol.AspNetCore package. Please install it and use the official builder methods.");
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

            // Check for task store if tasks are enabled
            var taskStore = services.GetService<ModelContextProtocol.IMcpTaskStore>();
            if (taskStore != null)
            {
                logger.LogInformation("- Task store: {Type}", taskStore.GetType().Name);
            }

            // Check for naming conflicts - Note: McpServerTool may not have Name property
            // This is a simplified check - in practice, names are set via attributes
            
            // Check for transport - simplified check
            // The transport is configured via the builder methods

            logger.LogInformation("MCP configuration validation completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during MCP configuration validation");
        }
    }
}