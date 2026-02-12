using ModelContextProtocol.Protocol;
using System.Text.Json;

/// <summary>
/// Helper class for creating test data in task store tests.
/// </summary>
public static class TaskStoreTestHelper
{
    /// <summary>
    /// Creates default MCP task metadata.
    /// </summary>
    /// <param name="ttl">Optional time to live.</param>
    /// <returns>The metadata.</returns>
    public static McpTaskMetadata CreateDefaultMetadata(TimeSpan? ttl = null)
    {
        return new McpTaskMetadata { TimeToLive = ttl ?? TimeSpan.FromHours(1) };
    }

    /// <summary>
    /// Creates a test JSON-RPC request.
    /// </summary>
    /// <param name="id">The request ID.</param>
    /// <param name="method">The method name.</param>
    /// <param name="params">The parameters.</param>
    /// <returns>The request.</returns>
    public static JsonRpcRequest CreateTestRequest(string id = "test-id", string method = "test.method", object? @params = null)
    {
        return new JsonRpcRequest
        {
            Id = new RequestId(id),
            Method = method,
            Params = @params != null ? JsonSerializer.SerializeToNode(@params) : null
        };
    }
}