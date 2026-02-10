using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIKit.Mcp.Tests;

public class TestStartup
{
    public IConfiguration Configuration { get; }

    public TestStartup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        Console.WriteLine("TestStartup.ConfigureServices called");
        services.AddHttpContextAccessor();

        services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
            });

            mcp.AutoDiscoverTools = true;
            mcp.AutoDiscoverResources = true;
            mcp.AutoDiscoverPrompts = false;
            mcp.EnableProgress = true;
            mcp.EnableCompletion = true;
            mcp.EnableSampling = false;
            mcp.EnableDevelopmentFeatures = true;
            mcp.EnableValidation = true;
        });

        Console.WriteLine("TestStartup.ConfigureServices completed");
    }

    public void Configure(IApplicationBuilder app)
    {
        Console.WriteLine("TestStartup.Configure called");
        // Ensure routing is enabled for MCP endpoints
        app.UseRouting();
        Console.WriteLine("Routing enabled");

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapMcp("/mcp");
        });
        Console.WriteLine("MCP mapped to /mcp");
    }
}