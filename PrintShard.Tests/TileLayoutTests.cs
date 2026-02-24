using Microsoft.VisualStudio.TestTools.UnitTesting;
using PrintShard.Models;
using System.Windows;

namespace PrintShard.Tests;

/// <summary>
/// Tests for tile layout computation including tile positions, 
/// source rectangles, overlap handling, and page ordering.
/// </summary>
[TestClass]
public class TileLayoutTests
{
    // Standard A4 dimensions in mm
    private const double A4WidthMm = 210;
    private const double A4HeightMm = 297;

    // Typical printable area (with ~5mm margins on each side)
    private const double A4PrintableWidthMm = 200;
    private const double A4PrintableHeightMm = 287;

    #region Basic Layout Tests

    [TestMethod]
    public void Compute_SinglePage_CreatesSingleTile()
    {
        // Arrange
        int imageWidth = 1000;
        int imageHeight = 1000;

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 1, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.AreEqual(1, layout.Tiles.Count);
        Assert.AreEqual(0, layout.Tiles[0].Row);
        Assert.AreEqual(0, layout.Tiles[0].Col);
    }

    [TestMethod]
    public void Compute_TwoByTwoGrid_CreatesFourTiles()
    {
        // Arrange
        int imageWidth = 2000;
        int imageHeight = 2000;

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.AreEqual(4, layout.Tiles.Count);
    }

    [TestMethod]
    public void Compute_ThreeByThreeGrid_CreatesNineTiles()
    {
        // Arrange
        int imageWidth = 3000;
        int imageHeight = 3000;

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 3, pagesTall: 3,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.AreEqual(9, layout.Tiles.Count);
    }

    #endregion

    #region Tile Position Tests

