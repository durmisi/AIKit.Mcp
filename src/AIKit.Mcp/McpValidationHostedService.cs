using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Threading;
using System.Threading.Tasks;

namespace AIKit.Mcp;

// Hosted service for validation
internal class McpValidationHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<McpValidationHostedService> _logger;

    public McpValidationHostedService(IServiceProvider services, ILogger<McpValidationHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MCP configuration validation...");
        McpServiceExtensions.ValidateMcpConfiguration(_services);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}