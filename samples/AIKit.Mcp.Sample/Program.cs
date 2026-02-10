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
// - Logging and observability with OpenTelemetry and MCP logging protocol
//
// To enable HTTP transport with authentication:
// 1. Change Transport to "http"
// 2. Configure OAuth or JWT properties in Authentication
// 3. Enable header forwarding if needed
// 4. Use WebApplication.CreateBuilder instead of Host.CreateApplicationBuilder
//
// See https://aka.ms/new-console-template for more information
using AIKit.Mcp;
using AIKit.Mcp.Sample;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configure logging - basic setup, detailed config via AIKit.Mcp
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure AIKit MCP server using the new options pattern
builder.Services.AddAIKitMcp(mcp =>
{
    // Server identification
    mcp.ServerName = "AIKit.Sample.Server";
    mcp.ServerVersion = "1.0.0";

    // Transport configuration
    // mcp.WithStdioTransport(); // Use Stdio transport (default)
    // For HTTP transport, use: mcp.WithHttpTransport(opts => { opts.HttpBasePath = "/mcp"; opts.Authentication = new OAuthAuth { /* ... */ }; /* etc. */ });

    // HTTP transport options (only used when WithHttpTransport is called)
    // Requires ModelContextProtocol.AspNetCore package and web host setup
    mcp.WithHttpTransport(opts =>
    {
        opts.HttpBasePath = "/mcp";
        opts.Authentication = new OAuthAuth
        {
            AuthenticationScheme = "Bearer", // JWT Bearer authentication
            // OAuth 2.0 configuration for client-side authentication
            OAuthClientId = "your-client-id",                    // OAuth 2.0 client ID
            OAuthClientSecret = "your-client-secret",            // OAuth 2.0 client secret
            OAuthAuthorizationServerUrl = new Uri("https://your-oauth-provider.com"), // OAuth 2.0 server URL
            OAuthScopes = new List<string> { "mcp:tools", "files:read" }, // OAuth 2.0 scopes (MCP-specific)
            // Protected resource metadata for OAuth 2.0 resource server (per RFC 9396)
            ProtectedResource = new Uri("http://localhost:5000/mcp"), // MCP server base URI (must match client requests)
            ProtectedScopesSupported = new List<string> { "mcp:tools", "files:read" }, // Supported scopes (must match OAuthScopes)
            ProtectedResourceName = "MCP Server API", // Human-readable resource name
            ProtectedAuthorizationServers = new List<Uri> { new Uri("https://your-oauth-provider.com") } // OAuth server URIs
        };
        // Alternative: JWT authentication
        // opts.Authentication = new JwtAuth
        // {
        //     AuthenticationScheme = "Bearer",
        //     JwtAuthority = "https://your-jwt-issuer.com",
        //     JwtAudience = "your-api-audience"
        // };
        // Alternative: Custom authentication
        // opts.Authentication = new CustomAuth
        // {
        //     AuthenticationScheme = "Custom",
        //     CustomAuthHandler = async (sp) => { /* custom logic */ }
        // };
    });

    // Auto-discovery settings
    mcp.AutoDiscoverTools = true;
    mcp.AutoDiscoverResources = true;
    mcp.AutoDiscoverPrompts = true;

    // Development features
    mcp.EnableDevelopmentFeatures = true; // Enable message tracing
    mcp.EnableValidation = true;          // Enable startup validation

    // Enable advanced MCP features
    mcp.EnableProgress = true;        // Enable progress notifications
    mcp.EnableCompletion = true;      // Enable auto-completion
    mcp.EnableSampling = true;        // Enable LLM sampling

    // Logging and Observability
    mcp.WithLogging(opts =>
    {
        opts.RedirectToStderr = true;
        opts.MinLogLevel = LogLevel.Debug;
    });
    mcp.WithOpenTelemetry(opts =>
    {
        opts.ServiceName = "AIKit.Sample.Server";
        opts.ServiceVersion = "1.0.0";
        opts.OtlpEndpoint = "http://localhost:4317"; // Default OTLP gRPC endpoint
    });

    // Configure message-level filters for logging and monitoring
    mcp.MessageFilter = () => next => async (context, cancellationToken) =>
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
});

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 AIKit MCP Sample Server is starting...");
logger.LogInformation("📋 Available tools: Math operations, Interactive games, AI sampling");
logger.LogInformation("📁 Available resources: File system access");
logger.LogInformation("💬 Available prompts: Conversation helpers");
logger.LogInformation("✨ Advanced features: Tasks, Progress, Elicitation, Completion, Sampling");

await app.RunAsync();