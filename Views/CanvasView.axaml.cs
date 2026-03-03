using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia;
using Pixellum.Core;
using Pixellum.Rendering;
using Avalonia.Platform;

namespace Pixellum.Views
{
    public partial class CanvasView : UserControl
    {
        private Document _document;
        private readonly List<Layer> _layers = new();
        private int _activeLayerIndex = 0;
        private Layer _activeLayer => _layers[_activeLayerIndex];

        private readonly BrushEngine _brushEngine = new();
        private readonly Renderer _renderer = new();
        private WriteableBitmap _canvasBitmap;
        private Image? _canvasImage;
        private Point? _lastPoint = null;

        private readonly Stack<uint[]> _undoStack = new Stack<uint[]>();
        private readonly Stack<uint[]> _redoStack = new Stack<uint[]>();
        private const int MAX_HISTORY = 30;

        public string ActiveTool { get; set; } = "Brush";
        public event EventHandler<uint>? ColorPicked;

        public bool ShowBrushPreview { get; set; }
        public float PreviewBrushRadius { get; set; }

        private uint _activeColor = 0xFFFF0000;
        private float _brushRadius = 15f;
        private float _brushOpacity = 1f;

        public uint ActiveColor { get => _activeColor; set => _activeColor = value; }
        public float BrushRadius { get => _brushRadius; set => _brushRadius = value; }
        public float BrushOpacity { get => _brushOpacity; set => _brushOpacity = value; }
        public WriteableBitmap CanvasBitmap => _canvasBitmap;

        private double _zoom = 1.0;
        private Point _panTranslation = new Point(0, 0);
        private bool _isPanning = false;
        private Point _lastPanPoint;
        private ScaleTransform? _scaleTransform;
        private TranslateTransform? _translateTransform;

        public CanvasView()
        {
            InitializeComponent();

            const int W = 800, H = 600;
            _document = new Document(W, H);
            _layers.Add(new Layer(W, H, "Layer 1"));

            _canvasBitmap = new WriteableBitmap(
                new PixelSize(W, H),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            _canvasImage = this.FindControl<Image>("CanvasImage");
            if (_canvasImage != null)
            {
                _canvasImage.Source = _canvasBitmap;
            }

            _scaleTransform = this.FindControl<ScaleTransform>("ScaleTransform");
            _translateTransform = this.FindControl<TranslateTransform>("TranslateTransform");

            PointerPressed += CanvasView_PointerPressed;
            PointerMoved += CanvasView_PointerMoved;
            PointerReleased += CanvasView_PointerReleased;
            PointerWheelChanged += CanvasView_PointerWheelChanged;

            RedrawCanvas();
        }

        // ✅ NEW LAYER MANAGEMENT METHODS
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
                {
                    _activeLayerIndex = _layers.Count - 1;
                }
                else if (_activeLayerIndex > index)
                {
                    _activeLayerIndex--;
                }

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

        public List<Layer> GetLayers() => _layers;

        public int GetActiveLayerIndex() => _activeLayerIndex;

        public void SetActiveLayer(int index)
        {
            if (index >= 0 && index < _layers.Count)
            {
                _activeLayerIndex = index;
                System.Diagnostics.Debug.WriteLine($"✅ Active layer changed to: {_layers[index].Name}");
            }
        }

        private Point? GetBitmapCoordinates(PointerEventArgs e)
        {
            if (_canvasImage?.Bounds == null || _canvasBitmap == null)
                return null;

            var pos = e.GetPosition(_canvasImage);
            var imageBounds = _canvasImage.Bounds;
            var bitmapWidth = _canvasBitmap.PixelSize.Width;
            var bitmapHeight = _canvasBitmap.PixelSize.Height;

            double offsetX = (imageBounds.Width - bitmapWidth) / 2.0;
            double offsetY = (imageBounds.Height - bitmapHeight) / 2.0;

            double bitmapX = pos.X - offsetX;
            double bitmapY = pos.Y - offsetY;

            if (bitmapX < 0 || bitmapY < 0 ||
                bitmapX >= bitmapWidth || bitmapY >= bitmapHeight)
            {
                return null;
            }

            return new Point(bitmapX, bitmapY);
        }

        private void CanvasView_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_canvasImage == null) return;

            var p = e.GetCurrentPoint(this);

            if (p.Properties.IsMiddleButtonPressed || (p.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Space)))
            {
                _isPanning = true;
                _lastPanPoint = p.Position;
                e.Handled = true;
                return;
            }

            if (!p.Properties.IsLeftButtonPressed) return;

            var coords = GetBitmapCoordinates(e);
            if (coords == null) return;

            if (ActiveTool == "Eyedropper")
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

