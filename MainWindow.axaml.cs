using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using Pixellum.Views;
using Pixellum.Core;
using Pixellum.Rendering;

namespace Pixellum
{
    public partial class MainWindow : Window
    {
        private CanvasView?  _canvasView;
        private string?      _lastSavePath;   // for Ctrl+S "save in place"
        private bool         _gridVisible = false;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += (_, _) =>
            {
                _canvasView = FindCanvasView();
                if (_canvasView == null) return;

                var topOptions = this.FindControl<TopOptionsBar>("TopOptionsBarControl");
                if (topOptions != null)
                    topOptions.SetCanvas(_canvasView);

                var historyPanel = this.FindControl<HistoryPanel>("HistoryPanelControl");
                if (historyPanel != null)
                    historyPanel.SetCanvas(_canvasView);

                _canvasView.ZoomChanged += (_, zoom) =>
                {
                    var pct = $"{(int)(zoom * 100)}%";
                    var zoomText = this.FindControl<TextBlock>("ZoomText");
                    if (zoomText != null) zoomText.Text = pct;
                    var statusZoom = this.FindControl<TextBlock>("StatusZoomText");
                    if (statusZoom != null) statusZoom.Text = pct;
                };

                _canvasView.ToolChanged += (_, tool) =>
                {
                    var toolText = this.FindControl<TextBlock>("ActiveToolText");
                    if (toolText != null)
                        toolText.Text = $"Tool: {tool}";
                };

                _canvasView.PointerMoved += (_, e) =>
                {
                    var pos     = e.GetPosition(_canvasView);
                    var posText = this.FindControl<TextBlock>("CursorPosText");
                    if (posText != null)
                        posText.Text = $"{(int)pos.X}, {(int)pos.Y} px";
                };

                UpdateCanvasSizeStatus();
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private CanvasView? FindCanvasView() =>
            this.GetVisualDescendants().OfType<CanvasView>().FirstOrDefault();

        private void UpdateStatus(string message)
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null) statusText.Text = message;
        }

        private void UpdateCanvasSizeStatus()
        {
            if (_canvasView == null) return;
            var sizeText = this.FindControl<TextBlock>("CanvasSizeText");
            if (sizeText != null)
                sizeText.Text = $"{_canvasView.CanvasWidth} × {_canvasView.CanvasHeight}";
        }

        private void RefreshLayersPanel()
        {
            var lp = this.GetVisualDescendants().OfType<LayersPanel>().FirstOrDefault();
            lp?.RefreshLayersList();
        }

        // ── File menu ─────────────────────────────────────────────────────────

        public async void OnNewClicked(object? sender, RoutedEventArgs e)
        {
            var dialog = new Views.NewDocumentDialog();
            await dialog.ShowDialog(this);

            if (!dialog.Confirmed) return;

            _canvasView = FindCanvasView();
            _canvasView?.NewDocument(dialog.DocWidth, dialog.DocHeight, dialog.BackgroundChoice);
            _lastSavePath = null;
            UpdateCanvasSizeStatus();
            RefreshLayersPanel();
            UpdateStatus($"New canvas: {dialog.DocWidth} × {dialog.DocHeight}");
        }

