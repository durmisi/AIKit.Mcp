# AIKit.Mcp

A .NET wrapper library for the Model Context Protocol (MCP) SDK that simplifies MCP server configuration with fluent builder patterns and helper classes.

## Installation

```bash
dotnet add package AIKit.Mcp
```

## Quick Start

```csharp
using AIKit.Mcp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MyMcpServer";
    mcp.ServerVersion = "1.0.0";

    // Configure transport
    mcp.WithHttpTransport(opts =>
    {
        opts.HttpBasePath = "/mcp";
    });

    // Auto-discover tools, resources, and prompts
    mcp.AutoDiscoverTools = true;
    mcp.AutoDiscoverResources = true;
    mcp.AutoDiscoverPrompts = true;
});

var app = builder.Build();
app.UseAIKitMcp("/mcp");
await app.RunAsync();
```

## Features

- **Fluent Builder API**: Intuitive configuration with `AIKitMcpBuilder`
- **Multiple Transports**: STDIO and HTTP support
- **Authentication**: OAuth 2.0, JWT Bearer, and custom auth
- **Auto-Discovery**: Automatic registration of MCP tools, resources, and prompts
- **Advanced Features**: Tasks, progress, completion, sampling, and elicitation helpers
- **Logging**: Integrated logging with clean output

## Documentation

For detailed documentation, examples, and API reference, visit the [GitHub repository](https://github.com/durmisi/AIKit.Mcp).

## License

MIT

- [ ] NuGet package release
- [ ] Additional authentication providers
- [ ] WebSocket transport support
- [ ] Performance optimizations
