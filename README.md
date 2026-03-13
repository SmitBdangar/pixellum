# Pixellum

A lightweight, cross-platform digital painting application built with Avalonia UI and .NET 9. It brings a robust set of Photoshop-like features to a native desktop experience.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)

## Features

- **Robust Toolset** - Brush, Eraser, Eyedropper, Fill Bucket, Marquee Selection, Move, Shape, Text, and Gradient tools.
- **Advanced Brush Engine** - Support for Brush Size, Opacity, Hardness (crisp vs soft edges), and Flow limiters via a context-sensitive **Top Options Bar**.
- **Layer Management** - Unlimited layers with visibility, opacity, locking (transparency/pixels/position), clipping masks, and 16 Photoshop-standard Blend Modes. Includes support for pure Solid Color (Fill) Layers.
- **Image Adjustments** - Live-preview destructive pixel filters: Brightness/Contrast, Hue/Saturation/Lightness, Levels (with Gamma), Curves, and multi-tone Color Balance.
- **Canvas Operations** - Easily Resize Canvas (with clipping/expanding), Resample Image sizes (bilinear scaling), Flip (Horizontal/Vertical), Rotate (90°, 180°), and display a toggleable **Grid Overlay**.
- **File I/O** - Native document creation Dialog, opening standard images (PNG/JPEG/BMP) directly to layers, and saving/exporting directly to PNG.
- **Color System** - Interactive color wheel, quick swatch palette, and hex input. Swap primary/secondary colors with keyboard shortcuts.
- **Infinite Zoom & Pan** - Interactive View manipulation using Mouse Wheel and Spacebar-Drag.
- **Non-Destructive Workflows** - Full, robust global history state tracking (up to 50 steps) complete with a visible **History Panel** for instant visual jump-undo operations across all tool and layer actions.
- **Cross-Platform** - Runs natively on Windows, macOS, and Linux.

## Quick Start

### Building from Source

```bash
# Clone the repository
git clone https://github.com/SmitBdangar/Pixellum.git
cd Pixellum

# Build and run
dotnet build
dotnet run --project Pixellum.csproj
```

**Requirements:**
- .NET 9.0 SDK
- Windows, macOS, or Linux  

## Usage

### Interface Layout
- **Left Panel** - Tools, color picker, and brush engine settings.
- **Center** - Interactive drawing canvas with dashed marquee selection overlay.
- **Right Panel** - Layer hierarchy with Opacity slider and Blend Mode drop-downs.
- **Top Menu** - File (New, Open, Save), Edit (Undo/Redo), Image (Adjustments, Rotations, Canvas Sizing), and View (Zooming, Grid Toggle) operations.

### Keyboard Shortcuts
| Action | Shortcut |
|--------|----------|
| **File / Edit** |
| Undo | `Ctrl+Z` |
| Redo | `Ctrl+Y` |
| New Document | `Ctrl+N` |
| Open Image | `Ctrl+O` |
| Save / Save As | `Ctrl+S` / `Ctrl+Shift+S` |
| **Tools** |
| Brush | `B` |
| Eraser | `E` |
| Eyedropper | `I` |
| Fill | `G` |
| Selection | `M` |
| Move | `V` |
| Shape | `U` |
| Text | `T` |
| Swap Colors | `X` |
| **View / Brush** |
| Pan Canvas | `Space + Drag` |
| Zoom In/Out | `Ctrl + =` / `Ctrl + -` |
| Reset Zoom | `Ctrl + 0` |
| Toggle Grid | `Ctrl + G` |
| Brush Size | `[` and `]` |

## Architecture

- **Framework:** Avalonia UI (MVVM structure backing code-behind UI events)
- **Runtime:** .NET 9.0
- **Graphics Pipeline:** Custom software rasterizer pushing straight alpha straight to a `WriteableBitmap` pointer array (`AlphaFormat.Unpremul`).
- **Layers:** `LayerCompositor` runs standard mathematical blending operations over all pixel layers every frame edit.

## Roadmap

### Upcoming Features
- [ ] Layer Groups (Folders)
- [x] Clipping Masks and layer locks
- [ ] Layer Effects (Drop shadow, glow, stroke)
- [ ] Brush scattered jitter engine & custom alpha tips
- [x] History visual panel & Top options bar
- [ ] Floating dockable interface panels

## License

This project is open source and available under the [MIT License](LICENSE).

## Acknowledgments

Built with [Avalonia UI](https://avaloniaui.net/) - A cross-platform .NET UI framework

## Contact

- **Issues:** [GitHub Issues](https://github.com/SmitBdangar/Pixellum/issues)
- **Author:** Smit Bdangar
