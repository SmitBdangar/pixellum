# Contributing to Pixellum

Thank you for contributing to Pixellum.

Pixellum is a desktop image editor built with C# and Avalonia UI. Contributions are welcome for bug fixes, new tools, workflow improvements, rendering improvements, and documentation updates.

## Development Setup

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

## Project Structure

```text
Pixellum/
├── Controls/       # Custom UI controls
├── Core/           # Document, layers, history, adjustments, file handling
├── Rendering/      # Rendering and compositing logic
├── ViewModels/     # View models
├── Views/          # Dialogs and editor views
├── MainWindow.axaml
├── MainWindow.axaml.cs
├── App.axaml
└── Program.cs
```

## Contribution Guidelines

### UI Changes

- Keep the interface consistent with the current editor style.
- Test changes that affect tools, dialogs, layers, or canvas workflow.
- Include screenshots or short screen recordings for visible UI updates when possible.

### Core and Rendering Changes

- Be careful with changes in `Core/` and `Rendering/` because they affect canvas behavior, history, adjustments, and compositing.
- Keep performance in mind for drawing, transforms, and image operations.
- Preserve undo/redo behavior when changing editing actions.

## Commit Guidelines

Use short, clear commit messages.

Examples:

```text
fix: correct canvas resize anchor behavior
feat: add layer opacity control
refactor: simplify blend mode update flow
docs: improve README screenshots section
```

## Pull Request Checklist

Before opening a pull request, make sure that:

- The project builds successfully.
- The change is scoped to a single improvement or fix.
- UI changes are tested manually.

## Reporting Bugs

When reporting a bug, include:

- What happened
- Steps to reproduce
- Expected behavior
- Operating system
- Screenshots, logs, or sample files if helpful
