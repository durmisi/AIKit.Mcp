using ModelContextProtocol.Server;

namespace AIKit.Mcp.Tests;

[McpServerToolType]
public class TestTools
{
    [McpServerTool(Name = "add-numbers")]
    public int AddNumbers(int a, int b)
    {
        return a + b;
    }

    [McpServerTool(Name = "get-current-time")]
    public string GetCurrentTime()
    {
        return DateTime.Now.ToString("HH:mm:ss");
    }
}