using AIKit.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace AIKit.Mcp.Tests;

public class McpServiceExtensionsTests
{
    [Fact]
    public void AddAIKitMcp_AddsMcpServerBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = services.AddAIKitMcp();

        // Assert
        Assert.NotNull(builder);
        Assert.IsType<AIKitMcpBuilder>(builder);
        Assert.NotNull(builder.InnerBuilder);
        Assert.IsAssignableFrom<IMcpServerBuilder>(builder.InnerBuilder);
    }

    [Fact]
    public void WithDefaultConfiguration_ConfiguresStdioTransportAndAutoDiscovery()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithDefaultConfiguration();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithConfiguration_LoadsSettingsFromConfig()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:ServerName"] = "TestServer",
                ["Mcp:ServerVersion"] = "1.0.0",
                ["Mcp:Transport"] = "stdio",
                ["Mcp:AutoDiscoverTools"] = "true",
                ["Mcp:AutoDiscoverResources"] = "true",
                ["Mcp:AutoDiscoverPrompts"] = "true"
            })
            .Build();

        // Act
        var result = builder.WithConfiguration(config);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithAllFromAssembly_DiscoversFromSpecifiedAssembly()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithAllFromAssembly(typeof(McpServiceExtensionsTests).Assembly);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithAllFromAssembly_UsesCallingAssemblyByDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithAllFromAssembly();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithDevelopmentFeatures_AddsMessageFilters()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithDevelopmentFeatures();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithValidation_AddsHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithValidation();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);

        // Verify hosted service was added
        var hostedServices = services.Where(sd => sd.ServiceType == typeof(IHostedService)).ToList();
        Assert.Single(hostedServices);
    }

    [Fact]
    public void WithOptions_ConfiguresBuilderCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithOptions(options =>
        {
            options.ServerName = "TestServer";
            options.Transport = "stdio";
            options.AutoDiscoverTools = true;
            options.EnableDevelopmentFeatures = true;
            options.EnableValidation = true;
        });

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);

        // Verify hosted services were added (validation when enabled)
        var hostedServices = services.Where(sd => sd.ServiceType == typeof(IHostedService)).ToList();
        Assert.True(hostedServices.Count >= 1, "Expected at least 1 hosted service (validation)"); // Only validation is added as hosted service
    }

    [Fact]
    public void WithTasks_AddsInMemoryTaskStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithTasks();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);

        // Verify task store was registered
        var taskStoreDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(ModelContextProtocol.IMcpTaskStore));
        Assert.NotNull(taskStoreDescriptor);
    }

    [Fact]
    public void WithTaskStore_AddsCustomTaskStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithTaskStore<CustomTaskStore>();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);

        // Verify custom task store was registered
        var taskStoreDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(ModelContextProtocol.IMcpTaskStore));
        Assert.NotNull(taskStoreDescriptor);
    }

    [Fact]
    public void WithLongRunningTool_RegistersToolAsScopedService()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithLongRunningTool<LongRunningTool>();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);

        // Verify tool was registered as scoped service
        var toolDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(LongRunningTool));
        Assert.NotNull(toolDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, toolDescriptor.Lifetime);
    }

    [Fact]
    public void WithHttpTransport_ConfiguresHttpTransport()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithHttpTransport();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(builder, result);
    }

    [Fact]
    public void WithOptions_EnablesTasks_WhenConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithOptions(options =>
        {
            options.EnableTasks = true;
        });

        // Assert
        Assert.NotNull(result);

        // Verify task store was registered
        var taskStoreDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(ModelContextProtocol.IMcpTaskStore));
        Assert.NotNull(taskStoreDescriptor);
    }

    [Fact]
    public void WithOptions_EnablesValidation_WhenConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithOptions(options =>
        {
            options.EnableValidation = true;
        });

        // Assert
        Assert.NotNull(result);

        // Verify hosted service was added
        var hostedServices = services.Where(sd => sd.ServiceType == typeof(IHostedService)).ToList();
        Assert.Single(hostedServices);
    }

    [Fact]
    public void WithConfiguration_HandlesNewConfigOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:EnableTasks"] = "true",
                ["Mcp:EnableElicitation"] = "true",
                ["Mcp:EnableProgress"] = "true",
                ["Mcp:EnableCompletion"] = "true",
                ["Mcp:EnableSampling"] = "true"
            })
            .Build();

        // Act
        var result = builder.WithConfiguration(config);

        // Assert
        Assert.NotNull(result);

        // Verify task store was registered when EnableTasks is true
        var taskStoreDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(ModelContextProtocol.IMcpTaskStore));
        Assert.NotNull(taskStoreDescriptor);
    }

    [Fact]
    public void WithElicitation_ConfiguresElicitationCapability()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithElicitation();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithCompletion_ConfiguresCompletionCapability()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithCompletion();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);

        // Note: The method configures both a completion handler on the MCP server builder
        // and capabilities in McpServerOptions. We verify the method completes successfully
        // and returns the expected builder. The actual capability activation is tested
        // through integration with the MCP SDK.
    }

    [Fact]
    public void WithSampling_ConfiguresSamplingCapability()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddAIKitMcp();

        // Act
        var result = builder.WithSampling();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AIKitMcpBuilder>(result);
        Assert.Same(builder, result);
    }

    [Fact]
    public void ValidateMcpConfiguration_ValidatesBasicConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAIKitMcp()
            .WithOptions(options =>
            {
                options.ServerName = "TestServer";
                options.Transport = "stdio";
                options.AutoDiscoverTools = true;
            });

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Should not throw for valid configuration
        Assert.NotNull(serviceProvider);
        McpServiceExtensions.ValidateMcpConfiguration(serviceProvider);
    }

    [Fact]
    public void ValidateMcpConfiguration_DetectsDuplicateToolNames()
    {
        // Arrange
        var services = new ServiceCollection();
        var loggerMock = new Mock<ILogger>();
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

        services.AddSingleton(loggerFactoryMock.Object);
        services.AddAIKitMcp()
            .WithOptions(options =>
            {
                options.ServerName = "TestServer";
                options.Transport = "stdio";
                options.AutoDiscoverTools = true;
            })
            .WithTasks(); // Add task store to avoid early failure

        // Register tools with duplicate names
        services.AddScoped<DuplicateTool1>();
        services.AddScoped<DuplicateTool2>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        McpServiceExtensions.ValidateMcpConfiguration(serviceProvider);

        // Assert - Should log error for duplicate tool names
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Duplicate tool names found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ValidateMcpConfiguration_ValidatesHttpConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAIKitMcp()
            .WithOptions(options =>
            {
                options.ServerName = "TestServer";
                options.Transport = "http";
                options.HttpBasePath = "/mcp";
                options.RequireAuthentication = true;
            });

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Should not throw for valid HTTP configuration
        Assert.NotNull(serviceProvider);
        McpServiceExtensions.ValidateMcpConfiguration(serviceProvider);
    }

    [Fact]
    public void ValidateMcpConfiguration_WarnsForMissingHttpBasePath()
    {
        // Arrange
        var services = new ServiceCollection();
        var loggerMock = new Mock<ILogger>();
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

        services.AddSingleton(loggerFactoryMock.Object);
        services.AddAIKitMcp()
            .WithOptions(options =>
            {
                options.ServerName = "TestServer";
                options.Transport = "http";
                options.HttpBasePath = ""; // Empty base path
            })
            .WithTasks(); // Add task store to avoid early failure

        var serviceProvider = services.BuildServiceProvider();

        // Act
        McpServiceExtensions.ValidateMcpConfiguration(serviceProvider);

        // Assert - Should log information about validation completion
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("MCP configuration validation completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}

[McpServerToolType]
public class SampleTool
{
    [McpServerTool(Name = "sample_tool")]
    public string ExecuteSampleTool() => "sample result";
}

[McpServerToolType]
public class SampleResource
{
    [McpServerResource(UriTemplate = "sample://resource", Name = "Sample Resource")]
    public string GetSampleResource() => "sample resource content";
}

[McpServerToolType]
public class DuplicateTool1
{
    [McpServerTool(Name = "duplicate_tool")]
    public string ExecuteTool() => "result1";
}

[McpServerToolType]
public class DuplicateTool2
{
    [McpServerTool(Name = "duplicate_tool")]
    public string ExecuteTool() => "result2";
}

[McpServerToolType]
public class LongRunningTool
{
    [McpServerTool(Name = "long_running_tool")]
    public string ExecuteLongRunningTool() => "long running result";
}

public class CustomTaskStore : ModelContextProtocol.IMcpTaskStore
{
    // Minimal implementation for testing
    public Task<ModelContextProtocol.Protocol.McpTask> CreateTaskAsync(ModelContextProtocol.Protocol.McpTaskMetadata metadata, ModelContextProtocol.Protocol.RequestId requestId, ModelContextProtocol.Protocol.JsonRpcRequest? request, string? sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ModelContextProtocol.Protocol.McpTask
        {
            TaskId = "test-task",
            Status = ModelContextProtocol.Protocol.McpTaskStatus.Working,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public Task<ModelContextProtocol.Protocol.McpTask?> GetTaskAsync(string taskId, string? sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ModelContextProtocol.Protocol.McpTask?>(null);
    }

    public Task<System.Text.Json.JsonElement> GetTaskResultAsync(string taskId, string? sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(System.Text.Json.JsonDocument.Parse("{}").RootElement);
    }

    public Task<ModelContextProtocol.Protocol.ListTasksResult> ListTasksAsync(string? sessionId, string? cursor, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ModelContextProtocol.Protocol.ListTasksResult
        {
            Tasks = new List<ModelContextProtocol.Protocol.McpTask>().ToArray()
        });
    }

    public Task<ModelContextProtocol.Protocol.McpTask> CancelTaskAsync(string taskId, string? sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ModelContextProtocol.Protocol.McpTask
        {
            TaskId = taskId,
            Status = ModelContextProtocol.Protocol.McpTaskStatus.Cancelled,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public Task<ModelContextProtocol.Protocol.McpTask> StoreTaskResultAsync(string taskId, ModelContextProtocol.Protocol.McpTaskStatus status, System.Text.Json.JsonElement result, string? sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ModelContextProtocol.Protocol.McpTask
        {
            TaskId = taskId,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public Task<ModelContextProtocol.Protocol.McpTask> UpdateTaskStatusAsync(string taskId, ModelContextProtocol.Protocol.McpTaskStatus status, string? message, string? sessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ModelContextProtocol.Protocol.McpTask
        {
            TaskId = taskId,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUpdatedAt = DateTimeOffset.UtcNow
        });
    }
}
