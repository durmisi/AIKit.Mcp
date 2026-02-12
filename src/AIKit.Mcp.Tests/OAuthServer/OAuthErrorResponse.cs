using System.Text.Json.Serialization;

namespace AIKit.Mcp.Tests.OAuthServer;

/// <summary>
/// Represents an OAuth error response.
/// </summary>
public sealed class OAuthErrorResponse
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