# PrintShard — Application Specification

## Overview

**PrintShard** is a WPF desktop application targeting .NET 9. Its purpose is to load large images, preview them, and print them tiled across multiple physical pages — enabling users to produce large-format prints from standard printers.

---

## Technology Stack

| Concern | Choice |
|---|---|
| UI Framework | WPF (Windows Presentation Foundation) |
| Runtime | .NET 9 (Windows) |
| Language | C# 13 |
| Project type | `net9.0-windows` SDK-style `.csproj` |
| Image decoding | `System.Windows.Media.Imaging` (WIC-backed) |
| Printing | `System.Printing` + `PrintDialog` / `PrintDocument` |
| Layout engine | Custom tile calculator (pure C#) |

---

## Supported Image Formats

At minimum the following formats must load successfully via WIC:

- JPEG / JFIF (`.jpg`, `.jpeg`)
- PNG (`.png`)
- BMP (`.bmp`)
- GIF (`.gif` — first frame)
- TIFF (`.tif`, `.tiff`)
- WEBP (`.webp`)
- ICO (`.ico`)
- RAW pass-through via WIC codecs already installed on the OS (e.g. CR2, NEF)

Opening an unsupported or corrupt file must show a user-friendly error dialog; the previous image (if any) remains loaded.

---

## Application Layout

```
+---------------------------------------------------------------+
|  Menu bar:  File | View | Help                                |
+------------------+--------------------------------------------+
|                  |                                            |
|  CONTROL PANEL   |           PREVIEW CANVAS                   |
|  (fixed width    |   (scrollable, zoomable)                   |
|   ~280 px)       |                                            |
|                  |   +----+----+----+                         |
|  [Printer combo] |   | 1,1| 1,2| 1,3|                         |
|  [Paper  combo]  |   +----+----+----+                         |
|                  |   | 2,1| 2,2| 2,3|                         |
|  Orientation     |   +----+----+----+                         |
|  (*) Portrait    |                                            |
|  ( ) Landscape   |   Page borders drawn in red/dashed         |
|                  |                                            |
|  Pages           |                                            |
|  Cols: [2 ↕]     |                                            |
|  Rows: [3 ↕]     |                                            |
|                  |                                            |
|  Print Order     |                                            |
|  [dropdown]      |                                            |
|                  |                                            |
|  [x] No overlap, |                                            |
|      def margins |                                            |
|  Overlap: [0 ↕]  |                                            |
|  Margin:  [5 ↕]  |                                            |
|                  |                                            |
|  Crop marks      |                                            |
|  [Corners ▼]     |                                            |
|                  |                                            |
|  [ Print...    ] |                                            |
+------------------+--------------------------------------------+
|  Status bar: filename | image size px | paper | pages | DPI   |
+---------------------------------------------------------------+
```

---

## Feature Specifications

### 1. Image Loading

- **File open**: via `File > Open` menu item. Opens a standard `OpenFileDialog` filtered by supported extensions.
- **Drag-and-drop**: dropping an image file onto the preview canvas loads it. If an image is already loaded, the user is asked to confirm replacing it.
- **Recent files**: `File` menu tracks the last 10 opened files (persisted in user settings).
- **Large image handling**: images wider or taller than 8,000 px on either axis are automatically downscaled to fit within 8,000 × 8,000 px before display and printing. The user is warned with a dialog showing original and scaled dimensions.

### 2. Preview Canvas

- Displays the image tiled with overlaid page-break lines.
- **Zoom**: mouse wheel zooms in/out; `Ctrl+0` resets to fit-window. Zoom point follows the mouse cursor.
- **Pan**: click-and-drag pans the canvas.
- **Page grid overlay**: dashed red lines show where each page boundary falls on the image. Each tile cell is labelled with its row/col identifier (e.g. "R2 C3").

### 3. Tile / Layout Controls

All controls live in the left Control Panel and update the preview in real time.

| Control | Type | Range | Default |
|---|---|---|---|
| Printer | ComboBox | installed printers | system default |
| Paper Size | ComboBox | A3, A4, A5, Letter, Legal, Tabloid | from printer |
| Orientation | Radio buttons | Portrait / Landscape | Portrait |
| Columns (pages wide) | Spinner (integer) | 1–20 | 2 |
| Rows (pages tall) | Spinner (integer) | 1–20 | 2 |
| Print Order | ComboBox | Left→Right then Down / Top→Bottom then Right | Left→Right |
| No overlap, default margins | Checkbox | on/off | on |
| Overlap | Spinner (double) | 0–50 (mm or in) | 0 |
| Margin | Spinner (double) | 0–50 (mm or in) | printer minimum |
| Crop Marks | ComboBox | None / Corners / Full Lines | Corners |

**Measurement units**: all dimension values (overlap, margin) use the system locale's measurement unit — millimetres or inches — detected via `RegionInfo.IsMetric`. Spinners increment/decrement by 1 mm or 0.1 in accordingly.

**No overlap, default margins checkbox**: when checked, sets overlap to 0 and margin to the printer's minimum hardware margin, and disables the overlap/margin spinners.

**Excess pages**: if the configured column or row count results in pages that don't intersect the image, those pages are skipped during printing. A warning icon (⚠) appears next to the spinner, with a tooltip explaining that empty pages will not be printed.

Changing any control recomputes the `TileLayout` model immediately and triggers a canvas redraw.

### 4. Tile Layout Model (`TileLayout`)

```
TileLayout
├── PagesWide         : int
├── PagesTall         : int
├── Orientation       : Portrait | Landscape
├── OverlapMm         : double
├── MarginMm          : double
├── PrintOrder        : LeftToRightThenDown | TopToBottomThenRight
├── PaperWidthMm      : double   (portrait-first, from selected paper size)
├── PaperHeightMm     : double
├── PrintableWidthMm  : double   (orientation-aware, from printer capabilities)
├── PrintableHeightMm : double
├── TotalWidthMm      : double   (span of all tiles minus overlaps)
├── TotalHeightMm     : double
├── MmPerPx           : double   (mm per source pixel)
├── EffectiveDpi      : double   (25.4 / MmPerPx)
└── Tiles             : IReadOnlyList<TileInfo>
        TileInfo { Row, Col, SourceRect (pixels), Label }
```

### 5. Printer Selection

- A **Printer** ComboBox in the control panel lists installed printers via `System.Printing`.
- Selecting a printer reads the default paper size from the printer's print ticket and selects the matching entry in the Paper Size ComboBox.
- A **Paper Size** ComboBox allows overriding: A3, A4, A5, Letter, Legal, Tabloid.
- Page dimensions are shown beneath the paper size selector in the appropriate measurement unit.
- Changing the printer or paper size recomputes the layout.

### 6. Print Flow

Clicking **Print...** (or `Ctrl+P`) opens the Print Preview window (see §7). Printing never happens without first showing the preview.

### 7. Print Preview

Opens a modal `PrintPreviewWindow`:

- Shows each tile page individually with Previous / Next navigation.
- Page count indicator: "Page X of Y".
- Clicking **Print...** in the preview window sends all pages to the printer and closes the preview window.
- Clicking **Close** dismisses the preview without printing.
- Pressing `Escape` closes without printing.

### 8. Printing

When Print is confirmed from the preview:

1. Iterates over all non-empty `TileInfo` entries (pages that intersect the image).
2. Each page:
   - Crops the source image to `TileInfo.SourceRect`.
   - Scales the crop to fill the printable area, preserving aspect ratio for edge tiles.
   - Renders crop marks (if enabled) at the boundaries of the image content area, inset by the user margin.
   - Renders the tile label (e.g. "R1 C2") in small grey text if enabled.
3. Pages are sent as a `FixedDocument` via `PrintDialog.PrintDocument`.
4. The print ticket is configured with the selected printer, paper size (using a named `PageMediaSizeName`), and orientation.

**Crop mark styles:**
- **None**: no marks printed.
- **Corners**: short L-shaped lines at all four corners of the content area.
- **Full Lines**: continuous lines forming a complete rectangle around the content area.

### 9. Settings

Persistent settings stored in `%APPDATA%\PrintShard\settings.json`:

| Setting | Type | Default |
|---|---|---|
| Show tile label | bool | true |
| Crop mark style | enum (None/Corners/FullLines) | Corners |
| Recent files list | string[] | [] |
| Last overlap value | double (mm) | 0 |
| Last margin value | double (mm) | printer minimum |
| Last columns | int | 2 |
| Last rows | int | 2 |
| Last orientation | enum | Portrait |
| Last print order | enum | LeftToRightThenDown |
| Last "no overlap" checked | bool | true |

---

## Menus

### File
- Open... `Ctrl+O`
- Recent Files ▶ (submenu, last 10 files)
- ─
- Print... `Ctrl+P`
- ─
- Exit `Alt+F4`

### View
- Zoom In `Ctrl++`
- Zoom Out `Ctrl+-`
- Fit to Window `Ctrl+0`
- ─
- Show Page Borders (toggle)

### Help
- About PrintShard

---

## Status Bar

Left to right:
1. Loaded filename (or "No file loaded")
2. Image dimensions: e.g. `4800 × 3200 px`
3. Selected printer + paper: e.g. `HP LaserJet — A4`
4. Layout summary: e.g. `2 col × 3 row = 6 pages`
5. Effective print DPI: e.g. `~300 DPI`

---

## Error Handling

| Scenario | Response |
|---|---|
| File not found / access denied | MessageBox with message; no state change |
| Unsupported / corrupt image | MessageBox with format suggestion |
| Image > 8,000 px on any axis | Warning dialog; image is downscaled before use |
| No printer installed | Print button disabled |
| Print spooler error | Error dialog with message |

---

## Non-Functional Requirements

- **Startup time**: cold start to ready-for-use under 2 seconds on a modern PC.
- **Preview responsiveness**: any control change must update the preview within 200 ms.
- **DPI-awareness**: the application declares `PerMonitorV2` DPI awareness in its manifest.
- **Platform**: Windows only (x64).

---

## Project Structure

```
PrintShard/
├── PrintShard.sln
├── SPEC.md
├── README.md
├── PrintShard/
│   ├── PrintShard.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs
│   ├── Views/
│   │   └── PrintPreviewWindow.xaml / .cs
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   └── PrintPreviewViewModel.cs
│   ├── Models/
│   │   ├── TileLayout.cs
│   │   ├── TileInfo.cs
│   │   ├── AppSettings.cs
│   │   ├── PaperSize.cs
│   │   ├── PageOrientation.cs
│   │   ├── PrintOrder.cs
│   │   └── CropMarkStyle.cs
│   ├── Services/
│   │   ├── ImageLoaderService.cs
│   │   ├── PrintService.cs
│   │   ├── SettingsService.cs
│   │   └── MeasurementService.cs
│   ├── Controls/
│   │   ├── ImagePreviewCanvas.cs
│   │   └── NumericSpinner.xaml / .cs
│   ├── Converters/
│   │   └── (value converters for XAML bindings)
│   └── Assets/
│       ├── icon.ico
│       ├── Styles.xaml
│       └── ParameterDiagrams.xaml
└── PrintShard.Tests/
    └── PrintShard.Tests.csproj
```

MVVM pattern throughout. ViewModels expose `INotifyPropertyChanged` properties and `ICommand` implementations (`RelayCommand`). No business logic in code-behind.

---

