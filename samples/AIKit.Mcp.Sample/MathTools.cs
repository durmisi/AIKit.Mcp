using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AIKit.Mcp.Sample;

/// <summary>
/// Example tool class with mathematical operations
/// </summary>
[McpServerToolType]
public class MathTools
{
    private readonly ILogger<MathTools> _logger;

    public MathTools(ILogger<MathTools> logger)
    {
        _logger = logger;
    }

    [McpServerTool(Name = "add")]
    public double Add(double a, double b)
    {
        var result = a + b;
        _logger.LogInformation("Adding {A} + {B} = {Result}", a, b, result);
        return result;
    }

    [McpServerTool(Name = "multiply")]
    public double Multiply(double a, double b)
    {
        var result = a * b;
        _logger.LogInformation("Multiplying {A} * {B} = {Result}", a, b, result);
        return result;
    }

    [McpServerTool(Name = "power")]
    public double Power(double baseValue, double exponent)
    {
        var result = Math.Pow(baseValue, exponent);
        _logger.LogInformation("Power {Base}^{Exponent} = {Result}", baseValue, exponent, result);
        return result;
    }

    [McpServerTool(Name = "fibonacci")]
    public long Fibonacci(int n)
    {
        if (n < 0) throw new ArgumentException("n must be non-negative", nameof(n));
        if (n == 0) return 0;
        if (n == 1) return 1;

        long a = 0, b = 1;
        for (int i = 2; i <= n; i++)
        {
            var temp = a + b;
            a = b;
            b = temp;
        }

        _logger.LogInformation("Fibonacci({N}) = {Result}", n, b);
        return b;
    }
}