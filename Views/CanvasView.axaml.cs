using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Pixellum.Core;
using Pixellum.Rendering;

namespace Pixellum.Views
{
    // Q3 / M3: Enum replaces fragile magic strings for tool switching
    public enum ToolType { Brush, Eraser, Fill, Eyedropper }

    public partial class CanvasView : UserControl
    {
        // ── Document / layers ────────────────────────────────────────────────
        private Document _document;
        private readonly List<Layer> _layers = new();
        private int _activeLayerIndex = 0;

        // B1: Null-safe ActiveLayer — never throws on empty list
        private Layer? ActiveLayer =>
            (_layers.Count > 0 && _activeLayerIndex < _layers.Count)
                ? _layers[_activeLayerIndex]
                : null;

        // ── Rendering ────────────────────────────────────────────────────────
        private readonly BrushEngine _brushEngine = new();
        private readonly Renderer    _renderer    = new();
        private WriteableBitmap _canvasBitmap;
        private Image?          _canvasImage;

        // ── Drawing state ────────────────────────────────────────────────────
        private Point? _lastPoint = null;
        private bool   _isDrawing = false;

        // ── Undo / redo (raw snapshot stacks — Phase 2 will wire HistoryManager) ─
        private readonly Stack<uint[]> _undoStack = new();
        private readonly Stack<uint[]> _redoStack = new();
        private const int MAX_HISTORY = 30;

        // ── Tool / brush state ──────────────────────────────────────────────
        // Q3 / M3: Strongly-typed tool; no more magic strings
        public ToolType ActiveTool { get; set; } = ToolType.Brush;

        public event EventHandler<uint>? ColorPicked;

        private uint  _activeColor  = 0xFFFF0000;
        private float _brushRadius  = 15f;
        private float _brushOpacity = 1f;

        public uint  ActiveColor  { get => _activeColor;  set => _activeColor  = value; }
        public float BrushRadius  { get => _brushRadius;  set => _brushRadius  = value; }
        public float BrushOpacity { get => _brushOpacity; set => _brushOpacity = value; }
        public WriteableBitmap CanvasBitmap => _canvasBitmap;

        // ── Zoom / pan ────────────────────────────────────────────────────────
        private double           _zoom           = 1.0;
        private Point            _panTranslation = new(0, 0);
        private bool             _isPanning      = false;
        private bool             _spaceHeld      = false;
        private Point            _lastPanPoint;
        private ScaleTransform?  _scaleTransform;
        private TranslateTransform? _translateTransform;

        // ── Constructor ──────────────────────────────────────────────────────
        public CanvasView()
        {
            InitializeComponent();

            const int W = 800, H = 600;
            _document = new Document(W, H);
            _layers.Add(new Layer(W, H, "Layer 1"));

            // BE1: Use AlphaFormat.Unpremul to match straight-alpha blend math in BrushEngine
            _canvasBitmap = new WriteableBitmap(
                new PixelSize(W, H),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);

            _canvasImage = this.FindControl<Image>("CanvasImage");
            if (_canvasImage != null)
                _canvasImage.Source = _canvasBitmap;

            if (_canvasImage?.RenderTransform is TransformGroup tg)
            {
                _scaleTransform     = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                _translateTransform = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
            }

            PointerPressed      += CanvasView_PointerPressed;
            PointerMoved        += CanvasView_PointerMoved;
            PointerReleased     += CanvasView_PointerReleased;
            PointerWheelChanged += CanvasView_PointerWheelChanged;

            KeyDown += (_, e) => { if (e.Key == Key.Space) _spaceHeld = true; };
            KeyUp   += (_, e) => { if (e.Key == Key.Space) _spaceHeld = false; };
            Focusable = true;

            RedrawCanvas();
        }

        // ── Layer management ─────────────────────────────────────────────────

        public void AddLayer(string name)
        {
            var newLayer = new Layer(_document.Width, _document.Height, name);
            _layers.Add(newLayer);
            _activeLayerIndex = _layers.Count - 1;
            RedrawCanvas();
            System.Diagnostics.Debug.WriteLine($"✅ Added new layer: {name}");
        }

