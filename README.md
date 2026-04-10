# Pixellum

A Photoshop-inspired raster image editor built with **Avalonia UI** and **.NET 9**.

## Features

### Canvas & Document
- New document dialog with custom dimensions and background (transparent, white, black)
- Open images (PNG, JPEG, BMP, GIF, WebP)
- Save / Save As / Export PNG
- Canvas resize with anchor positioning
- Image resampling (bilinear interpolation)

### Drawing Tools
- **Brush** — Variable size, opacity, hardness, and flow with soft/hard falloff
- **Eraser** — Same brush engine with alpha subtraction
- **Eyedropper** — Pick color from canvas
- **Fill (Bucket)** — Scan-line flood fill with tolerance
- **Shape** — Rectangle drawing tool
- **Text** — Click-to-place text with font customization
- **Gradient** — Foreground-to-background linear gradient
- **Move** — Translate layer pixels
- **Selection** — Rectangular marquee selection

### Layer System
- Unlimited layers with thumbnails
- Per-layer opacity, visibility toggle
- 16 blend modes: Normal, Darken, Multiply, ColorBurn, Lighten, Screen, ColorDodge, Overlay, SoftLight, HardLight, Difference, Exclusion, Hue, Saturation, Color, Luminosity
- Layer reorder (move up/down), rename, duplicate, merge down, delete
- Lock transparency, lock pixels, lock position
- Clipping masks

### Adjustments
- Brightness / Contrast
- Hue / Saturation / Lightness
- Levels (input/output black/white points + gamma)
- Curves (per-channel LUT)
- Color Balance (shadows / midtones / highlights)
- Live preview dialogs

### Navigation
- Zoom in/out (scroll wheel, Ctrl+/Ctrl-)
- Pan (middle mouse or Space+drag)
- Pixel grid overlay
- Rulers
- Status bar with zoom, cursor position, canvas size, color mode

### Undo / Redo
- Up to 50 undo steps with full history panel
- Click any history state to jump back

### Keyboard Shortcuts
| Shortcut | Action |
|---|---|
| `B` | Brush tool |
| `E` | Eraser tool |
| `I` | Eyedropper |
| `G` | Fill tool |
| `[` / `]` | Decrease / increase brush size |
| `X` | Swap foreground/background colors |
| `Ctrl+N` | New document |
| `Ctrl+O` | Open image |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As |
| `Ctrl+E` | Export PNG |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Ctrl+G` | Toggle grid |
| `Ctrl++` / `Ctrl+-` | Zoom in / out |
| `Ctrl+0` | Reset zoom |

## Build & Run

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

## Project Structure

```
Pixellum/
├── Core/                   # Domain logic
│   ├── Adjustments.cs      # Brightness, HSL, Levels, Curves, Color Balance
│   ├── BitmapFactory.cs    # WriteableBitmap creation helper
│   ├── ColorExtensions.cs  # Hex color parsing
│   ├── ColorMath.cs        # Shared HSL↔RGB, alpha compositing, blend mode parsing
│   ├── Document.cs         # Canvas document (pixel buffer + dirty tracking)
│   ├── FileHandler.cs      # Image I/O (open, save, export)
│   ├── HistoryManager.cs   # Undo/redo state management
│   ├── ICommand.cs         # Command pattern interface
│   ├── IntRect.cs          # Integer rectangle math (union, intersect)
│   ├── Layer.cs            # Layer model (pixels, blend mode, locks)
│   ├── PixelUtils.cs       # Region pixel extraction
│   ├── StrokeCommand.cs    # Delta-based stroke undo command
│   └── Tools.cs            # ToolType enum
├── Controls/               # Custom Avalonia controls
│   ├── ColorWheel          # HSV color wheel
│   ├── ColorSVPad          # Saturation/value pad
│   └── HexColorGrid        # Hex color grid input
├── Rendering/              # Pixel pipeline
│   ├── BrushEngine.cs      # Brush/eraser stamping with falloff
│   ├── LayerCompositor.cs  # Layer compositing with blend modes
│   └── Renderer.cs         # Document → WriteableBitmap blit
├── Views/                  # UI panels
│   ├── CanvasView           # Main drawing surface
│   ├── ToolsPanel           # Tool buttons + color swatches
│   ├── LayersPanel          # Layer list + management
│   ├── TopOptionsBar        # Brush settings toolbar
│   ├── HistoryPanel         # Undo/redo history list
│   ├── AdjustmentsDialog    # Live-preview adjustment dialogs
│   ├── AdjustmentsPanel     # Quick-access adjustment buttons
│   ├── NewDocumentDialog    # New canvas dialog
│   └── SizeDialogs          # Canvas size / image size dialogs
├── MainWindow.axaml(.cs)   # Application shell + menu routing
├── App.axaml(.cs)          # Application entry
└── Program.cs              # Main entry point
```

## Tech Stack
- **Framework:** [Avalonia UI](https://avaloniaui.net/) 11.3
- **Runtime:** .NET 9
- **Language:** C# 13
- **Rendering:** Software pixel compositing with dirty-rect optimization

## License

This project is currently unlicensed. Please add a license file before distributing.
