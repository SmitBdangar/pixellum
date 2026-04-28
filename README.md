<div align="center">
  <h1>Pixellum</h1>
  <p><strong>A modern desktop image editor built with C# and Avalonia UI.</strong></p>
  <p>
    Pixellum combines a layer-based workflow, custom rendering, image adjustments, and essential drawing tools in a clean cross-platform desktop app.
  </p>

  <p>
    <img src="https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=.net&logoColor=white" alt=".NET 9" />
    <img src="https://img.shields.io/badge/Avalonia-11.3.8-6E56CF?style=for-the-badge" alt="Avalonia 11.3.8" />
    <img src="https://img.shields.io/badge/Desktop-Cross--Platform-111827?style=for-the-badge" alt="Cross Platform Desktop" />
  </p>
</div>

---

## Preview

<p align="center">
  <img src="./assets/screenshot-1.png" alt="Pixellum Screenshot 1" width="48%" />
  <img src="./assets/screenshot-2.png" alt="Pixellum Screenshot 2" width="48%" />
</p>

<p align="center">
  <img src="./assets/screenshot-3.png" alt="Pixellum Screenshot 3" width="48%" />
</p>

## Overview

Pixellum is a raster image editor for desktop use, built around a straightforward editing workflow with layers, color tools, canvas operations, and export support.

The project is organized as a full Avalonia desktop application with dedicated folders for core image logic, rendering, controls, view models, and views. It includes a custom canvas workflow rather than a simple demo-style editor, which makes the repo more interesting for people exploring image editing architecture in C#.

## Highlights

- Built with **.NET 9** and **Avalonia UI**
- Layer-based editing workflow
- Custom rendering and compositing structure
- Undo and redo support through a history system
- Image adjustments such as levels, curves, and color balance
- Canvas resize, resample, rotate, and flip operations
- PNG export workflow
- Clean desktop UI with dialogs, panels, and editing controls

## Features

<table>
<tr>
<td width="50%" valign="top">

### Editing Tools

- Brush
- Eraser
- Fill
- Eyedropper
- Select
- Move
- Shape
- Text
- Gradient

</td>
<td width="50%" valign="top">

### Editor Workflow

- Add layers
- Duplicate layers
- Delete active layer
- Merge layers down
- Change blend modes
- Undo and redo
- Zoom in, zoom out, and reset
- Toggle grid overlay
- Live cursor position display
- Canvas size status display

</td>
</tr>
<tr>
<td width="50%" valign="top">

### Adjustments

- Brightness and contrast
- Hue and saturation
- Levels
- Curves
- Color balance

</td>
<td width="50%" valign="top">

### Image Operations

- Resize canvas with anchor positioning
- Resample image size
- Rotate 90° clockwise
- Rotate 90° counterclockwise
- Rotate 180°
- Flip horizontally
- Flip vertically

</td>
</tr>
</table>

## Why this repo is interesting

Pixellum is more than a UI shell. The repository includes separate core files for document management, layers, history, stroke commands, bitmap creation, file handling, color math, pixel utilities, and image adjustments, which makes it useful both as an app and as a reference project for building desktop graphics software in C#.

The project also exposes a real editor structure around the canvas, with panels and dialogs for things like history, layers, new document creation, canvas sizing, image sizing, and adjustments. That gives the repo practical value for developers who want to study how an image editor is split across UI, rendering, and core logic.

## Architecture

### Core engine

The `Core/` folder contains the main editing logic, including:

- `Document.cs` for document state
- `Layer.cs` for layer representation and dirty region handling
- `HistoryManager.cs` for undo and redo
- `StrokeCommand.cs` for undoable drawing actions
- `Adjustments.cs` for image adjustment operations
- `ColorMath.cs` for blend mode parsing and color math
- `FileHandler.cs` for open, save, and export handling
- `PixelUtils.cs` and `BitmapFactory.cs` for pixel and bitmap helpers

### App structure

The repo is separated into clear application layers:

- `Controls/` for custom controls
- `Rendering/` for rendering and compositing logic
- `ViewModels/` for MVVM view models
- `Views/` for dialogs and UI views
- `MainWindow.axaml` and `MainWindow.axaml.cs` for the main editor window and command wiring

## File Support

| Action | Supported Formats |
|---|---|
| Open | PNG, JPG, JPEG, BMP, GIF, WebP |
| Save | PNG |
| Export | PNG |

## Tech Stack

- .NET 9.0
- Avalonia 11.3.8
- Avalonia Desktop
- Avalonia Fluent Theme
- Avalonia ColorPicker
- C#

## Project Structure

```text
Pixellum/
├── assets/               # Screenshots and repo media
├── Controls/             # Custom editor controls
├── Core/                 # Document, layers, history, file handling, adjustments
├── Rendering/            # Rendering and compositing logic
├── ViewModels/           # View models
├── Views/                # Dialogs and editor panels
├── App.axaml
├── App.axaml.cs
├── MainWindow.axaml
├── MainWindow.axaml.cs
├── Program.cs
├── Pixellum.csproj
└── Pixellum.sln
```

## Getting Started

### Prerequisites

- .NET 9 SDK

### Run locally

```bash
git clone https://github.com/SmitBdangar/pixellum.git
cd pixellum
dotnet run
```

### Build

```bash
dotnet build
```

### Publish

```bash
dotnet publish -c Release
```

## Contributing

Contributions are welcome through issues and pull requests. For larger changes, open an issue first to discuss the proposed update.

## Author

Built by [Smit Dangar](https://github.com/SmitBdangar)
