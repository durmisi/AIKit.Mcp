using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AIKit.Mcp.Tests;

[McpServerResourceType]
public class TestResources
{
    [McpServerResource(UriTemplate = "test://data", Name = "Test Data")]
    public string GetTestData()
    {
        return "Test data content";
    }
}