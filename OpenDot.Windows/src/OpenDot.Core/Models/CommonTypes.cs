namespace OpenDot.Core.Models;

public class ToolkitState
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Logo { get; set; }
    public bool Connected { get; set; }
    public string? AccountId { get; set; }
}

public class ComputerInfo
{
    public string Mode { get; set; } = "local"; // "cloud" | "docker" | "local"
    public bool Docker { get; set; }
    public string Image { get; set; } = "ghcr.io/composio-community/open-dot-box:latest";
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 800;
}

public class TriggerApp
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Connected { get; set; }
    public string StatusText => Connected ? "✓ Connected" : "Connect App";
}

public class TriggerField
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
    public List<string>? EnumValues { get; set; }
    public object? DefaultValue { get; set; }
}

public class TriggerType
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<TriggerField> Fields { get; set; } = [];
}

public record FileEntry(string Path, long Size, bool IsDir);
