using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AIKit.Mcp.Helpers;

/// <summary>
/// Provides simplified methods for handling completion requests in MCP servers.
/// </summary>
public static class McpCompletionHelpers
{
    /// <summary>
    /// Creates a completion handler that provides suggestions based on a predefined dictionary.
    /// </summary>
    /// <param name="completions">Dictionary mapping argument names to their possible values.</param>
    /// <returns>A completion handler function.</returns>
    public static McpRequestHandler<CompleteRequestParams, CompleteResult> CreateDictionaryCompletionHandler(
        IReadOnlyDictionary<string, IEnumerable<string>> completions)
    {
        return async (request, cancellationToken) =>
        {
            if (request.Params?.Argument is not { } argument)
            {
                return new CompleteResult();
            }

            if (!completions.TryGetValue(argument.Name, out var values))
            {
                return new CompleteResult();
            }

            // Filter values that start with the current input
            var filteredValues = values
                .Where(v => v.StartsWith(argument.Value, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return new CompleteResult
            {
                Completion = new Completion
                {
                    Values = filteredValues,
                    Total = filteredValues.Length,
                    HasMore = false
                }
            };
        };
    }

    /// <summary>
    /// Creates a completion handler for resource references that filters based on a list of available resources.
    /// </summary>
    /// <param name="resourceIds">List of available resource IDs.</param>
    /// <returns>A completion handler function.</returns>
    public static McpRequestHandler<CompleteRequestParams, CompleteResult> CreateResourceCompletionHandler(
        IEnumerable<string> resourceIds)
    {
        var resourceList = resourceIds.ToArray();
        return async (request, cancellationToken) =>
        {
            if (request.Params?.Ref is not ResourceTemplateReference rtr ||
                request.Params.Argument is not { } argument)
            {
                return new CompleteResult();
            }

            // Filter resource IDs that start with the current input
            var filteredIds = resourceList
                .Where(id => id.StartsWith(argument.Value, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return new CompleteResult
            {
                Completion = new Completion
                {
                    Values = filteredIds,
                    Total = filteredIds.Length,
                    HasMore = false
                }
            };
        };
    }

    /// <summary>
    /// Creates a dynamic completion handler that uses a custom function to provide suggestions.
    /// </summary>
    /// <param name="completionFunc">Function that takes the argument name and current value, returns possible completions.</param>
    /// <returns>A completion handler function.</returns>
    public static McpRequestHandler<CompleteRequestParams, CompleteResult> CreateDynamicCompletionHandler(
        Func<string, string, IEnumerable<string>> completionFunc)
    {
        return async (request, cancellationToken) =>
        {
            if (request.Params?.Argument is not { } argument)
            {
                return new CompleteResult();
            }

            var values = completionFunc(argument.Name, argument.Value).ToArray();

            return new CompleteResult
            {
                Completion = new Completion
                {
                    Values = values,
                    Total = values.Length,
                    HasMore = false
                }
            };
        };
    }

    /// <summary>
    /// Creates a completion handler for prompt references that filters based on a list of available prompts.
    /// </summary>
    /// <param name="promptNames">List of available prompt names.</param>
    /// <returns>A completion handler function.</returns>
    public static McpRequestHandler<CompleteRequestParams, CompleteResult> CreatePromptCompletionHandler(
        IEnumerable<string> promptNames)
    {
        var promptList = promptNames.ToArray();
        return async (request, cancellationToken) =>
        {
            if (request.Params?.Ref is not PromptReference pr ||
                request.Params.Argument is not { } argument)
            {
                return new CompleteResult();
            }

            // Filter prompt names that start with the current input
            var filteredNames = promptList
                .Where(name => name.StartsWith(argument.Value, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return new CompleteResult
            {
                Completion = new Completion
                {
                    Values = filteredNames,
                    Total = filteredNames.Length,
                    HasMore = false
                }
            };
        };
    }
}