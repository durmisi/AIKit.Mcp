# AIKit.Mcp

**You focus on building business logic. AIKit handles the rest.**

A .NET wrapper library for the Model Context Protocol (MCP) SDK that simplifies MCP server configuration with fluent builder patterns. Designed to reduce complexity when building MCP servers in .NET Core, providing an intuitive API on top of the official MCP SDK.

## Features

- **Fluent Builder API**: Easy-to-use configuration with `AIKitMcpBuilder`
- **Transport Support**: Stdio and HTTP transports out of the box
- **Authentication**: OAuth 2.0, JWT Bearer, MCP-specific, and custom authentication for HTTP transport
- **Per-Session Tools**: Dynamically filter and expose tools based on session context (e.g., route parameters)
- **Auto-Discovery**: Automatically discovers tools, resources, and prompts from assemblies
- **Advanced MCP Features**: Tasks, progress notifications, completion, sampling, and elicitation
- **Logging**: Configurable logging with stderr redirection for clean stdio

## Architecture

AIKit.Mcp is a **abstraction layer** built on top of the [official Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk). It simplifies MCP server development while maintaining full compatibility with the MCP specification.

### Relationship to Official MCP SDK

- **What AIKit.Mcp provides**: Fluent configuration APIs, auto-discovery, simplified authentication, and developer-friendly patterns
- **What the official SDK provides**: Low-level MCP protocol implementation, core types, and protocol primitives
- **When to use official SDK docs**: For understanding MCP protocol concepts, advanced customization, or when AIKit.Mcp abstractions don't meet your needs

### Core Components

- **AIKitMcpBuilder**: Fluent API for configuring your MCP server (wraps MCP SDK configuration)
- **Transports**: Handle communication (Stdio for CLI clients, HTTP for web clients) using MCP SDK transports
- **Authentication**: Secure your HTTP endpoints with various auth methods (extends MCP SDK authentication)
- **Auto-Discovery**: Automatically finds and registers your tools, resources, and prompts (simplifies MCP SDK registration)
- **MCP Server**: The runtime that handles MCP protocol communication (powered by official MCP SDK)

### Development Flow

1. **Configure**: Use `AIKitMcpBuilder` to set up your server (vs manual MCP SDK setup)
2. **Implement**: Create tools, resources, and prompts with simple attributes (vs MCP SDK protocol types)
3. **Register**: Call `WithAutoDiscovery()` to automatically register your components (vs manual registration)
4. **Run**: Start your server with `app.RunAsync()` (same as MCP SDK)

### Key Abstractions

The library handles MCP protocol complexities so you can focus on business logic. Your code is automatically mapped to MCP concepts:

