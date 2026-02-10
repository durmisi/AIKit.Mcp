using AIKit.Mcp;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AIKit.Mcp.Tests;

public class McpServerTestBase : WebApplicationFactory<TestStartup>
{
    // TestProgram sets up the services
}