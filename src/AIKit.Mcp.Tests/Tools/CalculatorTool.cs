using ModelContextProtocol.Server;

namespace AIKit.Mcp.Tests.Tools;

[McpServerToolType]
public class CalculatorTool
{
    [McpServerTool(Name = "add-numbers")]
    public static int AddNumbers(int a, int b)
    {
        return a + b;
    }

    [McpServerTool(Name = "multiply-numbers")]
    public static int MultiplyNumbers(int a, int b)
    {
        return a * b;
    }
}