- Classes with `[McpServerToolType]` → MCP Tools (see [MCP Tool specification](https://modelcontextprotocol.io/specification/2024-11-05/server/tools))
- Classes with `[McpServerResourceType]` → MCP Resources (see [MCP Resource specification](https://modelcontextprotocol.io/specification/2024-11-05/server/resources))
- Prompt classes registered via `WithPrompts<T>()` → MCP Prompts (see [MCP Prompt specification](https://modelcontextprotocol.io/specification/2024-11-05/server/prompts))

### When to Consult Official Documentation

For advanced topics, refer to the [official MCP C# SDK documentation](https://github.com/modelcontextprotocol/csharp-sdk):

- **MCP Protocol Details**: Understanding the full MCP specification and protocol flow
- **Custom Transport Implementation**: Building transports beyond stdio/HTTP
- **Advanced Authentication**: Custom auth schemes beyond the provided options
- **Protocol Extensions**: Implementing custom MCP message types or extensions
- **Performance Tuning**: Low-level optimization of MCP communication
- **Debugging Protocol Issues**: Deep troubleshooting of MCP message flow

## Installation

Install via NuGet:

```bash
dotnet add package AIKit.Mcp
```

## Quick Start

### Basic Stdio Server

Create a simple MCP server that runs via stdio for command-line MCP clients:

```csharp
using AIKit.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MyMcpServer";
    mcp.ServerVersion = "1.0.0";
    mcp.WithStdioTransport();
    mcp.WithAutoDiscovery(); // Automatically finds tools, resources, and prompts
});

var app = builder.Build();
await app.RunAsync();
```

### HTTP Server with JWT Authentication

Create an HTTP-based MCP server with JWT authentication:

```csharp
using AIKit.Mcp;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MySecureMcpServer";
    mcp.ServerVersion = "1.0.0";

    mcp.WithHttpTransport(opts =>
    {
        opts.HttpBasePath = "/mcp";
        opts.WithJwtAuth(jwt =>
        {
            jwt.JwtIssuer = "https://your-issuer.com";
            jwt.JwtAudience = "your-mcp-server";
            jwt.SigningKey = "your-256-bit-secret-key-here"; // Use a secure key in production
        });
    });

    mcp.WithAutoDiscovery();
});

var app = builder.Build();
app.UseAIKitMcp("/mcp");
await app.RunAsync();
```

### Adding Resources and Prompts

MCP servers can provide resources (data) and prompts (reusable prompt templates) in addition to tools.

**Resources Example:**

```csharp
[McpServerResourceType]
public class DataResources
{
    [McpServerResource(Name = "config",
                      Description = "Application configuration data",
                      MimeType = "application/json")]
    public async Task<string> GetConfig()
    {
        return JsonSerializer.Serialize(new { version = "1.0", environment = "prod" });
    }
}
```

**Prompts Example:**

```csharp
[McpServerPromptType]
public class AnalysisPrompts
{
    [McpServerPrompt(Name = "analyze_data",
                    Description = "Prompt for data analysis tasks")]
    public string AnalyzeDataPrompt(string dataType)
    {
        return $"Please analyze the following {dataType} data and provide insights...";
    }
}
```

Both resources and prompts are automatically discovered when you call `WithAutoDiscovery()`.

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

AIKit.Mcp supports multiple authentication methods for HTTP transport. Choose based on your security requirements and integration needs.

#### Quick Authentication Setup

**For Development (Simple JWT):**

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MySecureMcpServer";
    mcp.ServerVersion = "1.0.0";

    mcp.WithHttpTransport(opts =>
    {
        opts.HttpBasePath = "/mcp";

        opts.WithJwtAuth(jwt =>
        {
            jwt.JwtIssuer = "dev-issuer";
            jwt.JwtAudience = "dev-audience";
            jwt.SigningKey = "your-dev-signing-key-min-32-chars";
        });
    });
});
```

**For Production (OAuth 2.0):**

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MySecureMcpServer";
    mcp.ServerVersion = "1.0.0";

    mcp.WithHttpTransport(opts =>
    {
        opts.HttpBasePath = "/mcp";

        opts.WithJwtAuth(jwt =>
        {
            jwt.JwtIssuer = "https://login.microsoftonline.com/your-tenant/v2.0";
            jwt.JwtAudience = Environment.GetEnvironmentVariable("OAUTH_CLIENT_ID");
        });
    });
});
```

#### Using JwtAuth

Direct JWT Bearer authentication validates JWT tokens without the OAuth 2.0 flow. This is useful when you have pre-issued JWT tokens or want to integrate with systems that provide JWT tokens directly.

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MyJwtServer";
    mcp.WithHttpTransport(opts =>
    {
        opts.WithJwtAuth(jwt =>
        {
            jwt.JwtIssuer = "your-issuer";
            jwt.JwtAudience = "your-audience";
            jwt.SigningKey = "your-symmetric-signing-key";

            // Or use authority for asymmetric keys:
            // jwt.Authority = "https://your-authority.com";

            // Optional: Custom token validation
            jwt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidAudience = "your-audience",
                ValidIssuer = "your-issuer"
            };

            // Optional: JWT events for custom handling
            jwt.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    // Custom validation logic
                    return Task.CompletedTask;
                }
            };
        });
    });
    mcp.WithAutoDiscovery();
});
```

**Key Features of JwtAuth:**

- **Direct JWT Validation**: No OAuth flow required
- **Symmetric/Asymmetric Keys**: Support for both HMAC and RSA/ECDSA signing
- **Authority Support**: Automatic key resolution from OAuth authorities
- **Simple Configuration**: Minimal setup for basic JWT validation

**When to Use JwtAuth:**

- Working with pre-issued JWT tokens
- Internal services with direct token issuance
- Simple authentication scenarios without OAuth complexity
- Integration with JWT-based authentication systems

#### Using CustomAuth

Custom authentication allows you to implement any authentication scheme by providing your own authentication handler. This gives maximum flexibility for proprietary or specialized authentication requirements.

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MyCustomAuthServer";
    mcp.WithHttpTransport(opts =>
    {
        opts.WithCustomAuth(custom =>
        {
            custom.SchemeName = "ApiKey";
            custom.RegisterScheme = builder =>
            {
                builder.AddScheme<ApiKeyOptions, ApiKeyHandler>("ApiKey", options => { });
            };
        });
    });
    mcp.WithAutoDiscovery();
});
```

