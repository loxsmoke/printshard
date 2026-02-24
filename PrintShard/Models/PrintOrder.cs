namespace PrintShard.Models;

/// <summary>Specifies the order in which pages are printed.</summary>
public enum PrintOrder
{
    /// <summary>Print pages row by row (left to right, then top to bottom).</summary>
    LeftToRightThenDown,
    /// <summary>Print pages column by column (top to bottom, then left to right).</summary>
    TopToBottomThenRight
}
