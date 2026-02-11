namespace AIKit.Mcp.Tests;

/// <summary>
/// Test startup class for OAuth integration tests.
/// </summary>
public class OAuthTestStartup
{
    public IConfiguration Configuration { get; }

    public OAuthTestStartup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddAIKitMcp(mcp =>
        {
            mcp.ServerName = "AIKit.Test.Server";
            mcp.ServerVersion = "1.0.0-test";

            mcp.WithHttpTransport(opts =>
            {
                opts.HttpBasePath = "/mcp";
                opts.Authentication = new OAuthAuth
                {
                    OAuthClientId = "demo-client",
                    OAuthClientSecret = "demo-secret",
                    OAuthAuthorizationServerUrl = new Uri("https://localhost:7029"),
                    JwtAudience = "mcp-server",
                    JwtIssuer = "https://localhost:7029"
                };
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
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapMcp("/mcp");
        });
    }
}