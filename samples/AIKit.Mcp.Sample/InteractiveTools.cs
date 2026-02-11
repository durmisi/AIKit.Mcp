// 
using AIKit.Mcp.Helpers;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AIKit.Mcp.Sample;

/// <summary>
/// Example tools demonstrating elicitation, completion, and sampling features
/// </summary>
[McpServerToolType]
public class InteractiveTools
{
    private readonly ILogger<InteractiveTools> _logger;

    public InteractiveTools(ILogger<InteractiveTools> logger)
    {
        _logger = logger;
    }

    [McpServerTool(Name = "guess_number")]
    public async Task<string> GuessNumber(McpServer server, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting number guessing game");

        // Generate a random number
        var random = new Random();
        var targetNumber = random.Next(1, 11);

        // Ask for confirmation to play
        var confirmed = await McpElicitationHelpers.RequestConfirmationAsync(
            server,
            "Would you like to play a number guessing game?",
            cancellationToken);

        if (!confirmed)
        {
            return "Maybe next time!";
        }

        // Get player's name
        var playerName = await McpElicitationHelpers.RequestTextInputAsync(
            server,
            "What's your name?",
            minLength: 1,
            maxLength: 50,
            cancellationToken);

        if (string.IsNullOrEmpty(playerName))
        {
            return "Game cancelled.";
        }

        // Play the guessing game
        var attempts = 0;
        while (true)
        {
            attempts++;

            var guess = await McpElicitationHelpers.RequestFormInputAsync(
                server,
                $"Hello {playerName}! Guess a number between 1 and 10 (attempt {attempts}):",
                new ElicitRequestParams.RequestSchema
                {
                    Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                    {
                        ["Guess"] = new ElicitRequestParams.NumberSchema
                        {
                            Type = "integer",
                            Minimum = 1,
                            Maximum = 10,
                            Description = "Your guess (1-10)"
                        }
                    }
                },
                cancellationToken);

            if (guess == null)
            {
                return $"{playerName} gave up after {attempts} attempts. The number was {targetNumber}.";
            }

            var guessValue = guess.TryGetValue("Guess", out var guessElement) ? guessElement.GetInt32() : 0;

            if (guessValue == targetNumber)
            {
                return $"Congratulations {playerName}! You guessed {targetNumber} correctly in {attempts} attempts!";
            }
            else if (guessValue < targetNumber)
            {
                _logger.LogInformation("{Player} guessed {Guess}, which is too low", playerName, guessValue);
            }
            else
            {
                _logger.LogInformation("{Player} guessed {Guess}, which is too high", playerName, guessValue);
            }
        }
    }

    [McpServerTool(Name = "sample_text")]
    public async Task<string> SampleText(McpServer server, string topic, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sampling text about: {Topic}", topic);

        try
        {
            var prompt = $"Write a short, informative paragraph about {topic}. Keep it under 100 words.";
            var generatedText = await McpSamplingHelpers.GenerateTextAsync(
                server,
                prompt,
                maxTokens: 150,
                temperature: 0.7f,
                cancellationToken);

            return $"Generated content about '{topic}':\n\n{generatedText}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sampling failed");
            return $"Sampling not available or failed: {ex.Message}";
        }
    }

    [McpServerTool(Name = "chat_with_ai")]
    public async Task<string> ChatWithAI(McpServer server, string message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Chatting with AI about: {Message}", message);

        try
        {
            var chatResponse = await McpSamplingHelpers.GenerateChatResponseAsync(
                server,
                [
                    new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.System, "You are a helpful assistant. Keep responses concise."),
                    new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, message)
                ],
                maxTokens: 100,
                temperature: 0.7f,
                cancellationToken: cancellationToken);

            return chatResponse.Messages.FirstOrDefault()?.Text ?? "No response generated.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat sampling failed");
            return $"Chat not available or failed: {ex.Message}";
        }
    }
}