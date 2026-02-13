using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xunit.Abstractions;

namespace AIKit.Mcp.Tests;

[Collection("Integration")]
public class CustomAuthIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public CustomAuthIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private class TestCustomAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestCustomAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var httpContext = Context;
            var apiKey = httpContext.Request.Headers["X-API-Key"].FirstOrDefault();
            Logger.LogInformation($"Auth handler called, API key: {apiKey}");
            if (apiKey != "valid-key")
            {
                Logger.LogInformation("Invalid API key, failing auth");
                return AuthenticateResult.Fail("Invalid API key");
            }
            Logger.LogInformation("Valid API key, succeeding auth");
            var identity = new ClaimsIdentity(Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
    }

    [Fact]
    public async Task Server_With_CustomAuth_Accepts_Valid_Request()
    {
        _output.WriteLine("=== Starting MCP Server Custom Auth Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        // Use random port to avoid conflicts in parallel tests
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
        });

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
                opts.WithCustomAuth(custom =>
                {
                    custom.SchemeName = "Custom";
                    custom.RegisterScheme = b => b.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestCustomAuthHandler>("Custom", o => { });
                });
            });


            mcp.EnableCompletion = true;

            mcp.EnableDevelopmentFeatures = true;

        });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseAIKitMcp("/mcp");

        await app.StartAsync();

        try
        {
            var url = app.Urls.First();
            _output.WriteLine($"Server started on URL: {url}");
            using var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Add("X-API-Key", "valid-key");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            var response = await client.PostAsJsonAsync("/mcp", new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/list",
                @params = new { }
            });

            _output.WriteLine($"Response status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Server_With_CustomAuth_Rejects_Invalid_Request()
    {
        _output.WriteLine("=== Starting MCP Server Custom Auth Rejection Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        // Use random port to avoid conflicts in parallel tests
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
        });

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
                opts.WithCustomAuth(custom =>
                {
                    custom.SchemeName = "Custom";
                    custom.RegisterScheme = b => b.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestCustomAuthHandler>("Custom", o => { });
                });
            });


            mcp.EnableCompletion = true;

            mcp.EnableDevelopmentFeatures = true;

        });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseAIKitMcp("/mcp");

        await app.StartAsync();

        try
        {
            var url = app.Urls.First();
            _output.WriteLine($"Server started on URL: {url}");
            using var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Add("X-API-Key", "invalid-key");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            var response = await client.PostAsJsonAsync("/mcp", new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/list",
                @params = new { }
            });

            _output.WriteLine($"Response status: {response.StatusCode}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
