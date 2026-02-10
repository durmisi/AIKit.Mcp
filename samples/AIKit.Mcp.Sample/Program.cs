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
        options.Transport = "stdio";

        // Auto-discovery settings
        options.AutoDiscoverTools = true;
        options.AutoDiscoverResources = true;
        options.AutoDiscoverPrompts = true;

        // Development features
        options.EnableDevelopmentFeatures = true; // Enable message tracing
        options.EnableValidation = true;          // Enable startup validation

        // Enable advanced MCP features
        options.EnableTasks = true;           // Enable long-running operations
        options.EnableProgress = true;        // Enable progress notifications
        options.EnableElicitation = true;     // Enable user input requests
        options.EnableCompletion = true;      // Enable auto-completion
        options.EnableSampling = true;        // Enable LLM sampling

        // Use the current assembly for component discovery
        options.Assembly = typeof(Program).Assembly;
    });

// Register business logic classes (services will be resolved via DI)
builder.Services.AddScoped<MathTools>();
builder.Services.AddScoped<FileSystemResources>();
builder.Services.AddScoped<ConversationPrompts>();
builder.Services.AddScoped<InteractiveTools>();

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 AIKit MCP Sample Server is starting...");
logger.LogInformation("📋 Available tools: Math operations, Interactive games, AI sampling");
logger.LogInformation("📁 Available resources: File system access");
logger.LogInformation("💬 Available prompts: Conversation helpers");
logger.LogInformation("✨ Advanced features: Tasks, Progress, Elicitation, Completion, Sampling");

await host.RunAsync();
