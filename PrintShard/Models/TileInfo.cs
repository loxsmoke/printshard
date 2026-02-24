using System.Windows;

namespace PrintShard.Models;

/// <summary>Describes a single page tile: which portion of the source image it contains.</summary>
public sealed class TileInfo
{
    /// <summary>Zero-based row index in the tile grid.</summary>
    public int Row { get; init; }

    /// <summary>Zero-based column index in the tile grid.</summary>
    public int Col { get; init; }

    /// <summary>
    /// Rectangle in source image pixel coordinates that maps onto the printable area of this tile.
    /// May extend beyond the image bounds — callers must clamp before use.
    /// </summary>
    public Rect SourceRect { get; init; }

    /// <summary>Human-readable label, e.g. "R1 C2".</summary>
    public string Label => $"R{Row + 1} C{Col + 1}";
}