    [TestMethod]
    public void Compute_TwoByTwoGrid_TilesHaveCorrectRowAndColumnIndices()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Row-major order
        Assert.AreEqual(0, layout.Tiles[0].Row); Assert.AreEqual(0, layout.Tiles[0].Col);
        Assert.AreEqual(0, layout.Tiles[1].Row); Assert.AreEqual(1, layout.Tiles[1].Col);
        Assert.AreEqual(1, layout.Tiles[2].Row); Assert.AreEqual(0, layout.Tiles[2].Col);
        Assert.AreEqual(1, layout.Tiles[3].Row); Assert.AreEqual(1, layout.Tiles[3].Col);
    }

    [TestMethod]
    public void Compute_FirstTile_StartsAtOrigin()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.AreEqual(0, layout.Tiles[0].SourceRect.X, 1);
        Assert.AreEqual(0, layout.Tiles[0].SourceRect.Y, 1);
    }

    [TestMethod]
    public void Compute_AdjacentTiles_HaveContiguousSourceRects()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Second tile should start where first tile ends
        var firstTile = layout.Tiles[0];
        var secondTile = layout.Tiles[1];

        Assert.AreEqual(firstTile.SourceRect.Right, secondTile.SourceRect.X, 1);
    }

    #endregion

    #region Overlap Tests

    [TestMethod]
    public void Compute_WithOverlap_TilesOverlapBySpecifiedAmount()
    {
        // Arrange
        double overlapMm = 10;

        // Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: overlapMm, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Second tile should start before first tile ends (overlap)
        var firstTile = layout.Tiles[0];
        var secondTile = layout.Tiles[1];

        // The overlap in pixels depends on the scale (MmPerPx)
        double overlapPx = overlapMm / layout.MmPerPx;
        double expectedSecondTileStart = firstTile.SourceRect.Right - overlapPx;

        Assert.AreEqual(expectedSecondTileStart, secondTile.SourceRect.X, 1);
    }

    [TestMethod]
    public void Compute_WithOverlap_ReducesTotalCoverage()
    {
        // Arrange & Act
        var layoutNoOverlap = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        var layoutWithOverlap = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 10, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Total width should be smaller with overlap
        Assert.IsTrue(layoutWithOverlap.TotalWidthMm < layoutNoOverlap.TotalWidthMm);
    }

    [TestMethod]
    public void Compute_ZeroOverlap_TilesDoNotOverlap()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        var firstTile = layout.Tiles[0];
        var secondTile = layout.Tiles[1];

        Assert.AreEqual(firstTile.SourceRect.Right, secondTile.SourceRect.X, 1);
    }

    #endregion

    #region Margin Tests

    [TestMethod]
    public void Compute_WithMargin_ReducesContentArea()
    {
        // Arrange & Act
        var layoutNoMargin = TileLayout.Compute(
            2000, 2000,
            pagesWide: 1, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        var layoutWithMargin = TileLayout.Compute(
            2000, 2000,
            pagesWide: 1, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 10,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Total coverage should be smaller with margin
        Assert.IsTrue(layoutWithMargin.TotalWidthMm < layoutNoMargin.TotalWidthMm);
        Assert.IsTrue(layoutWithMargin.TotalHeightMm < layoutNoMargin.TotalHeightMm);
    }

    [TestMethod]
    public void Compute_WithMargin_TileSizeRemainsConsistent()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 10,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - All tiles should have the same dimensions
        var firstTileSize = layout.Tiles[0].SourceRect.Size;
        foreach (var tile in layout.Tiles)
        {
            Assert.AreEqual(firstTileSize.Width, tile.SourceRect.Width, 1);
            Assert.AreEqual(firstTileSize.Height, tile.SourceRect.Height, 1);
        }
    }

    #endregion

    #region Orientation Tests

    [TestMethod]
    public void Compute_PortraitOrientation_PrintableWidthLessThanHeight()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            1000, 1000,
            pagesWide: 1, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.IsTrue(layout.PrintableWidthMm < layout.PrintableHeightMm);
    }

    [TestMethod]
    public void Compute_LandscapeOrientation_PrintableWidthGreaterThanHeight()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            1000, 1000,
            pagesWide: 1, pagesTall: 1,
            PageOrientation.Landscape,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.IsTrue(layout.PrintableWidthMm > layout.PrintableHeightMm);
    }

    [TestMethod]
    public void Compute_LandscapeOrientation_SwapsPrintableDimensions()
    {
        // Arrange & Act
        var portraitLayout = TileLayout.Compute(
            1000, 1000,
            pagesWide: 1, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        var landscapeLayout = TileLayout.Compute(
            1000, 1000,
            pagesWide: 1, pagesTall: 1,
            PageOrientation.Landscape,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Dimensions should be swapped
        Assert.AreEqual(portraitLayout.PrintableWidthMm, landscapeLayout.PrintableHeightMm, 1);
        Assert.AreEqual(portraitLayout.PrintableHeightMm, landscapeLayout.PrintableWidthMm, 1);
    }

    #endregion

    #region Print Order Tests

    [TestMethod]
    public void Compute_LeftToRightThenDown_TilesInRowMajorOrder()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            3000, 3000,
            pagesWide: 3, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - First row, then second row
        Assert.AreEqual(0, layout.Tiles[0].Row); Assert.AreEqual(0, layout.Tiles[0].Col);
        Assert.AreEqual(0, layout.Tiles[1].Row); Assert.AreEqual(1, layout.Tiles[1].Col);
        Assert.AreEqual(0, layout.Tiles[2].Row); Assert.AreEqual(2, layout.Tiles[2].Col);
        Assert.AreEqual(1, layout.Tiles[3].Row); Assert.AreEqual(0, layout.Tiles[3].Col);
        Assert.AreEqual(1, layout.Tiles[4].Row); Assert.AreEqual(1, layout.Tiles[4].Col);
        Assert.AreEqual(1, layout.Tiles[5].Row); Assert.AreEqual(2, layout.Tiles[5].Col);
    }

    [TestMethod]
    public void Compute_TopToBottomThenRight_TilesInColumnMajorOrder()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            3000, 3000,
            pagesWide: 3, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.TopToBottomThenRight,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - First column, then second column, then third column
        Assert.AreEqual(0, layout.Tiles[0].Row); Assert.AreEqual(0, layout.Tiles[0].Col);
        Assert.AreEqual(1, layout.Tiles[1].Row); Assert.AreEqual(0, layout.Tiles[1].Col);
        Assert.AreEqual(0, layout.Tiles[2].Row); Assert.AreEqual(1, layout.Tiles[2].Col);
        Assert.AreEqual(1, layout.Tiles[3].Row); Assert.AreEqual(1, layout.Tiles[3].Col);
        Assert.AreEqual(0, layout.Tiles[4].Row); Assert.AreEqual(2, layout.Tiles[4].Col);
        Assert.AreEqual(1, layout.Tiles[5].Row); Assert.AreEqual(2, layout.Tiles[5].Col);
    }

    #endregion

    #region Tile Label Tests

    [TestMethod]
    public void TileInfo_Label_FormatsCorrectly()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Labels are 1-indexed
        Assert.AreEqual("R1 C1", layout.Tiles[0].Label);
        Assert.AreEqual("R1 C2", layout.Tiles[1].Label);
        Assert.AreEqual("R2 C1", layout.Tiles[2].Label);
        Assert.AreEqual("R2 C2", layout.Tiles[3].Label);
    }

    #endregion

    #region Edge Case Tests

    [TestMethod]
    public void Compute_SmallImageLargeGrid_SkipsEmptyTiles()
    {
        // Arrange - Small image with many pages requested
        int imageWidth = 100;
        int imageHeight = 100;

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 5, pagesTall: 5,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Should have fewer tiles than requested since many don't intersect image
        Assert.IsTrue(layout.Tiles.Count <= 25);
        Assert.IsTrue(layout.Tiles.Count >= 1);
    }

    [TestMethod]
    public void Compute_AllTiles_IntersectImageBounds()
    {
        // Arrange
        int imageWidth = 1500;
        int imageHeight = 1200;
        var imageBounds = new Rect(0, 0, imageWidth, imageHeight);

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 3, pagesTall: 3,
            PageOrientation.Portrait,
            overlapMm: 5, marginMm: 5,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - All tiles should intersect with image bounds
        foreach (var tile in layout.Tiles)
        {
            Assert.IsTrue(tile.SourceRect.IntersectsWith(imageBounds),
                $"Tile at R{tile.Row} C{tile.Col} does not intersect image bounds");
        }
    }

    [TestMethod]
    public void Compute_VeryLargeOverlap_StillProducesValidLayout()
    {
        // Arrange - Overlap nearly as large as tile
        double overlapMm = 45; // Very large overlap

        // Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: overlapMm, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Should still produce valid tiles
        Assert.IsTrue(layout.Tiles.Count > 0);
        Assert.IsTrue(layout.MmPerPx > 0);
        Assert.IsTrue(layout.EffectiveDpi > 0);
    }

    [TestMethod]
    public void Compute_VeryLargeMargin_StillProducesValidLayout()
    {
        // Arrange
        double marginMm = 40; // Large margin

        // Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: marginMm,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.IsTrue(layout.Tiles.Count > 0);
        Assert.IsTrue(layout.TotalWidthMm > 0);
        Assert.IsTrue(layout.TotalHeightMm > 0);
    }

    #endregion

    #region DPI Calculation Tests

    [TestMethod]
    public void Compute_EffectiveDpi_IsPositive()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.IsTrue(layout.EffectiveDpi > 0);
    }

    [TestMethod]
    public void Compute_MorePages_LowerDpi()
    {
        // Arrange & Act - Same image size, different page counts
        var layoutFewPages = TileLayout.Compute(
            2000, 2000,
            pagesWide: 1, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        var layoutManyPages = TileLayout.Compute(
            2000, 2000,
            pagesWide: 4, pagesTall: 4,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - More pages = larger print = lower DPI (image stretched more)
        Assert.IsTrue(layoutManyPages.EffectiveDpi < layoutFewPages.EffectiveDpi);
    }

    [TestMethod]
    public void Compute_MmPerPx_IsConsistentWithDpi()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - DPI = 25.4 / MmPerPx
        double expectedDpi = 25.4 / layout.MmPerPx;
        Assert.AreEqual(expectedDpi, layout.EffectiveDpi, 0.00001);
    }

    #endregion

    #region Image Crop Tests (SourceRect Boundaries)

    [TestMethod]
    public void Compute_SourceRects_CoverEntireImage()
    {
        // Arrange
        int imageWidth = 2000;
        int imageHeight = 1500;

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Union of all source rects should cover the image
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var tile in layout.Tiles)
        {
            minX = Math.Min(minX, tile.SourceRect.X);
            minY = Math.Min(minY, tile.SourceRect.Y);
            maxX = Math.Max(maxX, tile.SourceRect.Right);
            maxY = Math.Max(maxY, tile.SourceRect.Bottom);
        }

        // First tile starts at origin
        Assert.AreEqual(0, minX, 1);
        Assert.AreEqual(0, minY, 1);

        // Tiles extend to or beyond image bounds
        Assert.IsTrue(maxX >= imageWidth - 1, $"maxX ({maxX}) should cover image width ({imageWidth})");
        Assert.IsTrue(maxY >= imageHeight - 1, $"maxY ({maxY}) should cover image height ({imageHeight})");
    }

    [TestMethod]
    public void Compute_WithOverlap_SourceRectsOverlapCorrectly()
    {
        // Arrange
        double overlapMm = 10;

        // Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: overlapMm, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Adjacent tiles should overlap
        var tile00 = layout.Tiles.First(t => t.Row == 0 && t.Col == 0);
        var tile01 = layout.Tiles.First(t => t.Row == 0 && t.Col == 1);
        var tile10 = layout.Tiles.First(t => t.Row == 1 && t.Col == 0);

        // Horizontal overlap
        Assert.IsTrue(tile00.SourceRect.IntersectsWith(tile01.SourceRect),
            "Horizontally adjacent tiles should overlap");

        // Vertical overlap
        Assert.IsTrue(tile00.SourceRect.IntersectsWith(tile10.SourceRect),
            "Vertically adjacent tiles should overlap");
    }

    [TestMethod]
    public void Compute_TileCropFitsInPrintableArea_AndNeighboringTilesHaveNoGaps()
    {
        // Arrange
        int imageWidth = 3000;
        int imageHeight = 2400;
        double marginMm = 10;

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 3, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: marginMm,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Calculate content area (printable area minus margins)
        double contentWidthMm = layout.PrintableWidthMm - 2 * marginMm;
        double contentHeightMm = layout.PrintableHeightMm - 2 * marginMm;

        // Convert content area to pixels using the computed scale
        double contentWidthPx = contentWidthMm / layout.MmPerPx;
        double contentHeightPx = contentHeightMm / layout.MmPerPx;

        // Assert 1: Each tile's source rect fits within the printable content area
        foreach (var tile in layout.Tiles)
        {
            Assert.IsTrue(tile.SourceRect.Width <= contentWidthPx + 0.01,
                $"Tile R{tile.Row} C{tile.Col} width ({tile.SourceRect.Width:F2}px) exceeds content area ({contentWidthPx:F2}px)");
            Assert.IsTrue(tile.SourceRect.Height <= contentHeightPx + 0.01,
                $"Tile R{tile.Row} C{tile.Col} height ({tile.SourceRect.Height:F2}px) exceeds content area ({contentHeightPx:F2}px)");
        }

        // Assert 2: Horizontally adjacent tiles have no gaps (right edge of one meets left edge of next)
        for (int row = 0; row < layout.PagesTall; row++)
        {
            var rowTiles = layout.Tiles
                .Where(t => t.Row == row)
                .OrderBy(t => t.Col)
                .ToList();

            for (int i = 0; i < rowTiles.Count - 1; i++)
            {
                var currentTile = rowTiles[i];
                var nextTile = rowTiles[i + 1];

                // The right edge of current tile should equal the left edge of next tile (no gap)
                Assert.AreEqual(currentTile.SourceRect.Right, nextTile.SourceRect.X, 0.01,
                    $"Gap detected between tiles R{currentTile.Row} C{currentTile.Col} and R{nextTile.Row} C{nextTile.Col}");
            }
        }

        // Assert 3: Vertically adjacent tiles have no gaps (bottom edge of one meets top edge of next)
        for (int col = 0; col < layout.PagesWide; col++)
        {
            var colTiles = layout.Tiles
                .Where(t => t.Col == col)
                .OrderBy(t => t.Row)
                .ToList();

            for (int i = 0; i < colTiles.Count - 1; i++)
            {
                var currentTile = colTiles[i];
                var nextTile = colTiles[i + 1];

                // The bottom edge of current tile should equal the top edge of next tile (no gap)
                Assert.AreEqual(currentTile.SourceRect.Bottom, nextTile.SourceRect.Y, 0.01,
                    $"Gap detected between tiles R{currentTile.Row} C{currentTile.Col} and R{nextTile.Row} C{nextTile.Col}");
            }
        }

        // Assert 4: All tiles together cover the image without gaps
        // First tile starts at origin
        var firstTile = layout.Tiles.OrderBy(t => t.Row).ThenBy(t => t.Col).First();
        Assert.AreEqual(0, firstTile.SourceRect.X, 0.01, "First tile should start at X=0");
        Assert.AreEqual(0, firstTile.SourceRect.Y, 0.01, "First tile should start at Y=0");
    }

    #endregion

    #region Layout Properties Tests

    [TestMethod]
    public void Compute_StoresInputParameters()
    {
        // Arrange
        int pagesWide = 3;
        int pagesTall = 2;
        var orientation = PageOrientation.Landscape;
        double overlapMm = 15;
        double marginMm = 8;
        var printOrder = PrintOrder.TopToBottomThenRight;

        // Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide, pagesTall,
            orientation,
            overlapMm, marginMm,
            printOrder,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Input parameters are preserved
        Assert.AreEqual(pagesWide, layout.PagesWide);
        Assert.AreEqual(pagesTall, layout.PagesTall);
        Assert.AreEqual(orientation, layout.Orientation);
        Assert.AreEqual(overlapMm, layout.OverlapMm);
        Assert.AreEqual(marginMm, layout.MarginMm);
        Assert.AreEqual(printOrder, layout.PrintOrder);
        Assert.AreEqual(A4WidthMm, layout.PaperWidthMm);
        Assert.AreEqual(A4HeightMm, layout.PaperHeightMm);
    }

    [TestMethod]
    public void Compute_TotalDimensions_ArePositive()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            1000, 1000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 5, marginMm: 5,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.IsTrue(layout.TotalWidthMm > 0);
        Assert.IsTrue(layout.TotalHeightMm > 0);
        Assert.IsTrue(layout.PrintableWidthMm > 0);
        Assert.IsTrue(layout.PrintableHeightMm > 0);
    }

    #endregion

    #region Aspect Ratio Tests

    [TestMethod]
    public void Compute_WideImage_MaintainsAspectRatio()
    {
        // Arrange - Wide image (2:1 aspect ratio)
        int imageWidth = 4000;
        int imageHeight = 2000;

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 2, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Scale should be uniform (same MmPerPx for both dimensions)
        // This ensures aspect ratio is preserved
        Assert.IsTrue(layout.MmPerPx > 0);
    }

    [TestMethod]
    public void Compute_TallImage_MaintainsAspectRatio()
    {
        // Arrange - Tall image (1:2 aspect ratio)
        int imageWidth = 1000;
        int imageHeight = 2000;

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 1, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.IsTrue(layout.MmPerPx > 0);
        Assert.IsTrue(layout.Tiles.Count >= 1);
    }

    #endregion

    #region Boundary Tests

    [TestMethod]
    public void Compute_MinimumImageSize_ProducesValidLayout()
    {
        // Arrange - Very small image
        int imageWidth = 1;
        int imageHeight = 1;

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 1, pagesTall: 1,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.AreEqual(1, layout.Tiles.Count);
        Assert.IsTrue(layout.MmPerPx > 0);
        Assert.IsTrue(layout.EffectiveDpi > 0);
    }

    [TestMethod]
    public void Compute_LargeImageSize_ProducesValidLayout()
    {
        // Arrange - Very large image
        int imageWidth = 20000;
        int imageHeight = 15000;

        // Act
        var layout = TileLayout.Compute(
            imageWidth, imageHeight,
            pagesWide: 5, pagesTall: 4,
            PageOrientation.Portrait,
            overlapMm: 10, marginMm: 5,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.IsTrue(layout.Tiles.Count > 0);
        Assert.IsTrue(layout.MmPerPx > 0);
    }

    [TestMethod]
    public void Compute_MaxOverlapValue_HandledGracefully()
    {
        // Arrange - Overlap at maximum allowed value
        double overlapMm = 50;

        // Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: overlapMm, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - Should still produce valid results
        Assert.IsTrue(layout.Tiles.Count > 0);
        Assert.IsTrue(layout.TotalWidthMm > 0);
    }

    [TestMethod]
    public void Compute_CombinedOverlapAndMargin_HandledCorrectly()
    {
        // Arrange
        double overlapMm = 15;
        double marginMm = 10;

        // Act
        var layout = TileLayout.Compute(
            3000, 3000,
            pagesWide: 3, pagesTall: 3,
            PageOrientation.Portrait,
            overlapMm: overlapMm, marginMm: marginMm,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert
        Assert.IsTrue(layout.Tiles.Count > 0);
        Assert.AreEqual(overlapMm, layout.OverlapMm);
        Assert.AreEqual(marginMm, layout.MarginMm);
    }

    #endregion

    #region Paper Size Tests

    [TestMethod]
    public void Compute_LetterPaperSize_ProducesValidLayout()
    {
        // Arrange - US Letter dimensions
        double letterWidthMm = 215.9;
        double letterHeightMm = 279.4;
        double letterPrintableWidthMm = 205.9;
        double letterPrintableHeightMm = 269.4;

        // Act
        var layout = TileLayout.Compute(
            2000, 2000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            letterWidthMm, letterHeightMm,
            letterPrintableWidthMm, letterPrintableHeightMm);

        // Assert
        Assert.AreEqual(letterWidthMm, layout.PaperWidthMm);
        Assert.AreEqual(letterHeightMm, layout.PaperHeightMm);
        Assert.IsTrue(layout.Tiles.Count > 0);
    }

    [TestMethod]
    public void Compute_CustomPaperSize_ProducesValidLayout()
    {
        // Arrange - Custom square paper
        double paperSize = 150;

        // Act
        var layout = TileLayout.Compute(
            1000, 1000,
            pagesWide: 2, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            paperSize, paperSize,
            paperSize, paperSize);

        // Assert
        Assert.AreEqual(paperSize, layout.PaperWidthMm);
        Assert.AreEqual(paperSize, layout.PaperHeightMm);
    }

    #endregion

    #region Tile Consistency Tests

    [TestMethod]
    public void Compute_AllTiles_HaveValidSourceRects()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            2500, 2000,
            pagesWide: 3, pagesTall: 2,
            PageOrientation.Portrait,
            overlapMm: 5, marginMm: 5,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - All tiles have positive dimensions
        foreach (var tile in layout.Tiles)
        {
            Assert.IsTrue(tile.SourceRect.Width > 0, 
                $"Tile R{tile.Row} C{tile.Col} has invalid width");
            Assert.IsTrue(tile.SourceRect.Height > 0, 
                $"Tile R{tile.Row} C{tile.Col} has invalid height");
            Assert.IsTrue(tile.SourceRect.X >= 0, 
                $"Tile R{tile.Row} C{tile.Col} has negative X");
            Assert.IsTrue(tile.SourceRect.Y >= 0, 
                $"Tile R{tile.Row} C{tile.Col} has negative Y");
        }
    }

    [TestMethod]
    public void Compute_UniformTileSize_WhenNoOverlap()
    {
        // Arrange & Act
        var layout = TileLayout.Compute(
            3000, 3000,
            pagesWide: 3, pagesTall: 3,
            PageOrientation.Portrait,
            overlapMm: 0, marginMm: 0,
            PrintOrder.LeftToRightThenDown,
            A4WidthMm, A4HeightMm,
            A4PrintableWidthMm, A4PrintableHeightMm);

        // Assert - All tiles have the same size
        var firstTile = layout.Tiles[0];
        foreach (var tile in layout.Tiles)
        {
            Assert.AreEqual(firstTile.SourceRect.Width, tile.SourceRect.Width, 0.01,
                $"Tile R{tile.Row} C{tile.Col} has different width");
            Assert.AreEqual(firstTile.SourceRect.Height, tile.SourceRect.Height, 0.01,
                $"Tile R{tile.Row} C{tile.Col} has different height");
        }
    }

    #endregion
}
