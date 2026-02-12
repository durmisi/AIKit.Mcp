using Microsoft.AspNetCore.WebUtilities;
using System.Net;
using System.Security.Cryptography;
using Xunit.Abstractions;

/// <summary>
/// Helper class for OAuth token acquisition in tests.
/// </summary>
public static class OAuthTestHelper
{
    /// <summary>
    /// Gets a valid OAuth token using authorization code flow with PKCE.
    /// </summary>
    /// <param name="oauthClient">The HTTP client for the OAuth server.</param>
    /// <param name="oauthUrl">The OAuth server URL.</param>
    /// <param name="output">The test output helper.</param>
    /// <returns>The access token.</returns>
    public static async Task<string> GetValidTokenAsync(HttpClient oauthClient, string oauthUrl, ITestOutputHelper output)
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
        output.WriteLine($"Redirect location: {location}");
        if (location == null || string.IsNullOrEmpty(location.Query))
        {
            throw new Exception("No redirect location");
        }
        var queryParams = QueryHelpers.ParseQuery(location.Query);
        var code = queryParams["code"];
        output.WriteLine($"Authorization code: {code}");
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
            output.WriteLine($"Token request failed: {response.StatusCode} - {content}");
        }
        response.EnsureSuccessStatusCode();
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return tokenResponse!.AccessToken;
    }

    /// <summary>
    /// Generates a PKCE code verifier.
    /// </summary>
    /// <returns>The code verifier.</returns>
    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Generates a PKCE code challenge.
    /// </summary>
    /// <param name="codeVerifier">The code verifier.</param>
    /// <returns>The code challenge.</returns>
    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(codeVerifier));
        return WebEncoders.Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Represents an OAuth token response.
    /// </summary>
    public class TokenResponse
    {
        /// <summary>
        /// The access token.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        /// <summary>
        /// The token type.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string TokenType { get; set; }
    }
}