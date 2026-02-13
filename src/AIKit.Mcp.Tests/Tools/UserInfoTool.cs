using ModelContextProtocol.Server;

namespace AIKit.Mcp.Tests.Tools;

[McpServerToolType]
public class UserInfoTool
{
    [McpServerTool(Name = "get-user-name")]
    public static string GetUserName()
    {
        return "TestUser";
    }
}