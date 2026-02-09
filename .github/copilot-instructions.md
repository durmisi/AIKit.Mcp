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
        options.Assembly = typeof(Program).Assembly;
    });

// Configuration integration
builder.Services.AddAIKitMcp()
    .WithConfiguration(builder.Configuration);

// Default configuration
builder.Services.AddAIKitMcp()
    .WithDefaultConfiguration();
```

### Component Registration

- Register MCP components as **scoped services**: `builder.Services.AddScoped<MyTools>()`
- Use dependency injection in constructors: `public MyTools(ILogger<MyTools> logger)`
- Mark classes with `[McpServerToolType]` for auto-discovery
- Individual methods use `[McpServerTool(Name = "toolName")]`

### Logging Convention

- Logs redirect to stderr to keep stdio clean for JSON-RPC
- Use structured logging: `_logger.LogInformation("Operation {Param} = {Result}", param, result)`

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
    "AutoDiscoverPrompts": true
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
