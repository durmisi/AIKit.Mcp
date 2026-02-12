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
    protected async Task<WebApplication> StartMcpServerAsync(string path = "")
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:5000");
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = OAuthServerUrl;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidAudience = McpServerUrl + path,
                ValidIssuer = OAuthServerUrl,
                NameClaimType = "name",
                RoleClaimType = "roles"
            };
        });

        builder.Services.AddAuthorization();
        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "TestOAuthServer";
            mcp.WithHttpTransport(opts => { });
        });

        var app = builder.Build();
        app.MapMcp(path).RequireAuthorization();
        await app.StartAsync(TestCts.Token);
        return app;
    }
}