# AIKit.Mcp

**You focus on building business logic. AIKit.Mcp handles the rest.**

A .NET wrapper library for the Model Context Protocol (MCP) SDK that simplifies MCP server configuration with fluent builder patterns. Designed to reduce complexity when building MCP servers in .NET Core, providing an intuitive API on top of the official MCP SDK.

## Features

- **Fluent Builder API**: Easy-to-use configuration with `AIKitMcpBuilder`
- **Transport Support**: Stdio and HTTP transports out of the box
- **Authentication**: OAuth 2.0, JWT Bearer, and custom authentication for HTTP transport
- **Auto-Discovery**: Automatically discovers tools, resources, and prompts from assemblies
- **Advanced MCP Features**: Tasks, progress notifications, completion, sampling, and elicitation
- **Multi-Framework**: Supports .NET 6.0, 8.0, 9.0, and 10.0
- **Logging**: Configurable logging with stderr redirection for clean stdio

## Architecture

AIKit.Mcp provides a clean abstraction over the MCP SDK, allowing you to focus on business logic while the framework handles protocol complexities.

```mermaid
graph TB
    A[Developer] --> B[AIKit Mcp Builder]
    B --> C[MCP Server]

    C --> D[Transport Layer]
    D --> D1[Stdio]
    D --> D2[HTTP]

    C --> E[Authentication]
    E --> E1[OAuth 2.0]
    E --> E2[JWT Bearer]
    E --> E3[Custom]

    C --> F[Auto-Discovery]
    F --> F1[Tools<br/>[McpServerTool]]
    F --> F2[Resources<br/>[McpServerResource]]
    F --> F3[Prompts<br/>[McpServerPrompt]]

    C --> G[Advanced Features]
    G --> G1[Progress]
    G --> G2[Tasks]
    G --> G3[Sampling]

    B --> H[Business Logic, Your Tools, Resources]
```

## Installation

Install via NuGet:

```bash
dotnet add package AIKit.Mcp --version 1.0.0-preview.1
```

For HTTP transport support, also install:

```bash
dotnet add package ModelContextProtocol.AspNetCore --version 0.8.0-preview.1
```

## Quick Start

### Basic Stdio Server

```csharp
using AIKit.Mcp;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MyMcpServer";
    mcp.WithStdioTransport();
    mcp.AutoDiscoverTools = true;
});

var app = builder.Build();
await app.RunAsync();
```

### HTTP Server with Authentication

```csharp
using AIKit.Mcp;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MyMcpServer";
    mcp.WithHttpTransport(opts =>
    {
        opts.HttpBasePath = "/mcp";
        opts.Authentication = new OAuthAuth
        {
            OAuthClientId = "your-client-id",
            OAuthScopes = new() { "mcp:tools" }
        };
    });
    mcp.AutoDiscoverTools = true;
    mcp.EnableProgress = true;
});

var app = builder.Build();
await app.RunAsync();
```

## Tool Implementation

Create tools by decorating classes and methods:

```csharp
using ModelContextProtocol.Server;

[McpServerToolType]
public class MathTools
{
    [McpServerTool(Name = "add")]
    public double Add(double a, double b) => a + b;

    [McpServerTool(Name = "multiply")]
    public double Multiply(double a, double b) => a * b;
}
```

## Configuration Options

### Transport

- `WithStdioTransport()`: For command-line MCP clients
- `WithHttpTransport(Action<HttpTransportOptions>)`: For HTTP-based MCP communication

### Authentication (HTTP only)

- `OAuthAuth`: OAuth 2.0 with client credentials
- `JwtAuth`: JWT Bearer token validation
- `CustomAuth`: Custom authentication logic

### Discovery

- `AutoDiscoverTools`: Scan assembly for `[McpServerTool]` methods
- `AutoDiscoverResources`: Scan for `[McpServerResource]` classes
- `AutoDiscoverPrompts`: Scan for `[McpServerPrompt]` classes

### Features

- `EnableProgress`: Progress notifications
- `EnableCompletion`: Auto-completion
- `EnableSampling`: LLM sampling
- `EnableDevelopmentFeatures`: Debug logging

## Advanced Usage

### Progress Reporting

```csharp
using AIKit.Mcp;

public class LongRunningTool
{
    [McpServerTool(Name = "long_task")]
    public async Task<string> LongTask(McpServer server, ProgressToken token)
    {
        for (int i = 0; i < 100; i++)
        {
            await McpTaskHelpers.ReportProgressAsync(server, token, i, 100, $"Step {i}");
            await Task.Delay(100);
        }
        return "Completed";
    }
}
```

### Message Filters

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.MessageFilter = () => next => async (context, ct) =>
    {
        Console.WriteLine($"Incoming: {context.JsonRpcMessage.Method}");
        await next(context, ct);
        Console.WriteLine("Message processed");
    };
});
```

## Sample Application

See `samples/AIKit.Mcp.Sample/` for a complete example with:

- Math tools
- File system resources
- Conversation prompts
- HTTP transport with OAuth

Run the sample:

```bash
dotnet run --project samples/AIKit.Mcp.Sample/AIKit.Mcp.Sample.csproj
```

## Building and Testing

```bash
# Build all projects
dotnet build src/AIKit.Mcp.slnx

# Run tests
dotnet test src/AIKit.Mcp.Tests/AIKit.Mcp.Tests.csproj
```

## Dependencies

- ModelContextProtocol (>= 0.8.0-preview.1)
- Microsoft.Extensions.Logging (>= 10.0.2)
- Microsoft.AspNetCore.Authentication.JwtBearer (>= 10.0.2) [for HTTP auth]

## Contributing

Contributions welcome! Please see issues for current tasks.

## License

MIT License - see LICENSE file for details.

## Roadmap

- [ ] NuGet package release
- [ ] Additional authentication providers
- [ ] WebSocket transport support
- [ ] Performance optimizations
