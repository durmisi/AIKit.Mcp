# AIKit.Mcp Copilot Instructions

## Architecture Overview
AIKit.Mcp is a .NET wrapper library for the Model Context Protocol (MCP) SDK, providing fluent builder patterns for MCP server configuration. It supports Stdio and HTTP transports with OAuth/JWT authentication, auto-discovery of tools/resources/prompts from assemblies, and advanced MCP features like tasks, progress, completion, and sampling.

Key components:
- `AIKitMcpBuilder`: Fluent configuration API in `src/AIKit.Mcp/AIKitMcpBuilder.cs`
- Service extensions: `AddAIKitMcp()` in `ServiceCollectionExtensions.cs`
- Helpers: Task/progress utilities in `Helpers/` directory
- Sample: Full configuration example in `samples/AIKit.Mcp.Sample/Program.cs`

## Configuration Patterns
Use the builder pattern for server setup:
```csharp
builder.Services.AddAIKitMcp(mcp => {
    mcp.ServerName = "MyServer";
    mcp.WithStdioTransport(); // or WithHttpTransport(opts => { ... })
    mcp.AutoDiscoverTools = true;
    mcp.EnableProgress = true;
});
```

## Tool/Resource/Prompt Implementation
Decorate classes with `[McpServerToolType]` and methods with `[McpServerTool]`. Example from `MathTools.cs`:
```csharp
[McpServerToolType]
public class MathTools {
    [McpServerTool(Name = "add")]
    public double Add(double a, double b) => a + b;
}
```

## Build & Test
- Multi-framework support: net6.0, net8.0, net9.0, net10.0
- Build: `dotnet build src/AIKit.Mcp.slnx`
- Test: Integration tests use `WebApplicationFactory<TestStartup>`
- Logging: Redirects to stderr by default to keep stdio clean for JSON-RPC

## Authentication (HTTP Transport)
Supports OAuth 2.0, JWT, and custom auth. Configure in `WithHttpTransport()`:
```csharp
opts.Authentication = new OAuthAuth {
    OAuthClientId = "client-id",
    OAuthScopes = new() { "mcp:tools" }
};
```

## Advanced Features
- Tasks: Use `IMcpTaskStore` (defaults to in-memory)
- Progress: Call `McpTaskHelpers.ReportProgressAsync()`
- Completion/Sampling: Enable via builder flags
- Message filters: Add via `mcp.MessageFilter` for logging/monitoring

## Dependencies
- ModelContextProtocol (0.8.0-preview.1)
- Microsoft.Extensions.* (10.0.2)
- ASP.NET Core for HTTP transport

Reference: `src/AIKit.Mcp/AIKitMcpBuilder.cs` for all options, `samples/` for usage examples.</content>
<parameter name="filePath">/Projects/AIKit.Mcp/.github/copilot-instructions.md