            if (ActiveTool == "Fill")
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
                e.Handled = true;
                return;
            }

            if (!_isDrawing) return;

            var coords = GetBitmapCoordinates(e);
            if (coords == null) return;

            if (ActiveTool == "Brush" || ActiveTool == "Eraser")
            {
                if (_lastPoint.HasValue)
                {
                    // Interpolate
                    double dist = Math.Sqrt(Math.Pow(coords.Value.X - _lastPoint.Value.X, 2) + Math.Pow(coords.Value.Y - _lastPoint.Value.Y, 2));
                    double steps = Math.Max(1, dist / (_brushRadius * 0.25)); // Stamp every 1/4th brush radius

                    for (int i = 1; i <= steps; i++)
                    {
                        double t = i / steps;
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
            e.Handled = true;
        }

        private void CanvasView_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                e.Handled = true;
                return;
            }

            if (!_isDrawing) return;

            _isDrawing = false;
            _lastPoint = null;
            e.Handled = true;
        }

        private void CanvasView_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_scaleTransform == null || _translateTransform == null) return;

            double zoomDelta = e.Delta.Y > 0 ? 1.1 : 0.9;
            double newZoom = Math.Clamp(_zoom * zoomDelta, 0.1, 10.0);
            
            if (newZoom == _zoom) return;

            var p = e.GetPosition(this);

            var relX = p.X - _translateTransform.X;
            var relY = p.Y - _translateTransform.Y;

            var ratio = newZoom / _zoom;

            _translateTransform.X = p.X - (relX * ratio);
            _translateTransform.Y = p.Y - (relY * ratio);
            _panTranslation = new Point(_translateTransform.X, _translateTransform.Y);

            _zoom = newZoom;
            _scaleTransform.ScaleX = _zoom;
            _scaleTransform.ScaleY = _zoom;

            e.Handled = true;
        }

        private void DrawAtPoint(double x, double y)
        {
            int px = (int)Math.Clamp(x, 0, _document.Width - 1);
            int py = (int)Math.Clamp(y, 0, _document.Height - 1);

            if (ActiveTool == "Brush")
            {
                uint newAlpha = (uint)(255 * _brushOpacity);
                uint dynamicColor = (newAlpha << 24) | (_activeColor & 0x00FFFFFF);
                _brushEngine.ApplyBrush(_activeLayer, px, py, dynamicColor, _brushRadius);
            }
            else if (ActiveTool == "Eraser")
            {
                _brushEngine.ApplyEraser(_activeLayer, px, py, _brushRadius);
            }

            RedrawCanvas();
        }

        private void FloodFill(int startX, int startY)
        {
            int w = _activeLayer.Width;
            int h = _activeLayer.Height;
            if (startX < 0 || startX >= w || startY < 0 || startY >= h) return;

            uint[] pixels = _activeLayer.GetPixels();
            uint targetColor = pixels[startY * w + startX];

            // Don't fill if clicked the same color (approx - ignore alpha opacity nuance for pure color matching here)
            // But we actually fill with _activeColor and 100% opacity for now
            uint fillAlpha = (uint)(255 * _brushOpacity);
            uint replacementColor = (fillAlpha << 24) | (_activeColor & 0x00FFFFFF);

            if (targetColor == replacementColor) return;

            Queue<Point> q = new Queue<Point>();
            q.Enqueue(new Point(startX, startY));

            while (q.Count > 0)
            {
                Point p = q.Dequeue();
                int x = (int)p.X;
                int y = (int)p.Y;

                int index = y * w + x;
                if (pixels[index] != targetColor) continue;

                // Find left boundary
                int left = x;
                while (left > 0 && pixels[y * w + (left - 1)] == targetColor)
                    left--;

                // Find right boundary
                int right = x;
                while (right < w - 1 && pixels[y * w + (right + 1)] == targetColor)
                    right++;

                for (int i = left; i <= right; i++)
                {
                    pixels[y * w + i] = replacementColor;

                    if (y > 0 && pixels[(y - 1) * w + i] == targetColor)
                        q.Enqueue(new Point(i, y - 1));

                    if (y < h - 1 && pixels[(y + 1) * w + i] == targetColor)
                        q.Enqueue(new Point(i, y + 1));
                }
            }
        }

        private void RedrawCanvas()
        {
            LayerCompositor.Composite(_document, _layers);
            _renderer.Render(_document, _canvasBitmap);

            if (_canvasImage != null)
            {
                _canvasImage.InvalidateVisual();
            }
        }

        private void SaveStateForUndo()
        {
            try
            {
                uint[] snapshot = new uint[_activeLayer.Width * _activeLayer.Height];
                Array.Copy(_activeLayer.GetPixels(), snapshot, snapshot.Length);

                _undoStack.Push(snapshot);
                _redoStack.Clear();

                if (_undoStack.Count > MAX_HISTORY)
                {
                    var tempList = new List<uint[]>(_undoStack);
                    _undoStack.Clear();
                    for (int i = 0; i < MAX_HISTORY; i++)
                    {
                        _undoStack.Push(tempList[i]);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ Saved undo state. Stack size: {_undoStack.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Failed to save undo state: {ex.Message}");
            }
        }

        public void Undo()
        {
            if (_undoStack.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Nothing to undo");
                return;
            }

            try
            {
                uint[] currentState = new uint[_activeLayer.Width * _activeLayer.Height];
                Array.Copy(_activeLayer.GetPixels(), currentState, currentState.Length);
                _redoStack.Push(currentState);

                uint[] previousState = _undoStack.Pop();
                Array.Copy(previousState, _activeLayer.GetPixels(), previousState.Length);

                RedrawCanvas();

                System.Diagnostics.Debug.WriteLine($"✅ Undo successful. Undo stack: {_undoStack.Count}, Redo stack: {_redoStack.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Undo failed: {ex.Message}");
            }
        }

        public void Redo()
        {
            if (_redoStack.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Nothing to redo");
                return;
            }

            try
            {
                uint[] currentState = new uint[_activeLayer.Width * _activeLayer.Height];
                Array.Copy(_activeLayer.GetPixels(), currentState, currentState.Length);
                _undoStack.Push(currentState);

                uint[] redoState = _redoStack.Pop();
                Array.Copy(redoState, _activeLayer.GetPixels(), redoState.Length);

                RedrawCanvas();

                System.Diagnostics.Debug.WriteLine($"✅ Redo successful. Undo stack: {_undoStack.Count}, Redo stack: {_redoStack.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Redo failed: {ex.Message}");
            }
        }
    }
}