        public async void OnOpenClicked(object? sender, RoutedEventArgs e)
        {
            var sp = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (sp == null) return;

            var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title         = "Open Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                    },
                    FilePickerFileTypes.All
                }
            });

            if (files.Count == 0) return;

            var result = await FileHandler.OpenImage(files[0]);
            if (result == null) { UpdateStatus("Failed to open image."); return; }

            var (pixels, w, h) = result.Value;

            _canvasView = FindCanvasView();
            _canvasView?.LoadImageOntoLayer(pixels, w, h, resizeCanvas: true);
            _lastSavePath = null;
            UpdateCanvasSizeStatus();
            RefreshLayersPanel();
            UpdateStatus($"Opened: {files[0].Name}  ({w} × {h})");
        }

        public async void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            if (_lastSavePath != null)
            {
                var cv = FindCanvasView();
                if (cv?.CanvasBitmap == null) return;
                await FileHandler.SavePng(cv.CanvasBitmap, _lastSavePath);
                UpdateStatus($"Saved: {System.IO.Path.GetFileName(_lastSavePath)}");
            }
            else
            {
                await SaveAs();
            }
        }

        public async void OnSaveAsClicked(object? sender, RoutedEventArgs e)
        {
            await SaveAs();
        }

        private async System.Threading.Tasks.Task SaveAs()
        {
            var cv = FindCanvasView();
            if (cv?.CanvasBitmap == null) return;

            var sp = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (sp == null) return;

            var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title             = "Save PNG",
                SuggestedFileName = "Pixellum_Image",
                FileTypeChoices   = new[] { FilePickerFileTypes.ImagePng }
            });

            if (file == null) return;

            await FileHandler.ExportPng(cv.CanvasBitmap, file);
            _lastSavePath = file.TryGetLocalPath();
            UpdateStatus($"Saved: {file.Name}");
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
                UpdateStatus($"Exported: {file.Name}");
            }
        }

        public void OnCloseDocClicked(object? sender, RoutedEventArgs e)
        {
            // Reset back to a blank 800×600 transparent canvas
            _canvasView = FindCanvasView();
            _canvasView?.NewDocument(800, 600, 0);
            _lastSavePath = null;
            UpdateCanvasSizeStatus();
            RefreshLayersPanel();
            UpdateStatus("Canvas closed — new 800×600 canvas");
        }

        // ── Edit menu ─────────────────────────────────────────────────────────

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

        // ── View menu ─────────────────────────────────────────────────────────

        public void OnZoomInClicked(object? sender, RoutedEventArgs e)    => FindCanvasView()?.ZoomIn();
        public void OnZoomOutClicked(object? sender, RoutedEventArgs e)   => FindCanvasView()?.ZoomOut();
        public void OnZoomResetClicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.ZoomReset();
            var zoomText = this.FindControl<TextBlock>("ZoomText");
            if (zoomText != null) zoomText.Text = "100%";
        }

        public void OnToggleGridClicked(object? sender, RoutedEventArgs e)
        {
            _gridVisible = !_gridVisible;
            FindCanvasView()?.SetGridVisible(_gridVisible);
            UpdateStatus(_gridVisible ? "Grid: On" : "Grid: Off");
        }

        // ── Image menu ────────────────────────────────────────────────────────

        public async void OnCanvasSizeClicked(object? sender, RoutedEventArgs e)
        {
            var cv = FindCanvasView();
            if (cv == null) return;

            var dialog = new CanvasSizeDialog(cv.CanvasWidth, cv.CanvasHeight);
            await dialog.ShowDialog(this);
            if (!dialog.Confirmed) return;

            cv.ResizeCanvas(dialog.NewWidth, dialog.NewHeight, dialog.AnchorX, dialog.AnchorY);
            UpdateCanvasSizeStatus();
            RefreshLayersPanel();
            UpdateStatus($"Canvas resized to {dialog.NewWidth} × {dialog.NewHeight}");
        }

        public async void OnImageSizeClicked(object? sender, RoutedEventArgs e)
        {
            var cv = FindCanvasView();
            if (cv == null) return;

            var dialog = new ImageSizeDialog(cv.CanvasWidth, cv.CanvasHeight);
            await dialog.ShowDialog(this);
            if (!dialog.Confirmed) return;

            cv.ResampleImage(dialog.NewWidth, dialog.NewHeight);
            UpdateCanvasSizeStatus();
            RefreshLayersPanel();
            UpdateStatus($"Image resampled to {dialog.NewWidth} × {dialog.NewHeight}");
        }

        // Adjustments
        public async void OnBrightnessContrastClicked(object? sender, RoutedEventArgs e)
        {
            var cv = FindCanvasView();
            if (cv == null) return;
            var dialog = new AdjustmentsDialog(AdjustmentType.BrightnessContrast, cv);
            await dialog.ShowDialog(this);
        }

        public async void OnHueSaturationClicked(object? sender, RoutedEventArgs e)
        {
            var cv = FindCanvasView();
            if (cv == null) return;
            var dialog = new AdjustmentsDialog(AdjustmentType.HueSaturation, cv);
            await dialog.ShowDialog(this);
        }

        public async void OnLevelsClicked(object? sender, RoutedEventArgs e)
        {
            var cv = FindCanvasView();
            if (cv == null) return;
            var dialog = new AdjustmentsDialog(AdjustmentType.Levels, cv);
            await dialog.ShowDialog(this);
        }

        public async void OnCurvesClicked(object? sender, RoutedEventArgs e)
        {
            var cv = FindCanvasView();
            if (cv == null) return;
            var dialog = new AdjustmentsDialog(AdjustmentType.Curves, cv);
            await dialog.ShowDialog(this);
        }

        public async void OnColorBalanceClicked(object? sender, RoutedEventArgs e)
        {
            var cv = FindCanvasView();
            if (cv == null) return;
            var dialog = new AdjustmentsDialog(AdjustmentType.ColorBalance, cv);
            await dialog.ShowDialog(this);
        }

        // Rotate / Flip
        public void OnRotate90CWClicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.RotateCanvas(90);
            UpdateCanvasSizeStatus();
            RefreshLayersPanel();
            UpdateStatus("Rotated 90° CW");
        }

        public void OnRotate90CCWClicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.RotateCanvas(-90);
            UpdateCanvasSizeStatus();
            RefreshLayersPanel();
            UpdateStatus("Rotated 90° CCW");
        }

        public void OnRotate180Clicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.RotateCanvas(180);
            UpdateCanvasSizeStatus();
            RefreshLayersPanel();
            UpdateStatus("Rotated 180°");
        }

        public void OnFlipHClicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.FlipCanvas(horizontal: true);
            RefreshLayersPanel();
            UpdateStatus("Flipped Horizontal");
        }

        public void OnFlipVClicked(object? sender, RoutedEventArgs e)
        {
            FindCanvasView()?.FlipCanvas(horizontal: false);
            RefreshLayersPanel();
            UpdateStatus("Flipped Vertical");
        }
    }
}
