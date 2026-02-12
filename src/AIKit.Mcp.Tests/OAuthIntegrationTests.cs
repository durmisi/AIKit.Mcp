//using Microsoft.AspNetCore.Mvc.Testing;
//using Microsoft.Extensions.Logging.Abstractions;
//using ModelContextProtocol.Client;
//using ModelContextProtocol.Protocol;
//using System.Net;
//using System.Net.Http.Headers;
//using Xunit.Abstractions;
//using AIKit.Mcp.Tests.OAuthServer;
//using Microsoft.AspNetCore.Server.Kestrel.Core;

//namespace AIKit.Mcp.Tests;

///// <summary>
///// Integration tests for OAuth 2.0 authentication in MCP servers.
///// </summary>
//public class OAuthIntegrationTests : IAsyncLifetime
//{
//    private readonly ITestOutputHelper _output;
//    private OAuthServerFactory? _oauthFactory;
//    private OAuthMcpServerFactory? _mcpFactory;

//    public OAuthIntegrationTests(ITestOutputHelper output)
//    {
//        _output = output;
//    }

//    public async Task InitializeAsync()
//    {
//        _output.WriteLine("Initializing OAuth and MCP server factories...");
//        _oauthFactory = new OAuthServerFactory();
//        await _oauthFactory.StartAsync();
//        using var oauthClient = _oauthFactory.CreateClient();
//        var oauthUrl = oauthClient.BaseAddress!.ToString().TrimEnd('/');
//        _output.WriteLine($"OAuth server URL: {oauthUrl}");

//        _mcpFactory = new OAuthMcpServerFactory(oauthUrl);
//        _output.WriteLine("Factories initialized.");
//    }

//    public async Task DisposeAsync()
//    {
//        _output.WriteLine("Disposing server factories...");
//        if (_mcpFactory != null)
//        {
//            await _mcpFactory.DisposeAsync();
//        }
//        if (_oauthFactory != null)
//        {
//            await _oauthFactory.DisposeAsync();
//        }
//        _output.WriteLine("Factories disposed.");
//    }

//    [Fact]
//    public async Task OAuth_ServerAuthenticatesProperly()
//    {
//        _output.WriteLine("=== Starting OAuth Server Authentication Test ===");
//        Console.WriteLine("Test method started");
//        var startTime = DateTime.UtcNow;
//        _output.WriteLine($"Test started at {startTime}");

//        // Get clients from factories
//        using var oauthClient = _oauthFactory!.CreateClient();
//        var oauthUrl = oauthClient.BaseAddress!.ToString().TrimEnd('/');
//        _output.WriteLine($"OAuth server URL: {oauthUrl}");

//        using var mcpClient = _mcpFactory!.CreateClient();
//        var mcpUrl = mcpClient.BaseAddress!.ToString().TrimEnd('/');
//        _output.WriteLine($"MCP server URL: {mcpUrl}");

//        // Verify OAuth server is responding
//        _output.WriteLine("Verifying OAuth server is responding...");
//        var verifyResponse = await oauthClient.GetAsync("/");
//        verifyResponse.EnsureSuccessStatusCode();
//        _output.WriteLine("OAuth server is responding.");

//        // Get valid token
//        _output.WriteLine("Getting valid token...");
//        var tokenStart = DateTime.UtcNow;
//        var token = await GetValidTokenAsync(oauthClient, oauthUrl);
//        _output.WriteLine($"Token obtained in {(DateTime.UtcNow - tokenStart).TotalSeconds:F2}s");

//        // Make authenticated request
//        _output.WriteLine("Making authenticated request to MCP server...");
//        var requestStart = DateTime.UtcNow;
//        mcpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
//        mcpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
//        mcpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

//        var response = await mcpClient.PostAsJsonAsync("/mcp", new
//        {
//            jsonrpc = "2.0",
//            id = 1,
//            method = "tools/list",
//            @params = new { }
//        });
//        _output.WriteLine($"Request completed in {(DateTime.UtcNow - requestStart).TotalSeconds:F2}s");

//        // Assert
//        _output.WriteLine($"Response status: {response.StatusCode}");
//        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
//        _output.WriteLine("Server accepted valid OAuth token.");
//        _output.WriteLine($"Total test time: {(DateTime.UtcNow - startTime).TotalSeconds:F2}s");
//    }

//    private async Task<string> GetValidTokenAsync(HttpClient oauthClient, string oauthUrl)
//    {
//        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, oauthUrl + "/connect/token")
//        {
//            Content = new FormUrlEncodedContent(new[]
//            {
//                new KeyValuePair<string, string>("grant_type", "client_credentials"),
//                new KeyValuePair<string, string>("client_id", "demo-client"),
//                new KeyValuePair<string, string>("client_secret", "demo-secret"),
//                new KeyValuePair<string, string>("scope", "mcp")
//            })
//        };
//        var response = await oauthClient.SendAsync(tokenRequest);
//        response.EnsureSuccessStatusCode();
//        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
//        return tokenResponse!.AccessToken;
//    }
//}