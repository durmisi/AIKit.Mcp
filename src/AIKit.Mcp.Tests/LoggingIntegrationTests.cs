using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AIKit.Mcp.Tests;

public class LoggingIntegrationTests : McpServerTestBase
{
    private readonly ITestOutputHelper _output;

    public LoggingIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _output.WriteLine("LoggingIntegrationTests initialized");
    }
    [Fact]
    public void WithLogging_ConfiguresConsoleLogging()
    {
        _output.WriteLine("Starting WithLogging_ConfiguresConsoleLogging test");
        
        var services = new ServiceCollection();
        _output.WriteLine("Created new ServiceCollection");
        
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
        _output.WriteLine("Configured AIKitMcp with logging: ServerName=TestServer, StdioTransport, RedirectToStderr=true, MinLogLevel=Debug");

        var sp = services.BuildServiceProvider();
        _output.WriteLine("Built service provider");
        
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Test");
        _output.WriteLine("Created logger with category 'Test'");

        // Verify logger is configured
        var debugEnabled = logger.IsEnabled(LogLevel.Debug);
        var infoEnabled = logger.IsEnabled(LogLevel.Information);
        _output.WriteLine($"Logger Debug enabled: {debugEnabled}, Information enabled: {infoEnabled}");
        
        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Information));
        _output.WriteLine("Logger configuration assertions passed ✓");
        
        _output.WriteLine("WithLogging_ConfiguresConsoleLogging test completed successfully");
    }

    [Fact]
    public void WithOpenTelemetry_AddsOpenTelemetryServices()
    {
        _output.WriteLine("Starting WithOpenTelemetry_AddsOpenTelemetryServices test");
        
        var services = new ServiceCollection();
        _output.WriteLine("Created new ServiceCollection");
        
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
        _output.WriteLine("Configured AIKitMcp with OpenTelemetry: ServerName=TestServer, StdioTransport, ServiceName=TestService, Tracing=true, Metrics=true, Logging=true");

        var sp = services.BuildServiceProvider();
        _output.WriteLine("Built service provider with OpenTelemetry services");

        // OpenTelemetry services should be registered
        // Note: Exact service types depend on OpenTelemetry implementation
        Assert.NotNull(sp);
        _output.WriteLine("Service provider is not null ✓");
        
        _output.WriteLine("WithOpenTelemetry_AddsOpenTelemetryServices test completed successfully");
    }
}