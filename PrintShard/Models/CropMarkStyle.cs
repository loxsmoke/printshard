namespace PrintShard.Models;

/// <summary>Specifies the style of crop marks printed on each page.</summary>
public enum CropMarkStyle
{
    /// <summary>No crop marks are printed.</summary>
    None,
    /// <summary>Short L-shaped marks in all four corners.</summary>
    Corners,
    /// <summary>Full lines around the entire crop area.</summary>
    FullLines
}
