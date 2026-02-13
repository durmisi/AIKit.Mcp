using AIKit.Mcp.Services;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIKit.Mcp.Tests;

[Collection("Integration")]
public class FileBasedTaskStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileBasedTaskStoreOptions _options;
    private readonly ILogger<FileBasedMcpTaskStore> _logger;
    private readonly ITestOutputHelper _output;

    public FileBasedTaskStoreTests(ITestOutputHelper output)
    {
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), "AIKitMcpTests", Guid.NewGuid().ToString());
        _options = new FileBasedTaskStoreOptions
        {
            StoragePath = _testDirectory,
            DefaultTtl = TimeSpan.FromMinutes(5),
            EnableSessionIsolation = true
        };
        _logger = new LoggerFactory().CreateLogger<FileBasedMcpTaskStore>();

        _output.WriteLine($"Test directory: {_testDirectory}");
        _output.WriteLine($"Storage path: {_options.StoragePath}");
        _output.WriteLine($"Default TTL: {_options.DefaultTtl}");
        _output.WriteLine($"Session isolation enabled: {_options.EnableSessionIsolation}");
    }

    [Fact]
    public async Task CreateTaskAsync_CreatesTaskWithCorrectProperties()
    {
        _output.WriteLine("Starting CreateTaskAsync_CreatesTaskWithCorrectProperties test");

        // Arrange
        var store = new FileBasedMcpTaskStore(_options, _logger);
        var metadata = TaskStoreTestHelper.CreateDefaultMetadata();
        var request = TaskStoreTestHelper.CreateTestRequest("test-id", "test.method", new { param = "value" });

        _output.WriteLine($"Created store with options: TTL={metadata.TimeToLive}, RequestId={request.Id}, Method={request.Method}");

        // Act
        var task = await store.CreateTaskAsync(metadata, request.Id, request, "session1");
        _output.WriteLine($"Created task: Id={task.TaskId}, Status={task.Status}, CreatedAt={task.CreatedAt}");

        // Assert
        Assert.NotNull(task);
        _output.WriteLine("Task is not null ✓");

        Assert.NotNull(task.TaskId);
        _output.WriteLine($"TaskId is not null: {task.TaskId} ✓");

        Assert.Equal(McpTaskStatus.Working, task.Status);
        _output.WriteLine($"Task status is Working: {task.Status} ✓");

        Assert.True(task.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        _output.WriteLine($"Task created recently: {task.CreatedAt} ✓");

        _output.WriteLine("CreateTaskAsync_CreatesTaskWithCorrectProperties test completed successfully");
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsTask_WhenExists()
    {
        _output.WriteLine("Starting GetTaskAsync_ReturnsTask_WhenExists test");

        // Arrange
        var store = new FileBasedMcpTaskStore(_options, _logger);
        var metadata = new McpTaskMetadata { TimeToLive = TimeSpan.FromHours(1) };
        var request = new JsonRpcRequest
        {
            Id = new RequestId("test-id"),
            Method = "test.method"
        };
        var createdTask = await store.CreateTaskAsync(metadata, request.Id, request, "session1");
        _output.WriteLine($"Created task: Id={createdTask.TaskId}, Status={createdTask.Status}");

        // Act
        var retrievedTask = await store.GetTaskAsync(createdTask.TaskId, "session1");
        _output.WriteLine($"Retrieved task: {(retrievedTask != null ? $"Id={retrievedTask.TaskId}, Status={retrievedTask.Status}" : "null")}");

        // Assert
        Assert.NotNull(retrievedTask);
        _output.WriteLine("Retrieved task is not null ✓");

        Assert.Equal(createdTask.TaskId, retrievedTask.TaskId);
        _output.WriteLine($"Task IDs match: {createdTask.TaskId} ✓");

        Assert.Equal(createdTask.Status, retrievedTask.Status);
        _output.WriteLine($"Task statuses match: {createdTask.Status} ✓");

        _output.WriteLine("GetTaskAsync_ReturnsTask_WhenExists test completed successfully");
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsNull_WhenSessionMismatch()
    {
        _output.WriteLine("Starting GetTaskAsync_ReturnsNull_WhenSessionMismatch test");

        // Arrange
        var store = new FileBasedMcpTaskStore(_options, _logger);
        var metadata = TaskStoreTestHelper.CreateDefaultMetadata();
        var request = TaskStoreTestHelper.CreateTestRequest("test-id", "test.method");
        var createdTask = await store.CreateTaskAsync(metadata, request.Id, request, "session1");
        _output.WriteLine($"Created task in session1: Id={createdTask.TaskId}");

        // Act
        var retrievedTask = await store.GetTaskAsync(createdTask.TaskId, "session2");
        _output.WriteLine($"Attempted to retrieve task from session2: {(retrievedTask != null ? "Found" : "Not found (null)")}");

        // Assert
        Assert.Null(retrievedTask);
        _output.WriteLine("Task correctly not accessible from different session ✓");

        _output.WriteLine("GetTaskAsync_ReturnsNull_WhenSessionMismatch test completed successfully");
    }

    [Fact]
    public async Task StoreTaskResultAsync_UpdatesTaskStatusAndResult()
    {
        _output.WriteLine("Starting StoreTaskResultAsync_UpdatesTaskStatusAndResult test");

        // Arrange
        var store = new FileBasedMcpTaskStore(_options, _logger);
        var metadata = TaskStoreTestHelper.CreateDefaultMetadata();
        var request = TaskStoreTestHelper.CreateTestRequest("test-id", "test.method");
        var task = await store.CreateTaskAsync(metadata, request.Id, request, "session1");
        var result = JsonSerializer.SerializeToElement(new { output = "success" });
        _output.WriteLine($"Created task: Id={task.TaskId}, Status={task.Status}");
        _output.WriteLine($"Prepared result: {result.GetRawText()}");

        // Act
        var updatedTask = await store.StoreTaskResultAsync(task.TaskId, McpTaskStatus.Completed, result, "session1");
        _output.WriteLine($"Updated task status to: {updatedTask.Status}");

        // Assert
        Assert.Equal(McpTaskStatus.Completed, updatedTask.Status);
        _output.WriteLine("Task status correctly updated to Completed ✓");

        var retrievedResult = await store.GetTaskResultAsync(task.TaskId, "session1");
        _output.WriteLine($"Retrieved result from storage");

        Assert.True(retrievedResult.TryGetProperty("output", out var outputProp));
        _output.WriteLine("Result has 'output' property ✓");

        Assert.Equal("success", outputProp.GetString());
        _output.WriteLine("Result 'output' property has correct value: success ✓");

        _output.WriteLine("StoreTaskResultAsync_UpdatesTaskStatusAndResult test completed successfully");
    }

    [Fact]
    public async Task ListTasksAsync_ReturnsTasksForSession()
    {
        _output.WriteLine("Starting ListTasksAsync_ReturnsTasksForSession test");

        // Arrange
        var store = new FileBasedMcpTaskStore(_options, _logger);
        var metadata = TaskStoreTestHelper.CreateDefaultMetadata();
        var request1 = TaskStoreTestHelper.CreateTestRequest("id1", "method1");
        var request2 = TaskStoreTestHelper.CreateTestRequest("id2", "method2");

        await store.CreateTaskAsync(metadata, request1.Id, request1, "session1");
        _output.WriteLine("Created task in session1");

        await store.CreateTaskAsync(metadata, request2.Id, request2, "session2");
        _output.WriteLine("Created task in session2");

        // Act
        var result = await store.ListTasksAsync(sessionId: "session1");
        _output.WriteLine($"Listed tasks for session1: Found {result.Tasks.Length} tasks");

        // Assert
        Assert.Single(result.Tasks);
        _output.WriteLine("Correctly returned exactly 1 task for session1 ✓");

        _output.WriteLine("ListTasksAsync_ReturnsTasksForSession test completed successfully");
    }

    [Fact]
    public async Task CancelTaskAsync_CancelsWorkingTask()
    {
        _output.WriteLine("Starting CancelTaskAsync_CancelsWorkingTask test");

        // Arrange
        var store = new FileBasedMcpTaskStore(_options, _logger);
        var metadata = TaskStoreTestHelper.CreateDefaultMetadata();
        var request = TaskStoreTestHelper.CreateTestRequest("test-id", "test.method");
        var task = await store.CreateTaskAsync(metadata, request.Id, request, "session1");
        _output.WriteLine($"Created task: Id={task.TaskId}, Status={task.Status}");

        // Act
        var cancelledTask = await store.CancelTaskAsync(task.TaskId, "session1");
        _output.WriteLine($"Cancelled task: Status={cancelledTask.Status}");

        // Assert
        Assert.Equal(McpTaskStatus.Cancelled, cancelledTask.Status);
        _output.WriteLine("Task status correctly changed to Cancelled ✓");

        _output.WriteLine("CancelTaskAsync_CancelsWorkingTask test completed successfully");
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsNull_WhenExpired()
    {
        _output.WriteLine("Starting GetTaskAsync_ReturnsNull_WhenExpired test");

        // Arrange
        var options = new FileBasedTaskStoreOptions
        {
            StoragePath = _testDirectory,
            DefaultTtl = TimeSpan.FromMilliseconds(1), // Very short TTL
            EnableSessionIsolation = true
        };
        var store = new FileBasedMcpTaskStore(options, _logger);
        var metadata = TaskStoreTestHelper.CreateDefaultMetadata(TimeSpan.FromMilliseconds(1));
        var request = TaskStoreTestHelper.CreateTestRequest("test-id", "test.method");
        var task = await store.CreateTaskAsync(metadata, request.Id, request, "session1");
        _output.WriteLine($"Created task with TTL: Id={task.TaskId}, TTL={metadata.TimeToLive?.TotalMilliseconds ?? 0}ms");

        // Wait for expiration
        await Task.Delay(10);
        _output.WriteLine("Waited 10ms for task to expire");

        // Act
        var retrievedTask = await store.GetTaskAsync(task.TaskId, "session1");
        _output.WriteLine($"Retrieved task after expiration: {(retrievedTask == null ? "null" : "not null")}");

        // Assert
        Assert.Null(retrievedTask);
        _output.WriteLine("Task correctly returned null after expiration ✓");

        _output.WriteLine("GetTaskAsync_ReturnsNull_WhenExpired test completed successfully");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}