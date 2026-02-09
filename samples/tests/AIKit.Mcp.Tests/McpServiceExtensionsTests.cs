using AIKit.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
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

        // Verify hosted services were added (MCP server + validation)
        var hostedServices = services.Where(sd => sd.ServiceType == typeof(IHostedService)).ToList();
        Assert.True(hostedServices.Count >= 2, "Expected at least 2 hosted services (MCP server + validation)");
    }

    [Fact]
    public void ValidateMcpConfiguration_LogsComponentCounts()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Should not throw
        McpServiceExtensions.ValidateMcpConfiguration(serviceProvider);
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
public class SamplePrompt
{
    [McpServerPrompt(Name = "sample_prompt")]
    public string GetSamplePrompt() => "sample prompt content";
}
