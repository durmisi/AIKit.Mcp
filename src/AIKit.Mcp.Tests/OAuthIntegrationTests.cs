using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AIKit.Mcp.Tests.TestOAuthServer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AIKit.Mcp.Tests;

/// <summary>
/// Integration tests for OAuth 2.0 authentication in MCP servers.
/// </summary>
public class OAuthIntegrationTests : WebApplicationFactory<OAuthTestStartup>, IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private TestOAuthServer.TestOAuthServer? _oauthServer;
    private CancellationTokenSource _cts = new();

    public OAuthIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var loggerProvider = NullLoggerProvider.Instance;
        _oauthServer = new TestOAuthServer.TestOAuthServer(loggerProvider);
        await _oauthServer.RunServerAsync(cancellationToken: _cts.Token);
        _output.WriteLine("TestOAuthServer started.");
    }

    public new async Task DisposeAsync()
    {
        _cts.Cancel();
        // if (_oauthServer != null)
        // {
        //     await _oauthServer.DisposeAsync();
        // }
        _cts.Dispose();
    }

    [Fact]
    public async Task OAuth_ServerReturns401WithoutToken()
    {
        // Arrange
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/list",
                @params = new { }
            })
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("Server correctly returned 401 for unauthenticated request.");
    }

    [Fact]
    public async Task OAuth_ServerAcceptsValidToken()
    {
        // Arrange
        var client = CreateClient();
        var token = await GetValidTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/list",
                @params = new { }
            })
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _output.WriteLine("Server accepted valid OAuth token.");
    }

    [Fact]
    public async Task OAuth_ServerRejectsExpiredToken()
    {
        // Arrange
        var client = CreateClient();
        var expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjE1MTYyMzkwMjJ9.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c"; // Expired token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/list",
                @params = new { }
            })
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("Server rejected expired token.");
    }

    private async Task<string> GetValidTokenAsync()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        using var httpClient = new HttpClient(handler);
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://localhost:7029/connect/token")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", "demo-client"),
                new KeyValuePair<string, string>("client_secret", "demo-secret"),
                new KeyValuePair<string, string>("scope", "mcp")
            })
        };
        var response = await httpClient.SendAsync(tokenRequest);
        response.EnsureSuccessStatusCode();
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return tokenResponse!.AccessToken;
    }
}