using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Xunit.Abstractions;

namespace AIKit.Mcp.Tests;

public class JwtAuthIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public JwtAuthIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static class JwtTestHelper
    {
        private const string Issuer = "test-issuer";
        private const string Audience = "test-audience";
        private static readonly SymmetricSecurityKey SecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("super-secret-key-for-jwt-testing-123456789"));

        public static string GenerateValidToken()
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static string GenerateTokenWithWrongIssuer()
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: "wrong-issuer",
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static string GenerateExpiredToken()
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(-1),
                signingCredentials: new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    [Fact]
    public async Task Server_With_JwtAuth_Accepts_Valid_Token()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
                opts.Authentication = new JwtAuth
                {
                    JwtIssuer = "test-issuer",
                    JwtAudience = "test-audience",
                    SigningKey = "super-secret-key-for-jwt-testing-123456789"
                };
            });

            mcp.AutoDiscoverPrompts = false;
            mcp.EnableProgress = true;
            mcp.EnableCompletion = true;
            mcp.EnableSampling = false;
            mcp.EnableDevelopmentFeatures = true;
            mcp.EnableValidation = true;
        });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseRouting();

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapMcp("/mcp").RequireAuthorization();

        await app.StartAsync();

        try
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:5000");
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
        }
    }

    [Fact]
    public async Task Server_With_JwtAuth_Rejects_Invalid_Issuer()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Invalid Issuer Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
                opts.Authentication = new JwtAuth
                {
                    JwtIssuer = "test-issuer",
                    JwtAudience = "test-audience",
                    SigningKey = "super-secret-key-for-jwt-testing-123456789"
                };
            });

            mcp.AutoDiscoverPrompts = false;
            mcp.EnableProgress = true;
            mcp.EnableCompletion = true;
            mcp.EnableSampling = false;
            mcp.EnableDevelopmentFeatures = true;
            mcp.EnableValidation = true;
        });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseRouting();

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapMcp("/mcp").RequireAuthorization();

        await app.StartAsync();

        try
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:5000");
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
        }
    }

    [Fact]
    public async Task Server_With_JwtAuth_Rejects_Expired_Token()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Expired Token Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
                opts.Authentication = new JwtAuth
                {
                    JwtIssuer = "test-issuer",
                    JwtAudience = "test-audience",
                    SigningKey = "super-secret-key-for-jwt-testing-123456789"
                };
            });

            mcp.AutoDiscoverPrompts = false;
            mcp.EnableProgress = true;
            mcp.EnableCompletion = true;
            mcp.EnableSampling = false;
            mcp.EnableDevelopmentFeatures = true;
            mcp.EnableValidation = true;
        });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseRouting();

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapMcp("/mcp").RequireAuthorization();

        await app.StartAsync();

        try
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:5000");
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
        }
    }

    [Fact]
    public async Task Server_With_JwtAuth_Rejects_Missing_Token()
    {
        _output.WriteLine("=== Starting MCP Server JWT Auth Missing Token Test ===");
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
                opts.Authentication = new JwtAuth
                {
                    JwtIssuer = "test-issuer",
                    JwtAudience = "test-audience",
                    SigningKey = "super-secret-key-for-jwt-testing-123456789"
                };
            });

            mcp.AutoDiscoverPrompts = false;
            mcp.EnableProgress = true;
            mcp.EnableCompletion = true;
            mcp.EnableSampling = false;
            mcp.EnableDevelopmentFeatures = true;
            mcp.EnableValidation = true;
        });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseRouting();

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapMcp("/mcp").RequireAuthorization();

        await app.StartAsync();

        try
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:5000");
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
        }
    }
}