        public void DeleteLayer(int index)
        {
            if (_layers.Count <= 1)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Cannot delete the last layer");
                return;
            }

            if (index >= 0 && index < _layers.Count)
            {
                string deletedName = _layers[index].Name;
                _layers.RemoveAt(index);

                if (_activeLayerIndex >= _layers.Count)
                    _activeLayerIndex = _layers.Count - 1;
                else if (_activeLayerIndex > index)
                    _activeLayerIndex--;

                RedrawCanvas();
                System.Diagnostics.Debug.WriteLine($"✅ Deleted layer: {deletedName}");
            }
        }

        public void RenameLayer(int index, string newName)
        {
            if (index >= 0 && index < _layers.Count && !string.IsNullOrWhiteSpace(newName))
            {
                _layers[index].Name = newName;
                System.Diagnostics.Debug.WriteLine($"✅ Renamed layer to: {newName}");
            }
        }

        public List<Layer>  GetLayers()          => _layers;
        public int           GetActiveLayerIndex() => _activeLayerIndex;

        public void SetActiveLayer(int index)
        {
            if (index >= 0 && index < _layers.Count)
            {
                _activeLayerIndex = index;
                System.Diagnostics.Debug.WriteLine($"✅ Active layer: {_layers[index].Name}");
            }
        }

        // ── Coordinate helpers ───────────────────────────────────────────────

        private Point? GetBitmapCoordinates(PointerEventArgs e)
        {
            if (_canvasImage?.Bounds == null) return null;

            var pos         = e.GetPosition(_canvasImage);
            var bitmapWidth = _canvasBitmap.PixelSize.Width;
            var bitmapHeight = _canvasBitmap.PixelSize.Height;
            var imageBounds = _canvasImage.Bounds;

            double offsetX = (imageBounds.Width  - bitmapWidth)  / 2.0;
            double offsetY = (imageBounds.Height - bitmapHeight) / 2.0;

            double bitmapX = pos.X - offsetX;
            double bitmapY = pos.Y - offsetY;

            if (bitmapX < 0 || bitmapY < 0 ||
                bitmapX >= bitmapWidth || bitmapY >= bitmapHeight)
                return null;

            return new Point(bitmapX, bitmapY);
        }

        // ── Input handlers ───────────────────────────────────────────────────

