// 
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace AIKit.Mcp.Helpers;

/// <summary>
/// Provides simplified methods for handling elicitation requests in MCP servers. 
/// </summary>
public static class McpElicitationHelpers
{
    /// <summary>
    /// Requests user input using a simple form with the specified schema.
    /// </summary>
    /// <param name="server">The MCP server instance.</param>
    /// <param name="message">The message to display to the user.</param>
    /// <param name="schema">The JSON schema defining the form fields.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user's response content, or null if cancelled/declined.</returns>
    public static async Task<IDictionary<string, JsonElement>?> RequestFormInputAsync(
        McpServer server,
        string message,
        ElicitRequestParams.RequestSchema schema,
        CancellationToken cancellationToken = default)
    {
        var result = await server.ElicitAsync(new ElicitRequestParams
        {
            Mode = "form",
            Message = message,
            RequestedSchema = schema
        }, cancellationToken);

        return result.Action == "accept" ? result.Content : null;
    }

    /// <summary>
    /// Requests user confirmation with Yes/No options.
    /// </summary>
    /// <param name="server">The MCP server instance.</param>
    /// <param name="message">The confirmation message to display.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if user accepted, false if declined or cancelled.</returns>
    public static async Task<bool> RequestConfirmationAsync(
        McpServer server,
        string message,
        CancellationToken cancellationToken = default)
    {
        var schema = new ElicitRequestParams.RequestSchema
        {
            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
            {
                ["Answer"] = new ElicitRequestParams.BooleanSchema
                {
                    Description = "Yes/No confirmation"
                }
            }
        };

        var result = await RequestFormInputAsync(server, message, schema, cancellationToken);
        return result != null && result.TryGetValue("Answer", out var element) && element.GetBoolean();
    }

    /// <summary>
    /// Requests a text input from the user.
    /// </summary>
    /// <param name="server">The MCP server instance.</param>
    /// <param name="message">The prompt message.</param>
    /// <param name="minLength">Minimum length of the input.</param>
    /// <param name="maxLength">Maximum length of the input.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user's text input, or null if cancelled.</returns>
    public static async Task<string?> RequestTextInputAsync(
        McpServer server,
        string message,
        int? minLength = null,
        int? maxLength = null,
        CancellationToken cancellationToken = default)
    {
        var stringSchema = new ElicitRequestParams.StringSchema
        {
            Description = "Text input"
        };

        if (minLength.HasValue)
            stringSchema.MinLength = minLength.Value;
        if (maxLength.HasValue)
            stringSchema.MaxLength = maxLength.Value;

        var schema = new ElicitRequestParams.RequestSchema
        {
            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
            {
                ["Input"] = stringSchema
            }
        };

        var result = await RequestFormInputAsync(server, message, schema, cancellationToken);
        return result != null && result.TryGetValue("Input", out var element) ? element.GetString() : null;
    }

    /// <summary>
    /// Requests an email address from the user.
    /// </summary>
    /// <param name="server">The MCP server instance.</param>
    /// <param name="message">The prompt message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user's email input, or null if cancelled.</returns>
    public static async Task<string?> RequestEmailInputAsync(
        McpServer server,
        string message,
        CancellationToken cancellationToken = default)
    {
        var schema = new ElicitRequestParams.RequestSchema
        {
            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
            {
                ["Email"] = new ElicitRequestParams.StringSchema
                {
                    Description = "Email address",
                    Format = "email"
                }
            }
        };

        var result = await RequestFormInputAsync(server, message, schema, cancellationToken);
        return result != null && result.TryGetValue("Email", out var element) ? element.GetString() : null;
    }

    /// <summary>
    /// Requests a date from the user.
    /// </summary>
    /// <param name="server">The MCP server instance.</param>
    /// <param name="message">The prompt message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user's date input, or null if cancelled.</returns>
    public static async Task<DateTime?> RequestDateInputAsync(
        McpServer server,
        string message,
        CancellationToken cancellationToken = default)
    {
        var schema = new ElicitRequestParams.RequestSchema
        {
            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
            {
                ["Date"] = new ElicitRequestParams.StringSchema
                {
                    Description = "Date in YYYY-MM-DD format",
                    Format = "date"
                }
            }
        };

        var result = await RequestFormInputAsync(server, message, schema, cancellationToken);
        if (result != null && result.TryGetValue("Date", out var element))
        {
            var dateStr = element.GetString();
            if (DateTime.TryParse(dateStr, out var date))
            {
                return date;
            }
        }
        return null;
    }

    /// <summary>
    /// Requests a number input from the user within a specified range.
    /// </summary>
    /// <param name="server">The MCP server instance.</param>
    /// <param name="message">The prompt message.</param>
    /// <param name="min">Minimum value.</param>
    /// <param name="max">Maximum value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user's number input, or null if cancelled.</returns>
    public static async Task<double?> RequestNumberInputAsync(
        McpServer server,
        string message,
        double? min = null,
        double? max = null,
        CancellationToken cancellationToken = default)
    {
        var numberSchema = new ElicitRequestParams.NumberSchema
        {
            Description = "Numeric input"
        };

        if (min.HasValue)
            numberSchema.Minimum = min.Value;
        if (max.HasValue)
            numberSchema.Maximum = max.Value;

        var schema = new ElicitRequestParams.RequestSchema
        {
            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
            {
                ["Number"] = numberSchema
            }
        };

        var result = await RequestFormInputAsync(server, message, schema, cancellationToken);
        return result != null && result.TryGetValue("Number", out var element) ? element.GetDouble() : null;
    }

    /// <summary>
    /// Requests user input via an external URL (out-of-band interaction).
    /// </summary>
    /// <param name="server">The MCP server instance.</param>
    /// <param name="message">The message to display with the URL.</param>
    /// <param name="url">The URL to open for the interaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if user accepted the URL interaction, false if declined.</returns>
    public static async Task<bool> RequestUrlInteractionAsync(
        McpServer server,
        string message,
        string url,
        CancellationToken cancellationToken = default)
    {
        var result = await server.ElicitAsync(new ElicitRequestParams
        {
            Mode = "url",
            Message = message,
            Url = url
        }, cancellationToken);

        return result.Action == "accept";
    }
}