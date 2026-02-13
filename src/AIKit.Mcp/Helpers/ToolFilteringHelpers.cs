using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using System.Reflection;
using System.Text.Json;

namespace AIKit.Mcp.Helpers;

/// <summary>
/// Provides helper methods for filtering tools and resources per session.
/// </summary>
public static class ToolFilteringHelpers
{
    /// <summary>
    /// Gets all tool names for the specified tool types using reflection.
    /// </summary>
    public static string[] GetToolNamesForTypes(params Type[] toolTypes)
    {
        var toolNames = new List<string>();
        foreach (var type in toolTypes)
        {
            toolNames.AddRange(GetToolNamesForType(type));
        }
        return toolNames.ToArray();
    }

    /// <summary>
    /// Gets all tool names for a specific tool type using reflection.
    /// </summary>
    public static string[] GetToolNamesForType(Type toolType)
    {
        var toolNames = new List<string>();
        var methods = toolType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var toolAttribute = method.GetCustomAttribute<McpServerToolAttribute>();
            if (toolAttribute != null)
            {
                toolNames.Add(toolAttribute.Name ?? method.Name);
            }
        }

        return toolNames.ToArray();
    }

    /// <summary>
    /// Generates a JSON schema for the method parameters.
    /// </summary>
    private static JsonElement GenerateInputSchema(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            var paramType = param.ParameterType;
            var schema = GetJsonSchemaForType(paramType);
            properties[param.Name!] = schema;
            if (!param.HasDefaultValue)
            {
                required.Add(param.Name!);
            }
        }

        var schemaObj = new
        {
            type = "object",
            properties,
            required
        };

        return JsonSerializer.SerializeToElement(schemaObj);
    }

    /// <summary>
    /// Gets a simple JSON schema for a .NET type.
    /// </summary>
    private static object GetJsonSchemaForType(Type type)
    {
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
        {
            return new { type = "integer" };
        }
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return new { type = "number" };
        }
        if (type == typeof(bool))
        {
            return new { type = "boolean" };
        }
        if (type == typeof(string))
        {
            return new { type = "string" };
        }
        // For other types, assume object
        return new { type = "object" };
    }

    /// <summary>
    /// Extracts the tool category from the HTTP context route values.
    /// </summary>
    public static string GetToolCategoryFromRoute(HttpContext context, string defaultCategory = "all")
    {
        return context.Request.RouteValues["toolCategory"]?.ToString()?.ToLower() ?? defaultCategory;
    }
}