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
    mcp.WithAutoDiscovery();
    // Resources and prompts auto-discovery enabled by default
});

var app = builder.Build();
app.UseAIKitMcp("/mcp");
await app.RunAsync();
```

## Authentication

AIKit.Mcp supports OAuth 2.0, JWT Bearer, and custom authentication schemes. Configure authentication using the fluent API:

```csharp
builder.Services.AddAIKitMcp(mcp =>
{
    mcp.ServerName = "MyMcpServer";
    mcp.ServerVersion = "1.0.0";

    mcp.WithHttpTransport(opts =>
    {
        opts.HttpBasePath = "/mcp";

        // JWT Bearer authentication
        opts.WithJwtAuth(jwt =>
        {
            jwt.JwtIssuer = "your-issuer";
            jwt.JwtAudience = "your-audience";
            jwt.TokenValidationParameters = new TokenValidationParameters
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

        // Or MCP-specific authentication with JWT and OAuth 2.0 resource metadata
        // opts.WithMcpAuth(mcpAuth =>
        // {
        //     mcpAuth.Authority = "https://your-oauth-server.com";
        //     mcpAuth.TokenValidationParameters = new TokenValidationParameters
        //     {
        //         ValidateIssuer = true,
        //         ValidateAudience = true,
        //         ValidateIssuerSigningKey = true,
        //         ValidAudience = "https://your-mcp-server.com/mcp",
        //         ValidIssuer = "https://your-oauth-server.com",
        //         NameClaimType = "name",
        //         RoleClaimType = "roles"
        //     };
        //     mcpAuth.ResourceMetadata = new ModelContextProtocol.Authentication.ProtectedResourceMetadata
        //     {
        //         AuthorizationServers = { "https://your-oauth-server.com" },
        //         ScopesSupported = ["mcp:tools"]
        //     };
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
```

## Features

- **Fluent Builder API**: Intuitive configuration with `AIKitMcpBuilder`
- **Multiple Transports**: STDIO and HTTP support
- **Authentication**: OAuth 2.0, JWT Bearer, MCP-specific auth, and custom auth
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