Where `ApiKeyHandler` is your custom implementation:

```csharp
public class ApiKeyHandler : AuthenticationHandler<ApiKeyOptions>
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKey = Request.Headers["X-API-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey) || apiKey != "valid-key")
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        var identity = new ClaimsIdentity(Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}

public class ApiKeyOptions : AuthenticationSchemeOptions { }
```

**Key Features of CustomAuth:**

- **Full Flexibility**: Implement any authentication logic
- **ASP.NET Core Integration**: Uses standard authentication handler pattern
- **Custom Schemes**: Support for proprietary authentication methods
- **Handler Pattern**: Leverage ASP.NET Core's authentication infrastructure

**When to Use CustomAuth:**

- Proprietary authentication schemes
- API key authentication
- Custom token formats
- Integration with legacy authentication systems
- Specialized security requirements

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

### Per-Session Tools

Per-session tools allow you to dynamically filter and expose different sets of tools based on session context, such as HTTP route parameters, headers, or other request-specific data. This is useful for multi-tenant applications, role-based access control, or context-aware tool availability.

#### Basic Setup

First, create tool classes with categories:

```csharp
[McpServerToolType]
public class ClockTool
{
    [McpServerTool(Name = "get_current_time", Description = "Get the current time")]
    public string GetCurrentTime() => DateTime.Now.ToString("HH:mm:ss");
}

[McpServerToolType]
public class CalculatorTool
{
    [McpServerTool(Name = "add_numbers", Description = "Add two numbers")]
    public double Add(double a, double b) => a + b;
}

[McpServerToolType]
public class UserInfoTool
{
    [McpServerTool(Name = "get_user_info", Description = "Get user information")]
    public string GetUserInfo() => "User: admin, Role: administrator";
}
```

#### Configuration

Register tools with categories and enable session-based filtering:

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MySessionServer";
    mcp.ServerVersion = "1.0.0";

    mcp.WithHttpTransport(opts =>
    {
        opts.HttpBasePath = "/mcp";
        // Optional: Configure session options
        opts.WithSessionOptions(sessionOpts =>
        {
            sessionOpts.RouteParameterName = "category"; // Default is "category"
        });
    });

    // Register tools with categories
    mcp.WithTools<ClockTool>("time");
    mcp.WithTools<CalculatorTool>("math");
    mcp.WithTools<UserInfoTool>("admin");

    // Enable auto-discovery for non-categorized tools if needed
    mcp.WithAutoDiscovery();
});
```

#### Session-Based Filtering

Tools are filtered based on the session context. For HTTP transport, this typically uses route parameters:

```csharp
// Route: /mcp/time/* - Only time-related tools available
app.MapGet("/mcp/time/{sessionId}", ...);

// Route: /mcp/math/* - Only math-related tools available
app.MapGet("/mcp/math/{sessionId}", ...);

// Route: /mcp/admin/* - Only admin-related tools available
app.MapGet("/mcp/admin/{sessionId}", ...);
```

#### Custom Session Context

For more advanced scenarios, you can customize how session context is determined:

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.WithHttpTransport(opts =>
    {
        opts.WithSessionOptions(sessionOpts =>
        {
            // Custom logic to extract session context from HTTP context
            sessionOpts.GetSessionContext = httpContext =>
            {
                // Example: Use user role from claims
                var userRole = httpContext.User.FindFirst("role")?.Value ?? "guest";
                return userRole switch
                {
                    "admin" => "admin",
                    "user" => "basic",
                    _ => "guest"
                };
            };
        });
    });

    // Register tools for different roles
    mcp.WithTools<AdminTools>("admin");
    mcp.WithTools<UserTools>("basic");
    mcp.WithTools<GuestTools>("guest");
});
```

#### Key Features

