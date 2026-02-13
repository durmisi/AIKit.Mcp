using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AIKit.Mcp.PerSessionTools;

[McpServerToolType]
public class EchoTool
{
    [McpServerTool, Description("Echoes the input back to the client.")]
    public static string Echo([Description("the message to echo")] string message)
    {
        return "hello " + message;
    }
}