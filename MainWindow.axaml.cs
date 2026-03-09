using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using Pixellum.Views;
using Pixellum.Core;

namespace Pixellum
{
    public partial class MainWindow : Window
    {
        private CanvasView? _canvasView;

        public MainWindow()
        {
            InitializeComponent();

            // Wire canvas events once the visual tree is ready
            this.Loaded += (_, _) =>
            {
                _canvasView = FindCanvasView();
                if (_canvasView == null) return;

                // Status bar — zoom updates
                _canvasView.ZoomChanged += (_, zoom) =>
                {
                    var zoomText = this.FindControl<TextBlock>("ZoomText");
                    if (zoomText != null)
                        zoomText.Text = $"{(int)(zoom * 100)}%";
                };

                // Status bar — tool name updates
                _canvasView.ToolChanged += (_, tool) =>
                {
                    var toolText = this.FindControl<TextBlock>("ActiveToolText");
                    if (toolText != null)
                        toolText.Text = $"Tool: {tool}";
                };

                // Status bar — cursor position
                _canvasView.PointerMoved += (_, e) =>
                {
                    var pos      = e.GetPosition(_canvasView);
                    var posText  = this.FindControl<TextBlock>("CursorPosText");
                    if (posText != null)
                        posText.Text = $"{(int)pos.X}, {(int)pos.Y}";
                };
            };
        }

        private CanvasView? FindCanvasView() =>
            this.GetVisualDescendants().OfType<CanvasView>().FirstOrDefault();

        private void UpdateStatus(string message)
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null) statusText.Text = message;
        }

        // ── File ─────────────────────────────────────────────────────────
        public void OnNewClicked(object? sender, RoutedEventArgs e)
        {
            // TODO: new document dialog
            UpdateStatus("New document… (not yet implemented)");
        }

        public void OnOpenClicked(object? sender, RoutedEventArgs e)
        {
            UpdateStatus("Open… (not yet implemented)");
        }

        public void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            UpdateStatus("Save… (not yet implemented)");
        }

        public async void OnExportPngClicked(object? sender, RoutedEventArgs e)
        {
            var canvas = FindCanvasView();
            if (canvas?.CanvasBitmap == null) return;

            var sp = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (sp == null) return;

            var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title             = "Export PNG",
                SuggestedFileName = "Pixellum_Image",
                FileTypeChoices   = new[] { FilePickerFileTypes.ImagePng }
            });

            if (file != null)
            {
                await FileHandler.ExportPng(canvas.CanvasBitmap, file);
                UpdateStatus($"✅ Exported: {file.Name}");
            }
        }

        // ── Edit ─────────────────────────────────────────────────────────
        public void OnUndoClicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.Undo();
            UpdateStatus("Undo");
        }

        public void OnRedoClicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.Redo();
            UpdateStatus("Redo");
        }

        // ── View ─────────────────────────────────────────────────────────
        public void OnZoomInClicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.ZoomIn();
        }

        public void OnZoomOutClicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.ZoomOut();
        }

        public void OnZoomResetClicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.ZoomReset();
            var zoomText = this.FindControl<TextBlock>("ZoomText");
            if (zoomText != null) zoomText.Text = "100%";
        }
    }
}
