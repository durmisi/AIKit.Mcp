using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using Xunit.Abstractions;

namespace AIKit.Mcp.Tests;

[Collection("Integration")]
public class JwtAuthIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public JwtAuthIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Server_With_JwtAuth_Accepts_Valid_Token()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Test ===");
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
                opts.WithJwtAuth(jwt =>
                {
                    jwt.JwtIssuer = "test-issuer";
                    jwt.JwtAudience = "test-audience";
                    jwt.SigningKey = "super-secret-key-for-jwt-testing-123456789";
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
            var token = JwtTestHelper.GenerateValidToken();
            _output.WriteLine($"Generated valid JWT token for testing");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            _output.WriteLine("Sending authenticated request to /mcp");

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
    public async Task Server_With_JwtAuth_Rejects_Invalid_Issuer()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Invalid Issuer Test ===");
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
                opts.WithJwtAuth(jwt =>
                {
                    jwt.JwtIssuer = "test-issuer";
                    jwt.JwtAudience = "test-audience";
                    jwt.SigningKey = "super-secret-key-for-jwt-testing-123456789";
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
            var token = JwtTestHelper.GenerateTokenWithWrongIssuer();
            _output.WriteLine($"Generated JWT token with wrong issuer for testing");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            _output.WriteLine("Sending request with invalid issuer token to /mcp");

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

    [Fact]
    public async Task Server_With_JwtAuth_Rejects_Expired_Token()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Expired Token Test ===");
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
                opts.WithJwtAuth(jwt =>
                {
                    jwt.JwtIssuer = "test-issuer";
                    jwt.JwtAudience = "test-audience";
                    jwt.SigningKey = "super-secret-key-for-jwt-testing-123456789";
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
            var token = JwtTestHelper.GenerateExpiredToken();
            _output.WriteLine($"Generated expired JWT token for testing");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            _output.WriteLine("Sending request with expired token to /mcp");

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

    [Fact]
    public async Task Server_With_JwtAuth_Rejects_Missing_Token()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Missing Token Test ===");
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
                opts.WithJwtAuth(jwt =>
                {
                    jwt.JwtIssuer = "test-issuer";
                    jwt.JwtAudience = "test-audience";
                    jwt.SigningKey = "super-secret-key-for-jwt-testing-123456789";
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
            // No Authorization header
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            _output.WriteLine("Sending unauthenticated request to /mcp");

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

    [Fact]
    public async Task Server_With_JwtAuth_Rejects_Wrong_Audience()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Wrong Audience Test ===");
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
                opts.WithJwtAuth(jwt =>
                {
                    jwt.JwtIssuer = "test-issuer";
                    jwt.JwtAudience = "test-audience";
                    jwt.SigningKey = "super-secret-key-for-jwt-testing-123456789";
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
            var token = JwtTestHelper.GenerateTokenWithWrongAudience();
            _output.WriteLine($"Generated JWT token with wrong audience for testing");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            _output.WriteLine("Sending request with wrong audience token to /mcp");

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

    [Fact]
    public async Task Server_With_JwtAuth_Rejects_Tampered_Token()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Tampered Token Test ===");
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
                opts.WithJwtAuth(jwt =>
                {
                    jwt.JwtIssuer = "test-issuer";
                    jwt.JwtAudience = "test-audience";
                    jwt.SigningKey = "super-secret-key-for-jwt-testing-123456789";
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
            var token = JwtTestHelper.GenerateTokenWithWrongKey();
            _output.WriteLine($"Generated JWT token with wrong key for testing");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            _output.WriteLine("Sending request with tampered token to /mcp");

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

    [Fact]
    public async Task Server_With_JwtAuth_Rejects_Malformed_Token()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Malformed Token Test ===");
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
                opts.WithJwtAuth(jwt =>
                {
                    jwt.JwtIssuer = "test-issuer";
                    jwt.JwtAudience = "test-audience";
                    jwt.SigningKey = "super-secret-key-for-jwt-testing-123456789";
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
            var token = JwtTestHelper.GenerateMalformedToken();
            _output.WriteLine($"Generated malformed JWT token for testing");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            _output.WriteLine("Sending request with malformed token to /mcp");

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
