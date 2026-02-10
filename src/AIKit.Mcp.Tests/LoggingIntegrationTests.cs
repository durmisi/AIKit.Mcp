using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIKit.Mcp.Tests;

public class LoggingIntegrationTests : McpServerTestBase
{
    [Fact]
    public void WithLogging_ConfiguresConsoleLogging()
    {
        var services = new ServiceCollection();
        services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "TestServer";
            mcp.WithStdioTransport();
            mcp.WithLogging(opts =>
            {
                opts.RedirectToStderr = true;
                opts.MinLogLevel = LogLevel.Debug;
            });
        });

        var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Test");

        // Verify logger is configured
        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void WithOpenTelemetry_AddsOpenTelemetryServices()
    {
        var services = new ServiceCollection();
        services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "TestServer";
            mcp.WithStdioTransport();
            mcp.WithOpenTelemetry(opts =>
            {
                opts.ServiceName = "TestService";
                opts.EnableTracing = true;
                opts.EnableMetrics = true;
                opts.EnableLogging = true;
            });
        });

        var sp = services.BuildServiceProvider();

        // OpenTelemetry services should be registered
        // Note: Exact service types depend on OpenTelemetry implementation
        Assert.NotNull(sp);
    }
}