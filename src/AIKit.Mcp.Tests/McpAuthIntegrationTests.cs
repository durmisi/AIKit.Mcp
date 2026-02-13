
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Headers;
using Xunit.Abstractions;
using ModelContextProtocol.TestOAuthServer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;

namespace AIKit.Mcp.Tests;

/// <summary>
/// Integration tests for MCP authentication in MCP servers.
/// </summary>
[Collection("Integration")]
public class McpAuthIntegrationTests : McpAuthTestBase, IAsyncLifetime
{
    private WebApplication? _mcpApp;
    private HttpClient? _mcpClient;

    public McpAuthIntegrationTests(ITestOutputHelper output)
        : base(output)
    {
    }

    public async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mcpApp = await StartMcpServerAsync("/mcp");
        _mcpClient = new HttpClient();
        _mcpClient.BaseAddress = new Uri(McpServerUrl + "/mcp");
    }

    public async Task DisposeAsync()
    {
        if (_mcpApp != null)
        {
            await _mcpApp.StopAsync();
            await _mcpApp.DisposeAsync();
        }
        await base.DisposeAsync();
    }

    [Fact]
    public async Task McpAuth_ServerAuthenticatesProperly()
    {
        _output.WriteLine("=== Starting MCP Auth Server Authentication Test ===");
        Console.WriteLine("Test method started");
        var startTime = DateTime.UtcNow;
        _output.WriteLine($"Test started at {startTime}");

        // Create OAuth client
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        handler.AllowAutoRedirect = false;
        using var oauthClient = new HttpClient(handler);
        oauthClient.BaseAddress = new Uri(OAuthServerUrl);

        _output.WriteLine($"OAuth server URL: {OAuthServerUrl}");

        // Verify OAuth server is responding
        _output.WriteLine("Verifying OAuth server is responding...");
        var verifyResponse = await oauthClient.GetAsync("/");
        verifyResponse.EnsureSuccessStatusCode();
        _output.WriteLine("OAuth server is responding.");

        // Get valid token
        _output.WriteLine("Getting valid token...");
        var tokenStart = DateTime.UtcNow;
        var token = await GetValidTokenAsync(oauthClient, OAuthServerUrl);
        _output.WriteLine($"Token obtained in {(DateTime.UtcNow - tokenStart).TotalSeconds:F2}s");

        // Make authenticated request
        _output.WriteLine("Making authenticated request to MCP server...");
        var requestStart = DateTime.UtcNow;
        _mcpClient!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _mcpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        _mcpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await _mcpClient.PostAsJsonAsync("", new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
            @params = new { }
        });
        _output.WriteLine($"Request completed in {(DateTime.UtcNow - requestStart).TotalSeconds:F2}s");

        // Assert
        _output.WriteLine($"Response status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _output.WriteLine("Server accepted valid MCP auth token.");
        _output.WriteLine($"Total test time: {(DateTime.UtcNow - startTime).TotalSeconds:F2}s");
    }

    [Fact]
    public async Task McpAuth_ServerRejectsInvalidToken()
    {
        _output.WriteLine("=== Starting MCP Auth Server Invalid Token Test ===");
        Console.WriteLine("Test method started");
        var startTime = DateTime.UtcNow;
        _output.WriteLine($"Test started at {startTime}");

        // Make request with invalid token
        _output.WriteLine("Making request with invalid token to MCP server...");
        var requestStart = DateTime.UtcNow;
        _mcpClient!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.mcp.token");
        _mcpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        _mcpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await _mcpClient.PostAsJsonAsync("", new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
            @params = new { }
        });
        _output.WriteLine($"Request completed in {(DateTime.UtcNow - requestStart).TotalSeconds:F2}s");

        // Assert
        _output.WriteLine($"Response status: {response.StatusCode}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _output.WriteLine("Server rejected invalid MCP auth token.");
        _output.WriteLine($"Total test time: {(DateTime.UtcNow - startTime).TotalSeconds:F2}s");
    }

    private async Task<string> GetValidTokenAsync(HttpClient oauthClient, string oauthUrl)
    {
        // Generate PKCE
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Use authorization code flow
        var authUrl = $"{oauthUrl}/authorize?client_id=demo-client&redirect_uri=http://localhost:1179/callback&response_type=code&scope=mcp&resource=http://localhost:5000/mcp&code_challenge={codeChallenge}&code_challenge_method=S256";
        var authResponse = await oauthClient.GetAsync(authUrl);
        if (authResponse.StatusCode != HttpStatusCode.Redirect)
        {
            authResponse.EnsureSuccessStatusCode();
        }
        var location = authResponse.Headers.Location;
        _output.WriteLine($"Redirect location: {location}");
        if (location == null || string.IsNullOrEmpty(location.Query))
        {
            throw new Exception("No redirect location");
        }
        var queryParams = QueryHelpers.ParseQuery(location.Query);
        var code = queryParams["code"];
        _output.WriteLine($"Authorization code: {code}");
        if (string.IsNullOrEmpty(code))
        {
            throw new Exception("No code in redirect");
        }

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, oauthUrl + "/token")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("client_id", "demo-client"),
                new KeyValuePair<string, string>("client_secret", "demo-secret"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("code_verifier", codeVerifier),
                new KeyValuePair<string, string>("redirect_uri", "http://localhost:1179/callback"),
                new KeyValuePair<string, string>("resource", "http://localhost:5000/mcp")
            })
        };
        var response = await oauthClient.SendAsync(tokenRequest);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"Token request failed: {response.StatusCode} - {content}");
        }
        response.EnsureSuccessStatusCode();
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return tokenResponse!.AccessToken;
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(codeVerifier));
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private class TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public required string AccessToken { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public required string TokenType { get; set; }
    }
}