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

### HTTP Transport

Enable HTTP-based MCP servers for web integration (requires ModelContextProtocol.AspNetCore package):

```csharp
// Note: HTTP transport requires additional package installation
builder.WithHttpTransport(options => {
    options.BasePath = "/mcp";
    options.RequireAuthentication = true;
});
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
    "EnableCompletion": false
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