        private void CanvasView_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_canvasImage == null) return;

            var p = e.GetCurrentPoint(this);

            if (p.Properties.IsMiddleButtonPressed ||
               (p.Properties.IsLeftButtonPressed && _spaceHeld))
            {
                _isPanning    = true;
                _lastPanPoint = p.Position;
                e.Handled     = true;
                return;
            }

            if (!p.Properties.IsLeftButtonPressed) return;

            var coords = GetBitmapCoordinates(e);
            if (coords == null) return;

            // Q3: Switch on enum, not magic strings
            if (ActiveTool == ToolType.Eyedropper)
            {
                int x = (int)coords.Value.X;
                int y = (int)coords.Value.Y;
                if (x >= 0 && x < _document.Width && y >= 0 && y < _document.Height)
                {
                    uint color = _document.GetPixelsRaw()[y * _document.Width + x];
                    ColorPicked?.Invoke(this, color);
                }
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Fill)
            {
                SaveStateForUndo();
                FloodFill((int)coords.Value.X, (int)coords.Value.Y);
                RedrawCanvas();
                e.Handled = true;
                return;
            }

            _isDrawing = true;
            _lastPoint = coords.Value;
            SaveStateForUndo();
            DrawAtPoint(coords.Value.X, coords.Value.Y);
            e.Handled = true;
        }

        private void CanvasView_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_canvasImage == null) return;

            var p = e.GetCurrentPoint(this);

            if (_isPanning)
            {
                var deltaX = p.Position.X - _lastPanPoint.X;
                var deltaY = p.Position.Y - _lastPanPoint.Y;

                _panTranslation = new Point(_panTranslation.X + deltaX, _panTranslation.Y + deltaY);
                if (_translateTransform != null)
                {
                    _translateTransform.X = _panTranslation.X;
                    _translateTransform.Y = _panTranslation.Y;
                }

                _lastPanPoint = p.Position;
                e.Handled     = true;
                return;
            }

            if (!_isDrawing) return;

            var coords = GetBitmapCoordinates(e);
            if (coords == null) return;

            if (ActiveTool == ToolType.Brush || ActiveTool == ToolType.Eraser)
            {
                if (_lastPoint.HasValue)
                {
                    double dist  = Math.Sqrt(
                        Math.Pow(coords.Value.X - _lastPoint.Value.X, 2) +
                        Math.Pow(coords.Value.Y - _lastPoint.Value.Y, 2));
                    double steps = Math.Max(1, dist / (_brushRadius * 0.25));

                    // BE4: Start from i=0 so the segment start point is always stamped
                    for (int i = 0; i <= steps; i++)
                    {
                        double t = steps == 0 ? 1.0 : i / steps;
                        double x = _lastPoint.Value.X + (coords.Value.X - _lastPoint.Value.X) * t;
                        double y = _lastPoint.Value.Y + (coords.Value.Y - _lastPoint.Value.Y) * t;
                        DrawAtPoint(x, y);
                    }
                }
                else
                {
                    DrawAtPoint(coords.Value.X, coords.Value.Y);
                }
            }

            _lastPoint = coords.Value;
            e.Handled  = true;
        }

        private void CanvasView_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                e.Handled  = true;
                return;
            }

            if (!_isDrawing) return;
            _isDrawing = false;
            _lastPoint = null;
            e.Handled  = true;
        }

        private void CanvasView_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_scaleTransform == null || _translateTransform == null) return;

            double zoomDelta = e.Delta.Y > 0 ? 1.1 : 0.9;
            double newZoom   = Math.Clamp(_zoom * zoomDelta, 0.1, 10.0);
            if (newZoom == _zoom) return;

            var p    = e.GetPosition(this);
            var relX = p.X - _translateTransform.X;
            var relY = p.Y - _translateTransform.Y;
            var ratio = newZoom / _zoom;

            _translateTransform.X = p.X - (relX * ratio);
            _translateTransform.Y = p.Y - (relY * ratio);
            _panTranslation = new Point(_translateTransform.X, _translateTransform.Y);

            _zoom                = newZoom;
            _scaleTransform.ScaleX = _zoom;
            _scaleTransform.ScaleY = _zoom;

            e.Handled = true;
        }

        // ── Drawing ──────────────────────────────────────────────────────────

        private void DrawAtPoint(double x, double y)
        {
            if (ActiveLayer == null) return;   // B1 guard

            int px = (int)Math.Clamp(x, 0, _document.Width  - 1);
            int py = (int)Math.Clamp(y, 0, _document.Height - 1);

            if (ActiveTool == ToolType.Brush)
            {
                uint newAlpha    = (uint)(255 * _brushOpacity);
                uint dynamicColor = (newAlpha << 24) | (_activeColor & 0x00FFFFFF);
                _brushEngine.ApplyBrush(ActiveLayer, px, py, dynamicColor, _brushRadius);
            }
            else if (ActiveTool == ToolType.Eraser)
            {
                _brushEngine.ApplyEraser(ActiveLayer, px, py, _brushRadius);
            }

            RedrawCanvas();
        }

        // B3: FloodFill uses value-tuple queue — no heavyweight Avalonia.Point allocations
        private void FloodFill(int startX, int startY)
        {
            if (ActiveLayer == null) return;   // B1 guard

            int    w       = ActiveLayer.Width;
            int    h       = ActiveLayer.Height;
            if (startX < 0 || startX >= w || startY < 0 || startY >= h) return;

            uint[] pixels  = ActiveLayer.GetPixels();
            uint   targetColor = pixels[startY * w + startX];

            uint fillAlpha        = (uint)(255 * _brushOpacity);
            uint replacementColor = (fillAlpha << 24) | (_activeColor & 0x00FFFFFF);

            if (targetColor == replacementColor) return;

            var q = new Queue<(int x, int y)>();
            q.Enqueue((startX, startY));

            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();

                int index = y * w + x;
                if (pixels[index] != targetColor) continue;

                // Scan-line fill
                int left = x;
                while (left > 0     && pixels[y * w + (left - 1)] == targetColor) left--;

                int right = x;
                while (right < w - 1 && pixels[y * w + (right + 1)] == targetColor) right++;

                for (int i = left; i <= right; i++)
                {
                    pixels[y * w + i] = replacementColor;

                    if (y > 0     && pixels[(y - 1) * w + i] == targetColor) q.Enqueue((i, y - 1));
                    if (y < h - 1 && pixels[(y + 1) * w + i] == targetColor) q.Enqueue((i, y + 1));
                }
            }
        }

        // ── Rendering ────────────────────────────────────────────────────────

        private void RedrawCanvas()
        {
            // Compositing and bitmap blit can happen on any thread
            LayerCompositor.Composite(_document, _layers);
            _renderer.Render(_document, _canvasBitmap);

            // T1: InvalidateVisual MUST run on the UI thread
            Dispatcher.UIThread.Post(() => _canvasImage?.InvalidateVisual(),
                DispatcherPriority.Render);
        }

        // ── Undo / Redo ──────────────────────────────────────────────────────

        private void SaveStateForUndo()
        {
            if (ActiveLayer == null) return;   // B1 guard
            try
            {
                uint[] snapshot = new uint[ActiveLayer.Width * ActiveLayer.Height];
                Array.Copy(ActiveLayer.GetPixels(), snapshot, snapshot.Length);
                _undoStack.Push(snapshot);
                _redoStack.Clear();

                // B2: Trim oldest entries — preserve correct LIFO order
                if (_undoStack.Count > MAX_HISTORY)
                {
                    var items = _undoStack.ToArray(); // index 0 = most-recent (top of stack)
                    _undoStack.Clear();
                    // Re-push from oldest (end) to newest (start) so newest is on top
                    for (int i = MAX_HISTORY - 1; i >= 0; i--)
                        _undoStack.Push(items[i]);
                }

                System.Diagnostics.Debug.WriteLine($"✅ Saved undo state. Stack: {_undoStack.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed to save undo state: {ex.Message}");
            }
        }

        public void Undo()
        {
            if (_undoStack.Count == 0 || ActiveLayer == null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Nothing to undo");
                return;
            }

            try
            {
                uint[] currentState = new uint[ActiveLayer.Width * ActiveLayer.Height];
                Array.Copy(ActiveLayer.GetPixels(), currentState, currentState.Length);
                _redoStack.Push(currentState);

                uint[] previousState = _undoStack.Pop();
                Array.Copy(previousState, ActiveLayer.GetPixels(), previousState.Length);

                RedrawCanvas();
                System.Diagnostics.Debug.WriteLine(
                    $"✅ Undo. Undo:{_undoStack.Count} Redo:{_redoStack.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Undo failed: {ex.Message}");
            }
        }

        public void Redo()
        {
            if (_redoStack.Count == 0 || ActiveLayer == null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Nothing to redo");
                return;
            }

            try
            {
                uint[] currentState = new uint[ActiveLayer.Width * ActiveLayer.Height];
                Array.Copy(ActiveLayer.GetPixels(), currentState, currentState.Length);
                _undoStack.Push(currentState);

                uint[] redoState = _redoStack.Pop();
                Array.Copy(redoState, ActiveLayer.GetPixels(), redoState.Length);

                RedrawCanvas();
                System.Diagnostics.Debug.WriteLine(
                    $"✅ Redo. Undo:{_undoStack.Count} Redo:{_redoStack.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Redo failed: {ex.Message}");
            }
        }
    }
}