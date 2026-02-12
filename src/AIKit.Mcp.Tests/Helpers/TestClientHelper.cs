using System.Net.Http.Headers;

/// <summary>
/// Helper class for creating HTTP clients and sending MCP requests in tests.
/// </summary>
public static class TestClientHelper
{
    /// <summary>
    /// Creates an HttpClient configured for MCP requests.
    /// </summary>
    /// <param name="baseUrl">The base URL of the MCP server.</param>
    /// <param name="bearerToken">Optional Bearer token for authentication.</param>
    /// <returns>The configured HttpClient.</returns>
    public static HttpClient CreateMcpHttpClient(string baseUrl, string? bearerToken = null)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (bearerToken != null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        return client;
    }

    /// <summary>
    /// Sends a JSON-RPC request to the MCP server.
    /// </summary>
    /// <param name="client">The HttpClient to use.</param>
    /// <param name="method">The JSON-RPC method.</param>
    /// <param name="params">The method parameters.</param>
    /// <param name="id">The request ID.</param>
    /// <returns>The HTTP response.</returns>
    public static async Task<HttpResponseMessage> SendMcpRequestAsync(HttpClient client, string method, object? @params = null, object? id = null)
    {
        var request = new { jsonrpc = "2.0", method, @params, id = id ?? Guid.NewGuid().ToString() };
        return await client.PostAsJsonAsync("/mcp", request);
    }
}