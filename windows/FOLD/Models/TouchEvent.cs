using System.Text.Json.Serialization;

namespace FOLD.Models;

/// <summary>
/// Touch event sent by the Android tablet as JSON over HTTP POST.
/// Coordinates are normalized (0.0–1.0) relative to the tablet display dimensions.
/// </summary>
public sealed class TouchEvent
{
    /// <summary>Event phase: "down" | "move" | "up"</summary>
    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Horizontal position, 0.0 = left edge, 1.0 = right edge</summary>
    [JsonPropertyName("NormX")]
    public float NormX { get; set; }

    /// <summary>Vertical position, 0.0 = top edge, 1.0 = bottom edge</summary>
    [JsonPropertyName("NormY")]
    public float NormY { get; set; }

    /// <summary>Multi-touch pointer ID (0 = primary finger)</summary>
    [JsonPropertyName("PointerId")]
    public int PointerId { get; set; }
}
