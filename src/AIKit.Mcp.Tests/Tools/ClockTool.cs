using ModelContextProtocol.Server;

namespace AIKit.Mcp.Tests.Tools;

[McpServerToolType]
public class ClockTool
{
    [McpServerTool(Name = "get-current-time")]
    public static string GetCurrentTime()
    {
        return DateTime.Now.ToString("HH:mm:ss");
    }
}