- **Dynamic Filtering**: Tools are filtered at runtime based on session context
- **Category-Based Registration**: Register tool types with string categories
- **HTTP Route Integration**: Automatic filtering based on route parameters
- **Custom Context Logic**: Implement custom session context extraction
- **Backward Compatibility**: Non-categorized tools still work with auto-discovery

#### Use Cases

- **Multi-Tenant Applications**: Different tools for different tenants
- **Role-Based Access**: Admin tools vs. user tools vs. guest tools
- **Feature Flags**: Enable/disable tool sets based on configuration
- **Context-Aware Services**: Tools that vary by user preferences or session state

## Building and Testing

```bash
# Build all projects
dotnet build src/AIKit.Mcp.slnx

# Run tests
dotnet test src/AIKit.Mcp.Tests/AIKit.Mcp.Tests.csproj
```

## Troubleshooting

### Common Issues

**"No tools found" error**

- Ensure your tool classes are decorated with `[McpServerToolType]`
- Check that tool methods have `[McpServerTool(Name = "...")]` attributes
- Verify `WithAutoDiscovery()` is called in your configuration

**Authentication failures**

- For JWT: Verify issuer, audience, and signing key match your token
- For OAuth: Check client ID, scopes, and token validation parameters
- Ensure tokens are sent in `Authorization: Bearer <token>` header

**HTTP transport not working**

- Verify `app.UseAIKitMcp("/mcp")` is called in your middleware pipeline
- Check that the base path matches your `HttpBasePath` configuration
- Ensure authentication is properly configured for HTTP transport

**Stdio transport issues**

- Stdio servers should be run directly by MCP clients
- Check logging output for connection errors
- Verify the server starts without exceptions

### Debug Logging

Enable detailed logging to troubleshoot issues:

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.EnableDevelopmentFeatures = true;
    // This enables debug logging and additional error details
});
```

### Testing Your Server

Use the MCP Inspector to test your server:

```bash
# Install MCP CLI if not already installed
npm install -g @modelcontextprotocol/cli

# Test stdio server
mcp dev --stdio "dotnet run --project YourProject.csproj"

# Test HTTP server
mcp dev --http "http://localhost:5000/mcp"
```

## Dependencies

- ModelContextProtocol (>= 0.8.0-preview.1)
- Microsoft.Extensions.Logging (>= 10.0.2)
- Microsoft.AspNetCore.Authentication.JwtBearer (>= 10.0.2) [for HTTP auth]

## Contributing

Contributions welcome! Please see issues for current tasks.

## Best Practices

### Tool Design

- **Descriptive Names**: Use clear, descriptive names for tools and parameters
- **Input Validation**: Always validate inputs and provide meaningful error messages
- **Async Operations**: Use async methods for I/O operations and long-running tasks
- **Progress Reporting**: For operations >5 seconds, implement progress reporting

```csharp
[McpServerToolType]
public class DataProcessingTools
{
    [McpServerTool(Name = "process_large_dataset",
                   Description = "Process a large dataset with progress reporting")]
    public async Task<string> ProcessLargeDataset(string datasetId, McpServer server, ProgressToken token)
    {
        // Validate input
        if (string.IsNullOrEmpty(datasetId))
            throw new ArgumentException("Dataset ID is required");

        // Report progress
        for (int i = 0; i < 100; i += 10)
        {
            await McpTaskHelpers.ReportProgressAsync(server, token, i, 100, $"Processing batch {i/10}");
            await Task.Delay(100); // Simulate work
        }

        return $"Dataset {datasetId} processed successfully";
    }
}
```

### Configuration Management

- **Environment Variables**: Store sensitive configuration in environment variables
- **Validation**: Validate configuration at startup
- **Logging**: Enable appropriate log levels for different environments

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = Environment.GetEnvironmentVariable("MCP_SERVER_NAME") ?? "MyServer";

    // Validate required configuration
    var jwtKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
    if (string.IsNullOrEmpty(jwtKey))
        throw new InvalidOperationException("JWT_SIGNING_KEY environment variable is required");

    mcp.WithHttpTransport(opts =>
    {
        opts.WithJwtAuth(jwt =>
        {
            jwt.JwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
            jwt.JwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
            jwt.SigningKey = jwtKey;
        });
    });
});
```

## License

MIT License - see LICENSE file for details.

## Roadmap

- [ ] NuGet package release
- [ ] Additional authentication providers
- [ ] WebSocket transport support
- [ ] Performance optimizations
