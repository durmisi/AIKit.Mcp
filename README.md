# AIKit.Mcp

**You focus on building business logic. AIKit handles the rest.**

A .NET wrapper library for the Model Context Protocol (MCP) SDK that simplifies MCP server configuration with fluent builder patterns. Designed to reduce complexity when building MCP servers in .NET Core, providing an intuitive API on top of the official MCP SDK.

## Features

- **Fluent Builder API**: Easy-to-use configuration with `AIKitMcpBuilder`
- **Transport Support**: Stdio and HTTP transports out of the box
- **Authentication**: OAuth 2.0, JWT Bearer, and custom authentication for HTTP transport
- **Auto-Discovery**: Automatically discovers tools, resources, and prompts from assemblies
- **Advanced MCP Features**: Tasks, progress notifications, completion, sampling, and elicitation
- **Logging**: Configurable logging with stderr redirection for clean stdio

## Architecture

AIKit.Mcp provides a clean abstraction over the MCP SDK, allowing you to focus on business logic while the framework handles protocol complexities.

```mermaid
graph TB
    %% Developer
    A[Developer]

    %% Builder Layer
    subgraph Builder["AIKit SDK / Builder"]
        B[AIKit MCP Builder]
    end

    %% Runtime Layer
    subgraph Runtime["MCP Server Runtime"]
        C[MCP Server]

        subgraph Transport["Transport Layer"]
            D1[STDIO]
            D2[HTTP]
        end

        subgraph Auth["Authentication"]
            E1[OAuth 2.0]
            E2[JWT Bearer]
            E3[Custom]
        end

        subgraph Discovery["Auto-Discovery"]
            F1[MCP Tools]
            F2[MCP Resources]
            F3[MCP Prompts]
        end

        subgraph Advanced["Advanced Features"]
            G1[Progress]
            G2[Tasks]
            G3[Sampling]
        end
    end

    %% Developer Extensions
    subgraph Extensions["Developer-Provided Extensions"]
        H[Business Logic<br/>Custom Tools<br/>Resources]
    end

    %% Flow
    A --> B
    B --> C

    C --> Transport
    C --> Auth
    C --> Discovery
    C --> Advanced

    B --> H
    H --> C

```

## Installation

Install via NuGet:

```bash
dotnet add package AIKit.Mcp
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
    mcp.WithAutoDiscovery();
});

var app = builder.Build();
await app.RunAsync();
```

### HTTP Server with Authentication

```csharp
using AIKit.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MyMcpServer";
    mcp.WithHttpTransport(opts =>
    {
        opts.HttpBasePath = "/mcp";
        opts.WithOAuth(oauth =>
        {
            oauth.OAuthClientId = "your-client-id";
            oauth.OAuthScopes = new() { "mcp:tools" };
            oauth.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidAudience = "https://your-mcp-server.com/mcp",
                ValidIssuer = "https://your-oauth-server.com"
            };
        });

        // Or JWT Bearer authentication
        // opts.WithJwtAuth(jwt =>
        // {
        //     jwt.Authority = "https://your-oauth-server.com";
        //     jwt.TokenValidationParameters = new TokenValidationParameters
        //     {
        //         ValidateIssuer = true,
        //         ValidateAudience = true,
        //         ValidateIssuerSigningKey = true,
        //         ValidAudience = "https://your-mcp-server.com/mcp",
        //         ValidIssuer = "https://your-oauth-server.com"
        //     };
        //     jwt.SigningKey = "your-secret-key"; // For symmetric key validation
        // });

        // Or custom authentication
        // opts.WithCustomAuth(custom =>
        // {
        //     custom.SchemeName = "MyAuth";
        //     custom.RegisterScheme = builder =>
        //         builder.AddScheme<AuthenticationSchemeOptions, MyAuthHandler>("MyAuth", null);
        // });
    });
    mcp.WithAutoDiscovery();
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

AIKit.Mcp supports multiple authentication methods for HTTP transport:

- `OAuthAuth`: OAuth 2.0 with client credentials and JWT Bearer token validation
- `JwtAuth`: Direct JWT Bearer token validation with symmetric key support
- `McpAuth`: MCP-specific authentication with JWT Bearer and OAuth 2.0 resource metadata
- `CustomAuth`: Custom authentication logic

All authentication methods support direct configuration of `TokenValidationParameters` for full control over JWT validation, following the same pattern as the official MCP SDK.

#### Using McpAuth

`McpAuth` provides MCP-compliant authentication that integrates JWT Bearer token validation with OAuth 2.0 protected resource metadata. This enables secure communication between MCP clients and servers following the [OAuth 2.0 Authorization Framework for Protected Resources](https://datatracker.ietf.org/doc/rfc8707/) specification.

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MyProtectedServer";
    mcp.WithHttpTransport(opts =>
    {
        opts.WithMcpAuth(mcpAuth =>
        {
            // JWT configuration - matches the official MCP SDK demo
            mcpAuth.Authority = "https://your-oauth-server.com";
            mcpAuth.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidAudience = "https://your-mcp-server.com/mcp",
                ValidIssuer = "https://your-oauth-server.com",
                NameClaimType = "name",
                RoleClaimType = "roles"
            };
            // Note: If TokenValidationParameters is not set, defaults with ValidateIssuer=true,
            // ValidateAudience=true, ValidateLifetime=true (already default), ValidateIssuerSigningKey=true are used

            // OAuth 2.0 protected resource metadata
            mcpAuth.ResourceMetadata = new ModelContextProtocol.Authentication.ProtectedResourceMetadata
            {
                ResourceDocumentation = "https://docs.example.com/api/mcp",
                AuthorizationServers = { "https://your-oauth-server.com" },
                ScopesSupported = ["mcp:tools", "mcp:resources"]
            };

            // Optional: JWT events for custom handling
            mcpAuth.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    // Custom token validation logic
                    return Task.CompletedTask;
                }
            };
        });
    });
    mcp.WithAutoDiscovery();
});
```

