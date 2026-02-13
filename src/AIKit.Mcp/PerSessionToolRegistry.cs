using System.Collections.Generic;

namespace AIKit.Mcp;

/// <summary>
/// Registry for storing tool types categorized for per-session filtering.
/// </summary>
public class PerSessionToolRegistry
{
    /// <summary>
    /// Gets the dictionary of categorized tool types.
    /// </summary>
    public Dictionary<string, List<System.Type>> CategorizedTools { get; } = new();
}