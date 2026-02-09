using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AIKit.Mcp.Sample;

/// <summary>
/// Example prompt class for conversation helpers
/// </summary>
[McpServerToolType]
public class ConversationPrompts
{
    private readonly ILogger<ConversationPrompts> _logger;

    public ConversationPrompts(ILogger<ConversationPrompts> logger)
    {
        _logger = logger;
    }

    [McpServerPrompt(Name = "greeting")]
    public string GetGreeting(string name = "User", string tone = "friendly")
    {
        var greeting = tone.ToLower() switch
        {
            "formal" => $"Good day, {name}. How may I assist you today?",
            "casual" => $"Hey {name}! What's up?",
            "professional" => $"Hello {name}. I'm here to help with your queries.",
            _ => $"Hello {name}! How can I help you today?"
        };

        _logger.LogInformation("Generated greeting for {Name} with {Tone} tone", name, tone);
        return greeting;
    }

    [McpServerPrompt(Name = "help")]
    public string GetHelp()
    {
        var help = @"
# AIKit Sample MCP Server Help

This is a sample MCP (Model Context Protocol) server demonstrating various capabilities.

## Available Tools
- **add(a, b)**: Add two numbers together
- **multiply(a, b)**: Multiply two numbers
- **power(base, exponent)**: Calculate exponentiation
- **fibonacci(n)**: Generate the nth Fibonacci number

## Available Resources
- **file://sample/info**: Basic server information
- **file://sample/capabilities**: Detailed server capabilities

## Available Prompts
- **greeting(name, tone)**: Generate personalized greetings
- **help**: Show this help message
- **math_explanation(topic)**: Explain mathematical concepts

## Usage Examples
- Ask me to calculate: ""What is 15 + 27?""
- Request Fibonacci: ""What's the 10th Fibonacci number?""
- Get server info: Access the info resource
- Generate greeting: Use the greeting prompt
- Learn about math: Ask for explanations of Fibonacci or exponentiation

Enjoy exploring the MCP protocol!
        ";

        _logger.LogInformation("Providing help information to client");
        return help.Trim();
    }

    [McpServerPrompt(Name = "math_explanation")]
    public string ExplainMath(string topic)
    {
        var explanation = topic.ToLower() switch
        {
            "fibonacci" => @"
The Fibonacci sequence is a series of numbers where each number is the sum of the two preceding ones:
0, 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, ...

It appears in nature (pineapple spirals, sunflower seeds), art, and has applications in computer science and mathematics.",
            "exponentiation" => @"
Exponentiation is raising a number (base) to the power of another number (exponent).
For example: 2³ = 2 × 2 × 2 = 8

The base is multiplied by itself as many times as indicated by the exponent.",
            _ => $"I'm sorry, I don't have a specific explanation for '{topic}' at the moment. Try asking about Fibonacci sequences or exponentiation!"
        };

        _logger.LogInformation("Explained mathematical topic: {Topic}", topic);
        return explanation.Trim();
    }
}