using System.Text.Json.Serialization;

namespace AIKit.Mcp.Tests.TestOAuthServer;

/// <summary>
/// Represents an OAuth error response.
/// </summary>
internal sealed class OAuthErrorResponse
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; init; }

    /// <summary>
    /// Gets or sets the error description.
    /// </summary>
    [JsonPropertyName("error_description")]
    public string ErrorDescription { get; init; }
}