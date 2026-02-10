# AIKit.Mcp - Copilot Instructions

## Architecture Overview

AIKit.Mcp is a .NET wrapper library for the ModelContextProtocol (MCP) SDK that simplifies MCP server configuration through a fluent builder pattern. The core architecture consists of:

- **AIKitMcpBuilder**: Fluent wrapper around the official `IMcpServerBuilder`
- **McpOptions**: Strongly-typed configuration class
- **McpServiceExtensions**: Extension methods for IServiceCollection
- **Attribute-based discovery**: Uses `[McpServerToolType]`, `[McpServerTool]`, `[McpServerResource]`, `[McpServerPrompt]` attributes

## Key Patterns & Conventions

### Configuration Patterns

```csharp
// Options pattern (preferred)
builder.Services.AddAIKitMcp()
    .WithOptions(options => {
        options.ServerName = "MyServer";
        options.Transport = "stdio";
        options.AutoDiscoverTools = true;
        options.EnableValidation = true;
        options.EnableTasks = true;        // Enable MCP Tasks support
        options.EnableElicitation = true;  // Enable user input requests
        options.EnableProgress = true;     // Enable progress notifications
        options.EnableCompletion = true;   // Enable auto-completion
        options.EnableSampling = true;     // Enable LLM sampling
        options.HttpBasePath = "/mcp";     // HTTP transport base path
        options.RequireAuthentication = false; // HTTP authentication
        options.Assembly = typeof(Program).Assembly;
    });

// Configuration integration
builder.Services.AddAIKitMcp()
    .WithConfiguration(builder.Configuration);

// Default configuration
builder.Services.AddAIKitMcp()
    .WithDefaultConfiguration();

// Advanced features
builder.Services.AddAIKitMcp()
    .WithTasks()                          // Add in-memory task store
    .WithTaskStore<CustomTaskStore>()     // Add custom task store implementation
    .WithLongRunningTool<MyTool>()        // Register tools for long-running operations
```

### Component Registration

- Register MCP components as **scoped services**: `builder.Services.AddScoped<MyTools>()`
- Use dependency injection in constructors: `public MyTools(ILogger<MyTools> logger)`
- Mark classes with `[McpServerToolType]` for auto-discovery
- Individual methods use `[McpServerTool(Name = "toolName")]`

### Logging Convention

- Logs redirect to stderr to keep stdio clean for JSON-RPC
- Use structured logging: `_logger.LogInformation("Operation {Param} = {Result}", param, result)`

## Advanced Features

### MCP Tasks Support

AIKit.Mcp provides built-in support for MCP Tasks, enabling long-running operations with polling:

```csharp
// Enable tasks in options
options.EnableTasks = true;

// Or explicitly
builder.WithTasks();  // In-memory store
builder.WithTaskStore<CustomTaskStore>();  // Custom implementation

// Use in tools
[McpServerToolType]
public class LongRunningTools
{
    [McpServerTool]
    public async Task<string> ProcessLargeDataset(McpServer server)
    {
        // Create a task that runs in the background
        var taskId = await server.CreateTaskAsync(
            taskId: Guid.NewGuid().ToString(),
            executionDuration: TimeSpan.FromMinutes(5),
            async cancellationToken => {
                // Long-running work here
                await Task.Delay(10000, cancellationToken);
                return JsonValue.Create("Processing complete");
            });

        return $"Task started with ID: {taskId}";
    }
}
```

### Progress Notifications

Report progress for long-running operations using the SDK's built-in progress support:

```csharp
// In tool methods, add IProgress<ProgressNotificationValue> parameter
[McpServerTool(Name = "long_operation")]
public async Task<string> LongOperation(int steps, IProgress<ProgressNotificationValue> progress)
{
    for (int i = 1; i <= steps; i++)
    {
        // Do work...
        progress.Report(new ProgressNotificationValue
        {
            Progress = i,
            Total = steps,
            Message = $"Step {i} of {steps} completed"
        });
        await Task.Delay(100);
    }
    return "Operation completed";
}

// Or use helper methods for manual progress reporting
await McpTaskHelpers.ReportProgressAsync(server, progressToken, 50.0f, 100.0f, "Halfway complete");
await server.NotifyProgressAsync(progressToken, 75.0f, 100.0f, "Almost done");
```

