using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using AIKit.Mcp;
using ModelContextProtocol.TestOAuthServer;
using System.Net;
using Xunit.Abstractions;

namespace AIKit.Mcp.Tests;

/// <summary>
/// Base class for OAuth integration tests.
/// Sets up an in-memory OAuth server and MCP server with JWT authentication.
/// </summary>
public abstract class OAuthTestBase : IAsyncLifetime
{
    protected const string McpServerUrl = "http://localhost:5000";
    protected const string OAuthServerUrl = "https://localhost:7029";

    protected readonly CancellationTokenSource TestCts = new();
    protected readonly Program TestOAuthServer;
    private readonly Task _testOAuthRunTask;

    protected readonly ITestOutputHelper _output;

    protected OAuthTestBase(ITestOutputHelper outputHelper)
    {
        _output = outputHelper;
        // Instantiate the OAuth server
        TestOAuthServer = new Program();
        _testOAuthRunTask = TestOAuthServer.RunServerAsync(cancellationToken: TestCts.Token);
    }

    public async Task InitializeAsync()
    {
        // Wait for the OAuth server to be ready
        await TestOAuthServer.ServerStarted.WaitAsync(TestCts.Token);
    }

    public async Task DisposeAsync()
    {
        TestCts.Cancel();
        try
        {
            await _testOAuthRunTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        finally
        {
            TestCts.Dispose();
        }
    }

    /// <summary>
    /// Starts the MCP server with OAuth protection.
    /// </summary>
    protected async Task<WebApplication> StartMcpServerAsync(string path = "/mcp")
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:5000");

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "TestOAuthServer";
            mcp.WithHttpTransport(opts => {
                opts.Authentication = new OAuthAuth()
                {
                    OAuthClientId = "test-client",
                    Authority = OAuthServerUrl,
                    JwtAudience = McpServerUrl + path,
                    JwtIssuer = OAuthServerUrl,
                    TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "name",
                        RoleClaimType = "roles"
                    }
                };
            });
        });

        var app = builder.Build();
        app.UseAIKitMcp(path);
        await app.StartAsync(TestCts.Token);
        return app;
    }
}