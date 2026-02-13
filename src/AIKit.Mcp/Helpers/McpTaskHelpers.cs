using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace AIKit.Mcp.Helpers;

/// <summary>
/// Helper methods for working with MCP Tasks in AIKit.Mcp.
/// </summary>
public static class McpTaskHelpers
{
    /// <summary>
    /// Reports progress for a long-running operation.
    /// </summary>
    /// <param name="server">The MCP server instance.</param>
    /// <param name="progressToken">Token identifying the progress operation.</param>
    /// <param name="progress">Current progress value.</param>
    /// <param name="total">Optional total progress value.</param>
    /// <param name="message">Optional progress message.</param>
    public static Task ReportProgressAsync(
        McpServer server,
        ProgressToken progressToken,
        float progress,
        float? total = null,
        string? message = null)
    {
        var progressValue = new ProgressNotificationValue
        {
            Progress = progress,
            Total = total,
            Message = message
        };
        return server.NotifyProgressAsync(progressToken, progressValue);
    }

    /// <summary>
    /// Sends a progress notification using a ProgressNotificationValue.
    /// </summary>
    /// <param name="server">The MCP server instance.</param>
    /// <param name="progressToken">Token identifying the progress operation.</param>
    /// <param name="progress">The progress notification value.</param>
    public static Task NotifyProgressAsync(
        McpServer server,
        ProgressToken progressToken,
        ProgressNotificationValue progress)
    {
        return server.NotifyProgressAsync(progressToken, progress);
    }

    /// <summary>
    /// Creates a task with custom metadata and executes a work function asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="taskStore">The task store instance.</param>
    /// <param name="work">The asynchronous work function to execute.</param>
    /// <param name="taskMetadata">Optional metadata for the task.</param>
    /// <param name="sessionId">Optional session ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created MCP task.</returns>
    public static async Task<McpTask> CreateTaskAsync<T>(
        IMcpTaskStore taskStore,
        Func<Task<T>> work,
        McpTaskMetadata? taskMetadata = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        taskMetadata ??= new McpTaskMetadata { TimeToLive = TimeSpan.FromMinutes(5) };

        // Create a dummy request for the task
        var request = new JsonRpcRequest
        {
            Id = new RequestId(Guid.NewGuid().ToString()),
            Method = "task.create",
            Params = null
        };

        var task = await taskStore.CreateTaskAsync(taskMetadata, request.Id, request, sessionId, cancellationToken);

        // Execute work in background
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await work();
                await taskStore.StoreTaskResultAsync(task.TaskId, McpTaskStatus.Completed, JsonSerializer.SerializeToElement(result), sessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                await taskStore.StoreTaskResultAsync(task.TaskId, McpTaskStatus.Failed, JsonSerializer.SerializeToElement(ex.Message), sessionId, cancellationToken);
            }
        }, cancellationToken);

        return task;
    }

}



/// <summary>
/// Extension methods for McpServer to simplify common operations.
/// </summary>
public static class McpServerExtensions
{
    /// <summary>
    /// Sends a progress notification.
    /// </summary>
    /// <param name="server">The MCP server instance.</param>
    /// <param name="progressToken">Token identifying the progress operation.</param>
    /// <param name="progress">Current progress value.</param>
    /// <param name="total">Optional total progress value.</param>
    /// <param name="message">Optional progress message.</param>
    public static Task NotifyProgressAsync(
        this McpServer server,
        ProgressToken progressToken,
        float progress,
        float? total = null,
        string? message = null)
    {
        return McpTaskHelpers.ReportProgressAsync(server, progressToken, progress, total, message);
    }
}