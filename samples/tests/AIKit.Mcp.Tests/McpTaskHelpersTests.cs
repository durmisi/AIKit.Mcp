using AIKit.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Moq;
using System.Text.Json.Nodes;
using Xunit;

namespace AIKit.Mcp.Tests;

public class McpTaskHelpersTests
{
    [Fact]
    public async Task ReportProgressAsync_CompletesWithoutError()
    {
        // Arrange
        var mockServer = new Mock<McpServer>(MockBehavior.Loose);

        // Act
        await McpTaskHelpers.ReportProgressAsync(mockServer.Object, "test-token", 0.5, "Test message");

        // Assert - Should complete without throwing
        Assert.True(true);
    }
}

public class McpServerExtensionsTests
{
    [Fact]
    public async Task NotifyProgressAsync_CompletesWithoutError()
    {
        // Arrange
        var mockServer = new Mock<McpServer>(MockBehavior.Loose);

        // Act
        await mockServer.Object.NotifyProgressAsync("test-token", 0.5, "Test message");

        // Assert - Should complete without throwing
        Assert.True(true);
    }

    [Fact]
    public void McpOptions_HasNewProperties()
    {
        // Arrange & Act
        var options = new McpOptions();

        // Assert - Verify new properties exist and have default values
        Assert.False(options.EnableTasks);
        Assert.False(options.EnableElicitation);
        Assert.False(options.EnableProgress);
        Assert.False(options.EnableCompletion);
    }

    [Fact]
    public void McpOptions_CanSetNewProperties()
    {
        // Arrange & Act
        var options = new McpOptions
        {
            EnableTasks = true,
            EnableElicitation = true,
            EnableProgress = true,
            EnableCompletion = true
        };

        // Assert
        Assert.True(options.EnableTasks);
        Assert.True(options.EnableElicitation);
        Assert.True(options.EnableProgress);
        Assert.True(options.EnableCompletion);
    }
}