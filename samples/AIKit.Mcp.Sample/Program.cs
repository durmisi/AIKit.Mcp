// AIKit.Mcp Sample Application
//
// This sample demonstrates the AIKit.Mcp library features including:
// - Fluent configuration API for MCP server setup
// - Auto-discovery of tools, resources, and prompts
// - Advanced MCP features (tasks, progress, elicitation, completion, sampling)
// - Authentication & authorization support for HTTP transport
//   * OAuth 2.0 integration for client-side authentication
//   * JWT Bearer token validation
//   * Header forwarding for service authentication
//   * Protected resource metadata for fine-grained access control
//
// To enable HTTP transport with authentication:
// 1. Change Transport to "http"
// 2. Set RequireAuthentication = true
// 3. Configure OAuthOptions or JwtOptions
// 4. Enable header forwarding if needed
// 5. Use WebApplication.CreateBuilder instead of Host.CreateApplicationBuilder
//
// See https://aka.ms/new-console-template for more information
using AIKit.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using AIKit.Mcp.Sample;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure AIKit MCP server using the new options pattern
builder.Services.AddAIKitMcp()
    .WithOptions(options =>
    {
        // Server identification
        options.ServerName = "AIKit.Sample.Server";
        options.ServerVersion = "1.0.0";

        // Transport configuration
        options.Transport = TransportType.Stdio; // Change to TransportType.Http for HTTP transport

        // HTTP transport options (only used when Transport is "http")
        // Requires ModelContextProtocol.AspNetCore package and web host setup
        options.HttpBasePath = "/mcp";
        options.RequireAuthentication = false; // Set to true for production

        // Authentication configuration (only used when Transport is "http")
        options.AuthenticationScheme = "Bearer"; // JWT Bearer authentication

        // OAuth 2.0 configuration for client-side authentication
        options.OAuthOptions = new AIKit.Mcp.OAuthOptions
        {
            ClientId = "your-client-id",                    // OAuth 2.0 client ID
            ClientSecret = "your-client-secret",            // OAuth 2.0 client secret
            AuthorizationServerUrl = new Uri("https://your-oauth-provider.com"), // OAuth 2.0 server URL
            Scopes = new List<string> { "api.read", "api.write" } // OAuth 2.0 scopes
        };

        // JWT Bearer configuration (alternative to OAuth)
        // options.JwtOptions = new AIKit.Mcp.JwtOptions
        // {
        //     Authority = "https://your-jwt-issuer.com",
        //     Audience = "your-api-audience"
        // };

        // Protected resource metadata for OAuth 2.0 resource server
        options.ProtectedResourceMetadata = new AIKit.Mcp.ProtectedResourceMetadata
        {
            Resource = new Uri("api://my-service/data"),
            ScopesSupported = new List<string> { "api.read", "api.write" },
            ResourceName = "Sensitive Data API",
            AuthorizationServers = new List<Uri> { new Uri("https://your-oauth-provider.com") }
        };

        // Auto-discovery settings
        options.AutoDiscoverTools = true;
        options.AutoDiscoverResources = true;
        options.AutoDiscoverPrompts = true;

        // Development features
        options.EnableDevelopmentFeatures = true; // Enable message tracing
        options.EnableValidation = true;          // Enable startup validation

        // Enable advanced MCP features
        options.EnableProgress = true;        // Enable progress notifications
        options.EnableElicitation = true;     // Enable user input requests
        options.EnableCompletion = true;      // Enable auto-completion
        options.EnableSampling = true;        // Enable LLM sampling

        // Configure message-level filters for logging and monitoring
        options.MessageFilter = () => next => async (context, cancellationToken) =>
        {
            // Simple message logging filter - logs to console
            var method = context.JsonRpcMessage is ModelContextProtocol.Protocol.JsonRpcRequest request
                ? request.Method
                : "non-request";
            Console.WriteLine($"📨 Incoming MCP message: {method}");

            // Call the next handler in the pipeline
            await next(context, cancellationToken);

            // Log completion
            Console.WriteLine($"📤 Message processing completed for: {method}");
        };

        // Use the current assembly for component discovery
        options.Assembly = typeof(Program).Assembly;
    });

// Register business logic classes (services will be resolved via DI)
builder.Services.AddScoped<MathTools>();
builder.Services.AddScoped<FileSystemResources>();
builder.Services.AddScoped<ConversationPrompts>();
builder.Services.AddScoped<InteractiveTools>();
builder.Services.AddScoped<HttpContextTools>();

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 AIKit MCP Sample Server is starting...");
logger.LogInformation("📋 Available tools: Math operations, Interactive games, AI sampling");
logger.LogInformation("📁 Available resources: File system access");
logger.LogInformation("💬 Available prompts: Conversation helpers");
logger.LogInformation("✨ Advanced features: Tasks, Progress, Elicitation, Completion, Sampling");

await host.RunAsync();
