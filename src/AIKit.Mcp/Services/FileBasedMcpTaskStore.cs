using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AIKit.Mcp.Services;

/// <summary>
/// A configurable file-based implementation of <see cref="IMcpTaskStore"/> that
/// demonstrates durable, fault-tolerant task storage.
/// </summary>
public sealed partial class FileBasedMcpTaskStore : IMcpTaskStore
{
    private readonly FileBasedTaskStoreOptions _options;
    private readonly ILogger<FileBasedMcpTaskStore>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileBasedMcpTaskStore"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the store.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public FileBasedMcpTaskStore(FileBasedTaskStoreOptions options, ILogger<FileBasedMcpTaskStore>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        Directory.CreateDirectory(_options.StoragePath);
    }

    /// <inheritdoc/>
    public async Task<McpTask> CreateTaskAsync(
        McpTaskMetadata metadata,
        RequestId requestId,
        JsonRpcRequest request,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var taskId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var entry = new TaskFileEntry
        {
            TaskId = taskId,
            SessionId = sessionId,
            Status = McpTaskStatus.Working,
            CreatedAt = now,
            ExecutionTime = _options.DefaultTtl, // Use TTL as execution time
            TimeToLive = metadata.TimeToLive ?? _options.DefaultTtl,
            Result = JsonSerializer.SerializeToElement(request.Params, JsonContext.Default.JsonNode)
        };

        await WriteTaskEntryAsync(GetTaskFilePath(taskId), entry, cancellationToken);
        _logger?.LogInformation("Created task {TaskId} in {FilePath}", taskId, GetTaskFilePath(taskId));
        return ToMcpTask(entry);
    }

    /// <inheritdoc/>
    public async Task<McpTask?> GetTaskAsync(
        string taskId,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var entry = await ReadTaskEntryAsync(taskId, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        // Session isolation
        if (_options.EnableSessionIsolation && sessionId is not null && entry.SessionId != sessionId)
        {
            return null;
        }

        // Skip if TTL has expired
        if (IsExpired(entry))
        {
            return null;
        }

        return ToMcpTask(entry);
    }

    /// <inheritdoc/>
    public async Task<McpTask> StoreTaskResultAsync(
        string taskId,
        McpTaskStatus status,
        JsonElement result,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (status is not McpTaskStatus.Completed and not McpTaskStatus.Failed)
        {
            throw new ArgumentException($"Status must be {nameof(McpTaskStatus.Completed)} or {nameof(McpTaskStatus.Failed)}.", nameof(status));
        }

        var updatedEntry = await UpdateTaskEntryAsync(taskId, sessionId, entry =>
        {
            var effectiveStatus = GetEffectiveStatus(entry);
            if (IsTerminalStatus(effectiveStatus))
            {
                throw new InvalidOperationException($"Cannot store result for task in terminal state: {effectiveStatus}");
            }

            return entry with
            {
                Status = status,
                Result = result
            };
        }, cancellationToken);

        _logger?.LogInformation("Stored result for task {TaskId}", taskId);
        return ToMcpTask(updatedEntry);
    }

    /// <inheritdoc/>
    public async Task<JsonElement> GetTaskResultAsync(
        string taskId,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var entry = await ReadTaskEntryAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task not found: {taskId}");

        if (_options.EnableSessionIsolation && sessionId is not null && entry.SessionId != sessionId)
        {
            throw new InvalidOperationException($"Task not found: {taskId}");
        }

        var effectiveStatus = GetEffectiveStatus(entry);
        if (!IsTerminalStatus(effectiveStatus))
        {
            throw new InvalidOperationException($"Task not yet completed: {taskId}");
        }

        // Return stored result
        return entry.Result ?? default;
    }

    /// <inheritdoc/>
    public async Task<McpTask> UpdateTaskStatusAsync(
        string taskId,
        McpTaskStatus status,
        string? statusMessage,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var updatedEntry = await UpdateTaskEntryAsync(taskId, sessionId, entry =>
            entry with
            {
                Status = status,
                StatusMessage = statusMessage
            }, cancellationToken);

        _logger?.LogInformation("Updated status for task {TaskId} to {Status}", taskId, status);
        return ToMcpTask(updatedEntry);
    }

    /// <inheritdoc/>
    public async Task<ListTasksResult> ListTasksAsync(
        string? cursor = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<McpTask>();
        foreach (var file in Directory.EnumerateFiles(_options.StoragePath, $"*{ _options.FileExtension}"))
        {
            try
            {
                var entry = await ReadTaskEntryFromFileAsync(file, cancellationToken);
                if (entry is not null)
                {
                    // Session isolation
                    if (_options.EnableSessionIsolation && sessionId is not null && entry.SessionId != sessionId)
                    {
                        continue;
                    }

                    // Skip expired tasks
                    if (IsExpired(entry))
                    {
                        continue;
                    }

                    tasks.Add(ToMcpTask(entry));
                }
            }
            catch
            {
                // Skip corrupted or inaccessible files
            }
        }

        tasks.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt)); // Newest first

        _logger?.LogDebug("Listed {TaskCount} tasks", tasks.Count);
        return new ListTasksResult { Tasks = tasks.ToArray() };
    }

    /// <inheritdoc/>
    public async Task<McpTask> CancelTaskAsync(
        string taskId,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var updatedEntry = await UpdateTaskEntryAsync(taskId, sessionId, entry =>
        {
            var effectiveStatus = GetEffectiveStatus(entry);
            if (IsTerminalStatus(effectiveStatus))
            {
                // Already terminal, return unchanged
                return entry;
            }

            return entry with { Status = McpTaskStatus.Cancelled };
        }, cancellationToken);

        _logger?.LogInformation("Cancelled task {TaskId}", taskId);
        return ToMcpTask(updatedEntry);
    }

    private string GetTaskFilePath(string taskId) => Path.Combine(_options.StoragePath, $"{taskId}{_options.FileExtension}");

    /// <summary>
    /// Reads, transforms, and writes a task entry while holding an exclusive file lock.
    /// </summary>
    private async Task<TaskFileEntry> UpdateTaskEntryAsync(
        string taskId,
        string? sessionId,
        Func<TaskFileEntry, TaskFileEntry> updateFunc,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetTaskFilePath(taskId);

        // Acquire exclusive lock on the file for the entire read-modify-write cycle
        using var stream = await AcquireFileStreamAsync(filePath, FileMode.Open, FileAccess.ReadWrite, cancellationToken);

        var entry = await JsonSerializer.DeserializeAsync(stream, JsonContext.Default.TaskFileEntry, cancellationToken)
            ?? throw new InvalidOperationException($"Task not found: {taskId}");

        // Enforce session isolation
        if (_options.EnableSessionIsolation && sessionId is not null && entry.SessionId != sessionId)
        {
            throw new InvalidOperationException($"Task not found: {taskId}");
        }

        // Apply the transformation (may throw to abort)
        var updatedEntry = updateFunc(entry);

        // Write back to the same stream
        stream.SetLength(0);
        stream.Position = 0;
        await JsonSerializer.SerializeAsync(stream, updatedEntry, JsonContext.Default.TaskFileEntry, cancellationToken);

        return updatedEntry;
    }

    private async Task<TaskFileEntry?> ReadTaskEntryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var filePath = GetTaskFilePath(taskId);
        return File.Exists(filePath) ? await ReadTaskEntryFromFileAsync(filePath, cancellationToken) : null;
    }

    private static async Task<TaskFileEntry?> ReadTaskEntryFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var stream = await AcquireFileStreamAsync(filePath, FileMode.Open, FileAccess.Read, cancellationToken);
            return await JsonSerializer.DeserializeAsync(stream, JsonContext.Default.TaskFileEntry, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteTaskEntryAsync(string filePath, TaskFileEntry entry, CancellationToken cancellationToken = default)
    {
        using var stream = await AcquireFileStreamAsync(filePath, FileMode.Create, FileAccess.Write, cancellationToken);
        await JsonSerializer.SerializeAsync(stream, entry, JsonContext.Default.TaskFileEntry, cancellationToken);
    }

    private static async Task<FileStream> AcquireFileStreamAsync(string filePath, FileMode fileMode, FileAccess fileAccess, CancellationToken cancellationToken = default)
    {
        const int MaxRetries = 10;
        const int RetryDelayMs = 50;

        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(filePath, fileMode, fileAccess, FileShare.None);
            }
            catch (IOException) when (attempt < MaxRetries)
            {
                await Task.Delay(RetryDelayMs, cancellationToken); // File is locked by another process, wait and retry
            }
        }
    }

    private McpTask ToMcpTask(TaskFileEntry entry)
    {
        var now = DateTimeOffset.UtcNow;
        return new McpTask
        {
            TaskId = entry.TaskId,
            Status = GetEffectiveStatus(entry),
            StatusMessage = entry.StatusMessage,
            CreatedAt = entry.CreatedAt,
            LastUpdatedAt = now,
            TimeToLive = entry.TimeToLive
        };
    }

    private static McpTaskStatus GetEffectiveStatus(TaskFileEntry entry)
    {
        // If already in a terminal state, return it
        if (IsTerminalStatus(entry.Status))
        {
            return entry.Status;
        }

        // Check if execution time has elapsed - auto-complete
        if (DateTimeOffset.UtcNow - entry.CreatedAt >= entry.ExecutionTime)
        {
            return McpTaskStatus.Completed;
        }

        return entry.Status;
    }

    private static bool IsTerminalStatus(McpTaskStatus status) =>
        status is McpTaskStatus.Completed or McpTaskStatus.Failed or McpTaskStatus.Cancelled;

    private static bool IsExpired(TaskFileEntry entry) =>
        entry.TimeToLive.HasValue && DateTimeOffset.UtcNow - entry.CreatedAt > entry.TimeToLive.Value;

    /// <summary>
    /// Represents the data stored for each task.
    /// </summary>
    private sealed record TaskFileEntry
    {
        /// <summary>The unique task identifier.</summary>
        public required string TaskId { get; init; }

        /// <summary>The session that created this task.</summary>
        public string? SessionId { get; init; }

        /// <summary>The current task status.</summary>
        public required McpTaskStatus Status { get; init; }

        /// <summary>Optional status message describing the current state.</summary>
        public string? StatusMessage { get; init; }

        /// <summary>When the task was created.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>How long until the task is considered complete (if not explicitly completed).</summary>
        public required TimeSpan ExecutionTime { get; init; }

        /// <summary>Time to live - task is filtered out after this duration from creation.</summary>
        public TimeSpan? TimeToLive { get; init; }

        /// <summary>The task result - initialized with request params, updated via StoreTaskResultAsync.</summary>
        public JsonElement? Result { get; init; }
    }

    [JsonSerializable(typeof(TaskFileEntry))]
    [JsonSerializable(typeof(JsonNode))]
    private sealed partial class JsonContext : JsonSerializerContext;
}