**Key Features of McpAuth:**

- **JWT Bearer Integration**: Configure JWT token validation directly using standard JwtBearerOptions properties
- **Resource Metadata**: Provides OAuth 2.0 protected resource information for client discovery
- **MCP Compliance**: Follows MCP authentication specifications exactly like the official SDK
- **Direct Configuration**: Set Authority, TokenValidationParameters, and Events directly for full control

**When to Use McpAuth:**

- Building production MCP servers that require secure authentication
- Following the exact same authentication pattern as the [official MCP SDK ProtectedMcpServer](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/ProtectedMcpServer/Program.cs)
- Integrating with OAuth 2.0 identity providers using JWT Bearer tokens and OAuth 2.0 resource metadata
- Supporting multiple MCP clients with standardized authentication

For a complete working example, see the [official MCP C# SDK ProtectedMcpServer sample](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/ProtectedMcpServer/Program.cs).

### Discovery

- `WithAutoDiscovery()`: Automatically discovers tools, resources, and prompts from assemblies

### Features

- `EnableCompletion`: Auto-completion
- `EnableDevelopmentFeatures`: Debug logging

### Task Management

AIKit.Mcp supports both in-memory and file-based task stores for long-running operations:

- **In-Memory Store** (default): Tasks exist only during server runtime
- **File-Based Store**: Persistent task storage with configurable TTL and session isolation

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    // Enable file-based task store with custom options
    mcp.WithFileBasedTaskStore(opts =>
    {
        opts.StoragePath = Path.Combine(AppContext.BaseDirectory, "tasks");
        opts.DefaultTtl = TimeSpan.FromHours(24);
        opts.EnableSessionIsolation = true;
        opts.FileExtension = ".task";
    });
});
```

Task store options:

- `StoragePath`: Directory for task files (default: `./tasks`)
- `DefaultTtl`: Default time-to-live for tasks (default: 1 hour)
- `EnableSessionIsolation`: Isolate tasks by session ID (default: true)
- `FileExtension`: File extension for task files (default: `.json`)

## Advanced Usage

### Task Management

Use the task helpers for background operations and polling:

```csharp
using AIKit.Mcp;

public class TaskTools
{
    private readonly IMcpTaskStore _taskStore;

    public TaskTools(IMcpTaskStore taskStore)
    {
        _taskStore = taskStore;
    }

    [McpServerTool(Name = "submit_job")]
    public async Task<string> SubmitJob(string jobType)
    {
        // Create a background task
        var task = await McpTaskHelpers.CreateTaskAsync(
            _taskStore,
            new McpTaskMetadata { TimeToLive = TimeSpan.FromHours(1) },
            new RequestId(Guid.NewGuid().ToString()),
            new JsonRpcRequest { Method = "background_job" },
            "session-id",
            async (progressToken, cancellationToken) =>
            {
                // Simulate long-running work
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(1000, cancellationToken);
                    await McpTaskHelpers.ReportProgressAsync(
                        _taskStore, progressToken, i * 10, 100, $"Processing step {i}");
                }
                return JsonSerializer.SerializeToElement(new { result = "Job completed" });
            });

        return $"Job submitted with task ID: {task.TaskId}";
    }

    [McpServerTool(Name = "poll_task")]
    public async Task<string> PollTask(string taskId)
    {
        var task = await _taskStore.GetTaskAsync(taskId, "session-id");
        if (task == null) return "Task not found";

        return task.Status switch
        {
            McpTaskStatus.Working => $"Task is running (progress: {task.Progress?.Percentage ?? 0}%)",
            McpTaskStatus.Completed => $"Task completed: {await _taskStore.GetTaskResultAsync(taskId, "session-id")}",
            McpTaskStatus.Cancelled => "Task was cancelled",
            _ => "Task status unknown"
        };
    }
}
```

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
