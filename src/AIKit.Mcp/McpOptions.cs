using System.Reflection;

namespace AIKit.Mcp;

/// <summary>
/// Options for configuring AIKit MCP server.
/// </summary>
public class McpOptions
{
    /// <summary>
    /// The name of the MCP server.
    /// </summary>
    public string? ServerName { get; set; }

    /// <summary>
    /// The version of the MCP server.
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// The transport type (e.g., "stdio", "http").
    /// </summary>
    public string Transport { get; set; } = "stdio";

    /// <summary>
    /// Whether to auto-discover tools from assemblies.
    /// </summary>
    public bool AutoDiscoverTools { get; set; } = true;

    /// <summary>
    /// Whether to auto-discover resources from assemblies.
    /// </summary>
    public bool AutoDiscoverResources { get; set; } = true;

    /// <summary>
    /// Whether to auto-discover prompts from assemblies.
    /// </summary>
    public bool AutoDiscoverPrompts { get; set; } = true;

    /// <summary>
    /// Whether to enable development features like message tracing.
    /// </summary>
    public bool EnableDevelopmentFeatures { get; set; }

    /// <summary>
    /// Whether to enable configuration validation.
    /// </summary>
    public bool EnableValidation { get; set; }

    /// <summary>
    /// Whether to enable MCP Tasks support for long-running operations.
    /// </summary>
    public bool EnableTasks { get; set; }

    /// <summary>
    /// Whether to enable elicitation support for requesting user input.
    /// </summary>
    public bool EnableElicitation { get; set; }

    /// <summary>
    /// Whether to enable progress tracking for long-running operations.
    /// </summary>
    public bool EnableProgress { get; set; }

    /// <summary>
    /// Whether to enable completion support for auto-completion.
    /// </summary>
    public bool EnableCompletion { get; set; }

    /// <summary>
    /// The assembly to scan for MCP components. If null, uses the calling assembly.
    /// </summary>
    public Assembly? Assembly { get; set; }
}