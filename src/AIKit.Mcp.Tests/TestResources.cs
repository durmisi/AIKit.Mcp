using ModelContextProtocol.Server;

namespace AIKit.Mcp.Tests;

[McpServerToolType]
[McpServerResourceType]
public class TestResources
{
    [McpServerResource(UriTemplate = "test://data", Name = "Test Data")]
    public string GetTestData()
    {
        return "Test data content";
    }

    [McpServerTool(Name = "get-resource-info")]
    public string GetResourceInfo()
    {
        return "Resource info";
    }
}