### Elicitation Support

Request additional information from users during tool execution using forms or external URLs:

```csharp
// Enable elicitation in options
options.EnableElicitation = true;

// Or explicitly
builder.WithElicitation();

// Use helper methods for common patterns
[McpServerToolType]
public class InteractiveTools
{
    [McpServerTool(Name = "confirm_action")]
    public async Task<string> ConfirmAction(McpServer server)
    {
        var confirmed = await McpElicitationHelpers.RequestConfirmationAsync(
            server, "Are you sure you want to proceed?", CancellationToken.None);

        return confirmed ? "Action confirmed!" : "Action cancelled.";
    }

    [McpServerTool(Name = "get_user_input")]
    public async Task<string> GetUserInput(McpServer server)
    {
        var name = await McpElicitationHelpers.RequestTextInputAsync(
            server, "What's your name?", minLength: 1, maxLength: 50);

        return $"Hello, {name}!";
    }

    [McpServerTool(Name = "complex_form")]
    public async Task<string> ComplexForm(McpServer server)
    {
        var response = await McpElicitationHelpers.RequestFormInputAsync(
            server,
            "Please provide your details:",
            new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                {
                    ["Name"] = new ElicitRequestParams.StringSchema { Description = "Your full name" },
                    ["Age"] = new ElicitRequestParams.NumberSchema { Minimum = 0, Maximum = 120 }
                }
            });

        if (response != null)
        {
            var name = response["Name"]?.GetValue<string>();
            var age = response["Age"]?.GetValue<int>();
            return $"Received: {name}, age {age}";
        }

        return "Form cancelled.";
    }
}
```

### Enhanced Helper Methods

AIKit.Mcp provides additional helper methods for common MCP patterns:

#### Task Helpers with Retry Logic

```csharp
// Create tasks with built-in retry
var taskId = await McpTaskHelpers.CreateTaskWithRetryAsync(
    server, "myTask", TimeSpan.FromMinutes(5),
    async ct => {
        // Operation that might fail
        return JsonValue.Create("result");
    },
    maxRetries: 3, retryDelay: TimeSpan.FromSeconds(2));

// Report progress
await McpTaskHelpers.ReportProgressAsync(server, progressToken, 50.0f, 100.0f, "Half complete");
```

#### Elicitation Helpers for Common Inputs

```csharp
// Pre-built input types
var email = await McpElicitationHelpers.RequestEmailInputAsync(server, "Enter your email:");
var date = await McpElicitationHelpers.RequestDateInputAsync(server, "Select a date:");
var number = await McpElicitationHelpers.RequestNumberInputAsync(server, "Enter a number:", min: 0, max: 100);
```

#### Completion Helpers with Custom Logic

```csharp
// Dynamic completion based on function
var dynamicHandler = McpCompletionHelpers.CreateDynamicCompletionHandler(
    (argName, value) => GetSuggestions(argName, value));

// Tool name completion
var toolHandler = McpCompletionHelpers.CreateToolCompletionHandler(toolNames);
```

#### Sampling Helpers for Code and Structured Output

```csharp
// Generate code
var code = await McpSamplingHelpers.GenerateCodeAsync(
    server, "csharp", "Create a method to calculate factorial", maxTokens: 200);

// Generate structured JSON
var json = await McpSamplingHelpers.GenerateStructuredOutputAsync(
    server, "Create a user profile", "{\"name\": \"string\", \"age\": \"number\"}");
```

### Completion Support

Provide auto-completion suggestions for prompt arguments and resource references:

```csharp
// Enable completion in options
options.EnableCompletion = true;

// Or explicitly
builder.WithCompletion();

// Use helper methods to create completion handlers
[McpServerToolType]
public class CompletionExampleTools
{
    [McpServerTool(Name = "use_completion")]
    public string UseCompletion(string style)
    {
        // Completion will suggest values for the 'style' parameter
        return $"Using style: {style}";
    }
}

// In Program.cs, configure completion
builder.Services.AddAIKitMcp()
    .WithCompletion()  // Basic empty completion
    .WithOptions(options => {
        // Configure completion handler
        var completions = new Dictionary<string, IEnumerable<string>>
        {
            ["style"] = ["formal", "casual", "technical", "friendly"]
        };

        // Note: Custom completion handlers need to be set on the McpServerOptions
        // after builder configuration
    });
```

