using AIKit.Mcp.Tests.TestOAuthServer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Net;
using System.Net.Http.Headers;
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

    [Fact]
    public async Task OAuth_Client_Can_Authenticate_And_Call_Tools()
    {
        // Arrange
        using var httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });

        var transport = new HttpClientTransport(new()
        {
            Endpoint = new Uri("http://localhost:5000/mcp"),
            Name = "Test OAuth Client",
            OAuth = new()
            {
                RedirectUri = new Uri("http://localhost:1179/callback"),
                AuthorizationRedirectDelegate = HandleAuthorizationUrlAsync,
                DynamicClientRegistration = new()
                {
                    ClientName = "TestOAuthClient",
                },
            }
        }, httpClient, NullLoggerFactory.Instance);

        // Act
        await using var mcpClient = await McpClient.CreateAsync(transport);

        var tools = await mcpClient.ListToolsAsync();

        // Assert
        Assert.NotEmpty(tools);
        var addTool = tools.FirstOrDefault(t => t.Name == "add-numbers");
        Assert.NotNull(addTool);

        var result = await mcpClient.CallToolAsync("add-numbers", new Dictionary<string, object?> { { "a", 5 }, { "b", 10 } });

        Assert.NotNull(result);
        Assert.Null(result.IsError);
        Assert.NotNull(result.Content);
        var textContent = Assert.Single(result.Content);
        Assert.Equal("15", Assert.IsType<TextContentBlock>(textContent).Text);

        _output.WriteLine("Client successfully authenticated and called tools.");
    }

    private async Task<string?> HandleAuthorizationUrlAsync(Uri authorizationUrl, Uri redirectUri, CancellationToken cancellationToken)
    {
        // For testing, simulate the authorization flow programmatically
        using var httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });

        var response = await httpClient.GetAsync(authorizationUrl, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location;
        Assert.NotNull(location);

        var query = System.Web.HttpUtility.ParseQueryString(location.Query);
        var code = query["code"];
        Assert.NotNull(code);

        return code;
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