### Sampling Support

Request LLM completions from the client for AI-powered features:

```csharp
// Enable sampling in options
options.EnableSampling = true;

// Or explicitly
builder.WithSampling();

// Use helper methods for text generation
[McpServerToolType]
public class AITools
{
    [McpServerTool(Name = "generate_summary")]
    public async Task<string> GenerateSummary(McpServer server, string topic)
    {
        var summary = await McpSamplingHelpers.GenerateTextAsync(
            server,
            $"Summarize the key points about {topic} in 2-3 sentences.",
            maxTokens: 150,
            temperature: 0.7f);

        return $"Summary of {topic}:\n{summary}";
    }

    [McpServerTool(Name = "chat_with_ai")]
    public async Task<string> ChatWithAI(McpServer server, string question)
    {
        var response = await McpSamplingHelpers.GenerateChatResponseAsync(
            server,
            new[] {
                new ChatMessage(ChatRole.System, "You are a helpful assistant."),
                new ChatMessage(ChatRole.User, question)
            },
            maxTokens: 100);

        return response.Messages.FirstOrDefault()?.Text ?? "No response.";
    }
}
```

### HTTP Transport

Enable HTTP-based MCP servers for web integration (requires ModelContextProtocol.AspNetCore package):

```csharp
// Configure HTTP transport in options
builder.Services.AddAIKitMcp()
    .WithOptions(options => {
        options.Transport = TransportType.Http;
        options.HttpBasePath = "/mcp";
        options.RequireAuthentication = false; // Set to true for production
    });

// For web applications, use WebApplication.CreateBuilder instead of Host.CreateApplicationBuilder
```

## Developer Workflows

### Building & Testing

```bash
# Build entire solution
cd src && dotnet build AIKit.Mcp.slnx

# Run tests
cd src && dotnet test

# Run sample application
cd samples/AIKit.Mcp.Sample && dotnet run
```

### Project Structure Notes

- Solution file: `src/AIKit.Mcp.slnx` (uses .slnx format)
- Tests located in: `samples/tests/` (not standard location)
- Sample app: `samples/AIKit.Mcp.Sample/`
- Each class in separate file (recent refactoring)

## Configuration Integration

### appsettings.json Structure

```json
{
  "Mcp": {
    "ServerName": "MyMcpServer",
    "ServerVersion": "1.0.0",
    "Transport": "stdio",
    "AutoDiscoverTools": true,
    "AutoDiscoverResources": true,
    "AutoDiscoverPrompts": true,
    "EnableTasks": false,
    "EnableElicitation": false,
    "EnableProgress": false,
    "EnableCompletion": false,
    "EnableSampling": false,
    "HttpBasePath": "/mcp",
    "RequireAuthentication": false
  }
}
```

### Validation

- Enable with `options.EnableValidation = true`
- Runs at startup via `McpValidationHostedService`
- Validates configuration and logs warnings for issues

## Common Implementation Patterns

### Tool Class Example

```csharp
[McpServerToolType]
public class MyTools
{
    private readonly ILogger<MyTools> _logger;

    public MyTools(ILogger<MyTools> logger) => _logger = logger;

    [McpServerTool(Name = "my_operation")]
    public string MyOperation(string input)
    {
        _logger.LogInformation("Processing {Input}", input);
        return $"Processed: {input}";
    }
}
```

### Resource Class Example

```csharp
[McpServerToolType]
public class MyResources
{
    [McpServerResource(UriTemplate = "file://my/data", Name = "My Data")]
    public string GetData() => "resource data";
}
```

## Dependencies & Framework

- **Target**: .NET 8.0
- **MCP SDK**: ModelContextProtocol 0.8.0-preview.1
- **DI**: Microsoft.Extensions.DI
- **Logging**: Microsoft.Extensions.Logging (redirects to stderr)
- **Testing**: xUnit with service collection testing

## File Organization

- `src/AIKit.Mcp/` - Main library code
- `samples/AIKit.Mcp.Sample/` - Working example
- `samples/tests/AIKit.Mcp.Tests/` - Test suite
- Each class in dedicated .cs file (post-refactoring)</content>
  <parameter name="filePath">c:\Projects\AIKit.Mcp\.github\copilot-instructions.md
