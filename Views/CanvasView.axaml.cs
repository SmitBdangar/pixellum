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
        private Canvas?         _overlayCanvas;
        private Avalonia.Controls.Shapes.Rectangle? _selectionMarquee;
        private Avalonia.Controls.Shapes.Rectangle? _selectionMarqueeInner;
        private TextBox?        _textOverlayBox;
        private Avalonia.Controls.Shapes.Rectangle? _gridOverlayRect;

        // ── Drawing state ────────────────────────────────────────────────────
        private Point? _lastPoint = null;
        private bool   _isDrawing = false;

        // ── Undo / redo ─────────────────────────────────────────────────
        public HistoryManager History { get; } = new HistoryManager(50);

        // ── Selection & Shapes ───────────────────────────────────────────
        private bool    _hasSelection   = false;
        private int     _selStartX, _selStartY;
        private int     _selEndX,   _selEndY;
        private bool    _selDragging    = false;
        
        private int     _shapeStartX, _shapeStartY;
        private bool    _shapeDragging = false;
        
        private int     _gradStartX, _gradStartY;
        private bool    _gradDragging = false;

        // ── Grid overlay ────────────────────────────────────────────────
        private bool _gridVisible   = false;
        private int  _gridCellSize  = 32;  // pixels
        public void SetGridVisible(bool visible)
        {
            _gridVisible = visible;
            UpdateGridOverlay();
        }

        private void UpdateGridOverlay()
        {
            if (_gridOverlayRect != null && _canvasBitmap != null)
            {
                _gridOverlayRect.Width     = _canvasBitmap.PixelSize.Width;
                _gridOverlayRect.Height    = _canvasBitmap.PixelSize.Height;
                _gridOverlayRect.IsVisible = _gridVisible;

                if (_gridVisible && _gridOverlayRect.Fill == null)
                    _gridOverlayRect.Fill = CreateGridBrush(_gridCellSize);
            }
        }

        private static DrawingBrush CreateGridBrush(int size)
        {
            var lineGeometry = new GeometryGroup();
            lineGeometry.Children.Add(new LineGeometry(new Point(0, 0), new Point(size, 0)));
            lineGeometry.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, size)));

            var pen = new Pen(new SolidColorBrush(Color.Parse("#44ffffff")), 1);

            return new DrawingBrush
            {
                Drawing         = new GeometryDrawing { Pen = pen, Geometry = lineGeometry },
                TileMode        = TileMode.Tile,
                SourceRect      = new RelativeRect(0, 0, size, size, RelativeUnit.Absolute),
                DestinationRect = new RelativeRect(0, 0, size, size, RelativeUnit.Absolute)
            };
        }

        // ── Tool / brush state ──────────────────────────────────────────────
        // Q3 / M3: Strongly-typed tool; no more magic strings
        private ToolType _activeTool = ToolType.Brush;
        public ToolType ActiveTool
        {
            get => _activeTool;
            set
            {
                if (_activeTool != value)
                {
                    _activeTool = value;
                    ToolChanged?.Invoke(this, _activeTool);
                }
            }
        }

        public event EventHandler<uint>? ColorPicked;

        private uint  _activeColor  = 0xFFFF0000;
        private uint  _secondaryColor = 0xFFFFFFFF;
        private float _brushRadius  = 15f;
        private float _brushOpacity = 1f;

        public uint  ActiveColor  { get => _activeColor;  set => _activeColor  = value; }
        public float BrushRadius  { get => _brushRadius;  set => _brushRadius  = value; }
        public float BrushOpacity { get => _brushOpacity; set => _brushOpacity = value; }

        public void SetBrushEngineParams(float hardness, float flow)
        {
            _brushEngine.Hardness = hardness;
            _brushEngine.Flow     = flow;
        }
        public WriteableBitmap CanvasBitmap => _canvasBitmap;
        public double Zoom => _zoom;
        public int CanvasWidth  => _document.Width;
        public int CanvasHeight => _document.Height;

        // Events for status bar updates
        public event EventHandler<double>?  ZoomChanged;
        public event EventHandler<ToolType>? ToolChanged;

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

            _overlayCanvas = this.FindControl<Canvas>("OverlayCanvas");
            _selectionMarquee = this.FindControl<Avalonia.Controls.Shapes.Rectangle>("SelectionMarquee");
            _selectionMarqueeInner = this.FindControl<Avalonia.Controls.Shapes.Rectangle>("SelectionMarqueeInner");
            _textOverlayBox = this.FindControl<TextBox>("TextOverlayBox");
            _gridOverlayRect = this.FindControl<Avalonia.Controls.Shapes.Rectangle>("GridOverlayRect");

            var container = this.FindControl<Grid>("TransformContainer");
            if (container?.RenderTransform is TransformGroup tg)
            {
                _scaleTransform     = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                _translateTransform = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
            }

            PointerPressed      += CanvasView_PointerPressed;
            PointerMoved        += CanvasView_PointerMoved;
            PointerReleased     += CanvasView_PointerReleased;
            PointerWheelChanged += CanvasView_PointerWheelChanged;

            KeyDown += CanvasView_KeyDown;
            KeyUp   += (_, e) => { if (e.Key == Key.Space) _spaceHeld = false; };
            Focusable = true;

            RedrawCanvas();
        }

        // ── New document / Load image ────────────────────────────────────────

        /// <summary>
        /// Reinitializes the canvas with a fresh document of the given size.
        /// bgChoice: 0=transparent, 1=white, 2=black
        /// </summary>
        public void NewDocument(int width, int height, int bgChoice)
        {
            _layers.Clear();
            History.Clear();

            _document = new Document(width, height);
            var bg = new Layer(width, height, "Background");

            if (bgChoice == 1)  // White
            {
                var px = bg.GetPixels();
                for (int i = 0; i < px.Length; i++) px[i] = 0xFFFFFFFF;
            }
            else if (bgChoice == 2)  // Black
            {
                var px = bg.GetPixels();
                for (int i = 0; i < px.Length; i++) px[i] = 0xFF000000;
            }

            _layers.Add(bg);
            _activeLayerIndex = 0;

            _canvasBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);

            if (_canvasImage != null)
                _canvasImage.Source = _canvasBitmap;

            // Reset zoom/pan
            _zoom = 1.0;
            if (_scaleTransform != null)
                _scaleTransform.ScaleX = _scaleTransform.ScaleY = 1.0;
            if (_translateTransform != null)
                _translateTransform.X = _translateTransform.Y = 0;
            _panTranslation = new Point(0, 0);

            ZoomChanged?.Invoke(this, _zoom);
            RedrawCanvas();
        }

        /// <summary>
        /// Pastes decoded image pixels onto a new layer (or replaces the canvas if resizeCanvas is true).
        /// </summary>
        public void LoadImageOntoLayer(uint[] pixels, int imgWidth, int imgHeight, bool resizeCanvas)
        {
            if (resizeCanvas)
            {
                _layers.Clear();
                History.Clear();

                _document = new Document(imgWidth, imgHeight);

                _canvasBitmap = new WriteableBitmap(
                    new PixelSize(imgWidth, imgHeight),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Unpremul);

                if (_canvasImage != null)
                    _canvasImage.Source = _canvasBitmap;

                _zoom = 1.0;
                if (_scaleTransform != null)
                    _scaleTransform.ScaleX = _scaleTransform.ScaleY = 1.0;
                if (_translateTransform != null)
                    _translateTransform.X = _translateTransform.Y = 0;
                _panTranslation = new Point(0, 0);
                ZoomChanged?.Invoke(this, _zoom);
            }

            var newLayer = new Layer(_document.Width, _document.Height, "Imported Image");
            var dst = newLayer.GetPixels();
            int copyW = Math.Min(imgWidth, _document.Width);
            int copyH = Math.Min(imgHeight, _document.Height);
            for (int y = 0; y < copyH; y++)
                for (int x = 0; x < copyW; x++)
                    dst[y * _document.Width + x] = pixels[y * imgWidth + x];

            _layers.Add(newLayer);
            _activeLayerIndex = _layers.Count - 1;
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

        public void AddSolidColorLayer(string name, uint color)
        {
            var newLayer = new Layer(_document.Width, _document.Height, name);
            var px = newLayer.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = color;
            }
            newLayer.LockPixels = true; // Lock pixels for solid color fills by default
            _layers.Add(newLayer);
            _activeLayerIndex = _layers.Count - 1;
            RedrawCanvas();
            System.Diagnostics.Debug.WriteLine($"✅ Added solid color layer: {name}");
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

        /// <summary>Public entry point for LayersPanel (opacity/blend mode changes).</summary>
        public void TriggerRedraw()
        {
            // Mark all layers and document as fully dirty so the next composite pass sees the changes.
            var fullRect = new IntRect(0, 0, _document.Width, _document.Height);
            foreach (var layer in _layers)
                layer.MarkDirty(fullRect);
            _document.MarkDirty(fullRect);
            RedrawCanvas();
        }

        /// <summary>Exposes undo snapshot to external callers (e.g. Adjustments dialog).</summary>
        public void SaveUndoState() => SaveStateForUndo();

        // ── Canvas operations ─────────────────────────────────────────────────

        /// <summary>Resize the canvas (extend or clip); anchor is offset from top-left.</summary>
        public void ResizeCanvas(int newW, int newH, int anchorX = 0, int anchorY = 0)
        {
            SaveStateForUndo();

            // Rebuild layers with new size — copies pixel data with anchor offset
            var newLayers = new Layer[_layers.Count];
            for (int i = 0; i < _layers.Count; i++)
            {
                var oldLayer = _layers[i];
                var newLayer = new Layer(newW, newH, oldLayer.Name)
                {
                    Visible          = oldLayer.Visible,
                    Opacity          = oldLayer.Opacity,
                    Mode             = oldLayer.Mode,
                    LockTransparency = oldLayer.LockTransparency,
                    LockPixels       = oldLayer.LockPixels,
                    LockPosition     = oldLayer.LockPosition,
                    IsClippingMask   = oldLayer.IsClippingMask
                };
                var src = oldLayer.GetPixels();
                var dst = newLayer.GetPixels();
                int copyW = Math.Min(oldLayer.Width,  newW - anchorX);
                int copyH = Math.Min(oldLayer.Height, newH - anchorY);
                for (int y = 0; y < copyH; y++)
                {
                    int dstY = y + anchorY;
                    if (dstY < 0 || dstY >= newH) continue;
                    for (int x = 0; x < copyW; x++)
                    {
                        int dstX = x + anchorX;
                        if (dstX < 0 || dstX >= newW) continue;
                        dst[dstY * newW + dstX] = src[y * oldLayer.Width + x];
                    }
                }
                newLayers[i] = newLayer;
            }

            _layers.Clear();
            foreach (var l in newLayers) _layers.Add(l);
            _document = new Document(newW, newH);

            _canvasBitmap = BitmapFactory.Create(newW, newH);
            if (_canvasImage != null) _canvasImage.Source = _canvasBitmap;

            RedrawCanvas();
        }

        /// <summary>Bilinear resample all layers to a new size.</summary>
        public void ResampleImage(int newW, int newH)
        {
            SaveStateForUndo();

            var newLayers = new Layer[_layers.Count];
            for (int li = 0; li < _layers.Count; li++)
            {
                var oldLayer = _layers[li];
                var newLayer = new Layer(newW, newH, oldLayer.Name)
                {
                    Visible = oldLayer.Visible,
                    Opacity = oldLayer.Opacity,
                    Mode    = oldLayer.Mode
                };
                var src = oldLayer.GetPixels();
                var dst = newLayer.GetPixels();
                float sx = (float)oldLayer.Width  / newW;
                float sy = (float)oldLayer.Height / newH;

                for (int y = 0; y < newH; y++)
                {
                    for (int x = 0; x < newW; x++)
                    {
                        float fx = x * sx;
                        float fy = y * sy;
                        int   x0 = (int)fx, y0 = (int)fy;
                        int   x1 = Math.Min(x0 + 1, oldLayer.Width  - 1);
                        int   y1 = Math.Min(y0 + 1, oldLayer.Height - 1);
                        float dx = fx - x0, dy = fy - y0;

                        uint c00 = src[y0 * oldLayer.Width + x0];
                        uint c10 = src[y0 * oldLayer.Width + x1];
                        uint c01 = src[y1 * oldLayer.Width + x0];
                        uint c11 = src[y1 * oldLayer.Width + x1];

                        dst[y * newW + x] = BilinearBlend(c00, c10, c01, c11, dx, dy);
                    }
                }
                newLayers[li] = newLayer;
            }

            _layers.Clear();
            foreach (var l in newLayers) _layers.Add(l);
            _document = new Document(newW, newH);

            _canvasBitmap = new WriteableBitmap(
                new PixelSize(newW, newH),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);
            if (_canvasImage != null) _canvasImage.Source = _canvasBitmap;

            RedrawCanvas();
        }

        private static uint BilinearBlend(uint c00, uint c10, uint c01, uint c11, float dx, float dy)
        {
            uint A = BlendChannel((c00>>24)&0xFF,(c10>>24)&0xFF,(c01>>24)&0xFF,(c11>>24)&0xFF,dx,dy);
            uint R = BlendChannel((c00>>16)&0xFF,(c10>>16)&0xFF,(c01>>16)&0xFF,(c11>>16)&0xFF,dx,dy);
            uint G = BlendChannel((c00>> 8)&0xFF,(c10>> 8)&0xFF,(c01>> 8)&0xFF,(c11>> 8)&0xFF,dx,dy);
            uint B = BlendChannel( c00     &0xFF, c10     &0xFF, c01     &0xFF, c11     &0xFF,dx,dy);
            return (A<<24)|(R<<16)|(G<<8)|B;
        }

        private static uint BlendChannel(uint v00, uint v10, uint v01, uint v11, float dx, float dy)
        {
            float top    = v00 + (v10 - v00) * dx;
            float bottom = v01 + (v11 - v01) * dx;
            return (uint)Math.Clamp(top + (bottom - top) * dy + 0.5f, 0, 255);
        }

        /// <summary>Rotate all layers: degrees = 90, -90, or 180.</summary>
        public void RotateCanvas(int degrees)
        {
            SaveStateForUndo();
            int srcW = _document.Width, srcH = _document.Height;
            int newW, newH;

            if (degrees == 90 || degrees == -90)
                (newW, newH) = (srcH, srcW);
            else
                (newW, newH) = (srcW, srcH);

            var newLayers = new Layer[_layers.Count];
            for (int li = 0; li < _layers.Count; li++)
            {
                var old = _layers[li];
                var nw  = new Layer(newW, newH, old.Name)
                    { Visible = old.Visible, Opacity = old.Opacity, Mode = old.Mode };
                var src = old.GetPixels();
                var dst = nw.GetPixels();

                for (int y = 0; y < srcH; y++)
                for (int x = 0; x < srcW; x++)
                {
                    uint pixel = src[y * srcW + x];
                    int nx, ny;
                    if (degrees == 90)       { nx = srcH - 1 - y; ny = x; }
                    else if (degrees == -90) { nx = y;             ny = srcW - 1 - x; }
                    else                     { nx = srcW - 1 - x;  ny = srcH - 1 - y; }
                    dst[ny * newW + nx] = pixel;
                }
                newLayers[li] = nw;
            }

            _layers.Clear();
            foreach (var l in newLayers) _layers.Add(l);
            _document = new Document(newW, newH);

            _canvasBitmap = new WriteableBitmap(
                new PixelSize(newW, newH),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);
            if (_canvasImage != null) _canvasImage.Source = _canvasBitmap;
            RedrawCanvas();
        }

        /// <summary>Flip all layers horizontally or vertically.</summary>
        public void FlipCanvas(bool horizontal)
        {
            SaveStateForUndo();
            int w = _document.Width, h = _document.Height;

            foreach (var layer in _layers)
            {
                var px   = layer.GetPixels();
                var copy = (uint[])px.Clone();
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = horizontal ? w - 1 - x : x;
                    int sy = horizontal ? y         : h - 1 - y;
                    px[y * w + x] = copy[sy * w + sx];
                }
            }
            RedrawCanvas();
        }


        public void ZoomIn()    => ApplyZoomDelta(1.25, new Point(Bounds.Width / 2, Bounds.Height / 2));
        public void ZoomOut()   => ApplyZoomDelta(0.8,  new Point(Bounds.Width / 2, Bounds.Height / 2));
        public void ZoomReset()
        {
            if (_scaleTransform == null || _translateTransform == null) return;
            _zoom = 1.0;
            _scaleTransform.ScaleX = _scaleTransform.ScaleY = 1.0;
            _translateTransform.X  = _translateTransform.Y  = 0;
            _panTranslation = new Point(0, 0);
            ZoomChanged?.Invoke(this, _zoom);
        }

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
            if (_canvasImage == null) return null;

            // Map pointer position in the view to bitmap pixel-space using the visual transform chain.
            // This stays correct under zoom/pan and avoids off-by-one errors from manual centering math.
            var toView = _canvasImage.TransformToVisual(this);
            if (toView == null) return null;

            var pView = e.GetPosition(this);
            if (!toView.Value.TryInvert(out var inv)) return null;

            var pImg = inv.Transform(pView);

            double bitmapX = pImg.X;
            double bitmapY = pImg.Y;

            int w = _canvasBitmap.PixelSize.Width;
            int h = _canvasBitmap.PixelSize.Height;
            if (bitmapX < 0 || bitmapY < 0 || bitmapX >= w || bitmapY >= h) return null;

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

            if (ActiveTool == ToolType.Select)
            {
                _selStartX = (int)coords.Value.X;
                _selStartY = (int)coords.Value.Y;
                _selEndX   = _selStartX;
                _selEndY   = _selStartY;
                _selDragging = true;
                _hasSelection = true;
                UpdateSelectionOverlay();
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Shape)
            {
                _shapeStartX = (int)coords.Value.X;
                _shapeStartY = (int)coords.Value.Y;
                _shapeDragging = true;
                _lastPoint = coords.Value;
                SaveStateForUndo();
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Gradient)
            {
                _gradStartX = (int)coords.Value.X;
                _gradStartY = (int)coords.Value.Y;
                _gradDragging = true;
                _lastPoint = coords.Value;
                SaveStateForUndo();
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Text)
            {
                if (ActiveLayer == null || ActiveLayer.LockPixels) return; // Block text entry on locked layer

                if (_textOverlayBox != null && _textOverlayBox.IsVisible)
                {
                    CommitText(); // they clicked elsewhere, commit the previous text
                }
                
                if (_textOverlayBox != null)
                {
                    SaveStateForUndo();
                    Avalonia.Controls.Canvas.SetLeft(_textOverlayBox, coords.Value.X);
                    Avalonia.Controls.Canvas.SetTop(_textOverlayBox, coords.Value.Y);
                    _textOverlayBox.Text = "";
                    _textOverlayBox.IsVisible = true;
                    // Try to focus it so they can type immediately
                    Dispatcher.UIThread.Post(() => _textOverlayBox.Focus(), DispatcherPriority.Input);
                }
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Move)
            {
                _lastPoint = coords.Value;
                SaveStateForUndo(); // dragging starts
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

            if (ActiveTool == ToolType.Select && _selDragging)
            {
                _selEndX = (int)coords.Value.X;
                _selEndY = (int)coords.Value.Y;
                UpdateSelectionOverlay();
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Shape && _shapeDragging)
            {
                // To do live preview of shape, we could erase and redraw, but for now we draw on release.
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Gradient && _gradDragging)
            {
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Text)
            {
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Move && _lastPoint.HasValue && ActiveLayer != null)
            {
                if (ActiveLayer.LockPosition) return;

                int dx = (int)(coords.Value.X - _lastPoint.Value.X);
                int dy = (int)(coords.Value.Y - _lastPoint.Value.Y);

                if (dx != 0 || dy != 0)
                {
                    ActiveLayer.TranslatePixels(dx, dy); // We will add this helper
                    _lastPoint = coords.Value;
                    RedrawCanvas();
                }
                e.Handled = true;
                return;
            }

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

            if (_selDragging)
            {
                _selDragging = false;
                EnsureValidSelectionBox();
                e.Handled = true;
                return;
            }

            if (_shapeDragging)
            {
                _shapeDragging = false;
                var coords = GetBitmapCoordinates(e);
                if (coords != null)
                {
                    int endX = (int)coords.Value.X;
                    int endY = (int)coords.Value.Y;
                    DrawShape(_shapeStartX, _shapeStartY, endX, endY);
                }
                e.Handled = true;
                return;
            }

            if (_gradDragging)
            {
                _gradDragging = false;
                var coords = GetBitmapCoordinates(e);
                if (coords != null)
                {
                    int endX = (int)coords.Value.X;
                    int endY = (int)coords.Value.Y;
                    DrawGradient(_gradStartX, _gradStartY, endX, endY);
                }
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Text)
            {
                e.Handled = true;
                return;
            }

            if (ActiveTool == ToolType.Move && _lastPoint.HasValue)
            {
                _lastPoint = null;
                e.Handled = true;
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
            double delta = e.Delta.Y > 0 ? 1.1 : 0.9;
            ApplyZoomDelta(delta, e.GetPosition(this));
            e.Handled = true;
        }

        private void ApplyZoomDelta(double delta, Point pivot)
        {
            if (_scaleTransform == null || _translateTransform == null) return;

            double newZoom = Math.Clamp(_zoom * delta, 0.05, 20.0);
            if (Math.Abs(newZoom - _zoom) < 1e-6) return;

            double ratio = newZoom / _zoom;
            double relX  = pivot.X - _translateTransform.X;
            double relY  = pivot.Y - _translateTransform.Y;

            _translateTransform.X = pivot.X - relX * ratio;
            _translateTransform.Y = pivot.Y - relY * ratio;
            _panTranslation = new Point(_translateTransform.X, _translateTransform.Y);

            _zoom                  = newZoom;
            _scaleTransform.ScaleX = _zoom;
            _scaleTransform.ScaleY = _zoom;

            ZoomChanged?.Invoke(this, _zoom);
        }

        // Keyboard shortcuts: B=Brush, E=Eraser, I=Eyedropper, G=Fill
        //                     [/] = brush -/+ size, X = swap colors (routed via event)
        private void CanvasView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) { _spaceHeld = true; e.Handled = true; return; }

            // Ignore when a TextBox has focus
            if (e.Source is TextBox) return;

            switch (e.Key)
            {
                case Key.B: SetTool(ToolType.Brush);      e.Handled = true; break;
                case Key.E: SetTool(ToolType.Eraser);     e.Handled = true; break;
                case Key.I: SetTool(ToolType.Eyedropper); e.Handled = true; break;
                case Key.G: SetTool(ToolType.Fill);       e.Handled = true; break;

                case Key.OemOpenBrackets:   // [
                    _brushRadius = Math.Max(1f, _brushRadius - 2f);
                    e.Handled = true;
                    break;
                case Key.OemCloseBrackets:  // ]
                    _brushRadius = Math.Min(300f, _brushRadius + 2f);
                    e.Handled = true;
                    break;

                case Key.X:
                    // Route swap to ToolsPanel via event — ToolsPanel owns primary/secondary colors
                    RequestColorSwap?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>Raised when the user presses X to swap primary/secondary color.</summary>
        public event EventHandler? RequestColorSwap;

        private void SetTool(ToolType tool)
        {
            if (ActiveTool == ToolType.Text && tool != ToolType.Text)
            {
                CommitText();
            }

            ActiveTool = tool;
            ToolChanged?.Invoke(this, tool);
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
            if (ActiveLayer == null || ActiveLayer.LockPixels) return;

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

        // ── Shape & Selection Helpers ────────────────────────────────────────

        private void UpdateSelectionOverlay()
        {
            if (_selectionMarquee == null || _selectionMarqueeInner == null) return;
            
            if (!_hasSelection)
            {
                _selectionMarquee.IsVisible = false;
                _selectionMarqueeInner.IsVisible = false;
                return;
            }

            int minX = Math.Min(_selStartX, _selEndX);
            int minY = Math.Min(_selStartY, _selEndY);
            int maxX = Math.Max(_selStartX, _selEndX);
            int maxY = Math.Max(_selStartY, _selEndY);

            double width  = maxX - minX;
            double height = maxY - minY;

            if (width < 1 || height < 1)
            {
                _selectionMarquee.IsVisible = false;
                _selectionMarqueeInner.IsVisible = false;
                return;
            }

            // Since the RenderTransform is on TransformContainer, the Canvas is drawn
            // in the same coordinate space as the image! (1 pixel = 1 unit)
            // But we need to offset it by the padding if the image is centered.
            // Wait, the Image and Canvas are siblings, both centered. 
            // The Canvas will just overlay the grid. We should set Canvas sizing.
            // Actually, setting Margin or Canvas.Left/Top works.
            Avalonia.Controls.Canvas.SetLeft(_selectionMarquee, minX);
            Avalonia.Controls.Canvas.SetTop(_selectionMarquee, minY);
            _selectionMarquee.Width = width;
            _selectionMarquee.Height = height;
            _selectionMarquee.IsVisible = true;

            Avalonia.Controls.Canvas.SetLeft(_selectionMarqueeInner, minX);
            Avalonia.Controls.Canvas.SetTop(_selectionMarqueeInner, minY);
            _selectionMarqueeInner.Width = width;
            _selectionMarqueeInner.Height = height;
            _selectionMarqueeInner.IsVisible = true;
        }

        private void EnsureValidSelectionBox()
        {
            int minX = Math.Min(_selStartX, _selEndX);
            int minY = Math.Min(_selStartY, _selEndY);
            int maxX = Math.Max(_selStartX, _selEndX);
            int maxY = Math.Max(_selStartY, _selEndY);

            _selStartX = minX;
            _selStartY = minY;
            _selEndX   = maxX;
            _selEndY   = maxY;

            if (maxX - minX < 1 || maxY - minY < 1)
            {
                _hasSelection = false;
                UpdateSelectionOverlay();
            }
        }

        private void DrawShape(int startX, int startY, int endX, int endY)
        {
            if (ActiveLayer == null || ActiveLayer.LockPixels) return;

            int minX = Math.Max(0, Math.Min(startX, endX));
            int minY = Math.Max(0, Math.Min(startY, endY));
            int maxX = Math.Min(_document.Width - 1, Math.Max(startX, endX));
            int maxY = Math.Min(_document.Height - 1, Math.Max(startY, endY));

            if (maxX < minX || maxY < minY) return;

            uint fillAlpha = (uint)(255 * _brushOpacity);
            uint color = (fillAlpha << 24) | (_activeColor & 0x00FFFFFF);
            uint[] pixels = ActiveLayer.GetPixels();

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    pixels[y * _document.Width + x] = color; 
                }
            }

            RedrawCanvas();
        }

        private void CommitText()
        {
            if (_textOverlayBox == null || !_textOverlayBox.IsVisible || ActiveLayer == null) return;
            
            string text = _textOverlayBox.Text ?? "";
            _textOverlayBox.IsVisible = false;
            if (string.IsNullOrWhiteSpace(text)) return;
            
            double x = Avalonia.Controls.Canvas.GetLeft(_textOverlayBox);
            double y = Avalonia.Controls.Canvas.GetTop(_textOverlayBox);

            uint fillAlpha = (uint)(255 * _brushOpacity);
            var color = Avalonia.Media.Color.FromArgb((byte)fillAlpha, (byte)((_activeColor >> 16) & 0xFF), (byte)((_activeColor >> 8) & 0xFF), (byte)(_activeColor & 0xFF));
            var brush = new SolidColorBrush(color);

            var typeface = new Typeface(_textOverlayBox.FontFamily, _textOverlayBox.FontStyle, _textOverlayBox.FontWeight);
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                _textOverlayBox.FontSize,
                brush);

            int w = (int)Math.Ceiling(formattedText.Width) + 1;
            int h = (int)Math.Ceiling(formattedText.Height) + 1;

            if (w <= 1 || h <= 1) return;

            var rtb = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            using (var ctx = rtb.CreateDrawingContext())
            {
                ctx.DrawText(formattedText, new Point(0, 0));
            }

            uint[] textPixels = new uint[w * h];
            unsafe
            {
                fixed (uint* ptr = textPixels)
                {
                    rtb.CopyPixels(new PixelRect(0, 0, w, h), (IntPtr)ptr, w * h * 4, w * 4);
                }
            }

            int startX = (int)x;
            int startY = (int)y;
            uint[] layerPixels = ActiveLayer.GetPixels();

            for (int ty = 0; ty < h; ty++)
            {
                int ly = startY + ty;
                if (ly < 0 || ly >= _document.Height) continue;

                for (int tx = 0; tx < w; tx++)
                {
                    int lx = startX + tx;
                    if (lx < 0 || lx >= _document.Width) continue;

                    uint srcPx = textPixels[ty * w + tx];
                    float srcA = ((srcPx >> 24) & 0xFF) / 255.0f;
                    if (srcA > 0)
                    {
                        uint dstPx = layerPixels[ly * _document.Width + lx];
                        float dstA = ((dstPx >> 24) & 0xFF) / 255.0f;
                        float invA = 1.0f - srcA;
                        float outA = srcA + dstA * invA;

                        float sR = ((srcPx >> 16) & 0xFF) / 255.0f;
                        float sG = ((srcPx >> 8) & 0xFF) / 255.0f;
                        float sB = (srcPx & 0xFF) / 255.0f;

                        float dR = ((dstPx >> 16) & 0xFF) / 255.0f;
                        float dG = ((dstPx >> 8) & 0xFF) / 255.0f;
                        float dB = (dstPx & 0xFF) / 255.0f;

                        float oR = (sR * srcA + dR * dstA * invA) / outA;
                        float oG = (sG * srcA + dG * dstA * invA) / outA;
                        float oB = (sB * srcA + dB * dstA * invA) / outA;

                        uint A = (uint)Math.Clamp(outA * 255f, 0, 255);
                        uint R = (uint)Math.Clamp(oR * 255f, 0, 255);
                        uint G = (uint)Math.Clamp(oG * 255f, 0, 255);
                        uint B = (uint)Math.Clamp(oB * 255f, 0, 255);

                        layerPixels[ly * _document.Width + lx] = (A << 24) | (R << 16) | (G << 8) | B;
                    }
                }
            }

            RedrawCanvas();
        }

        private void DrawGradient(int startX, int startY, int endX, int endY)
        {
            if (ActiveLayer == null || ActiveLayer.LockPixels) return;

            uint[] pixels = ActiveLayer.GetPixels();
            uint fillAlpha = (uint)(255 * _brushOpacity);
            uint startColor = (fillAlpha << 24) | (_activeColor & 0x00FFFFFF);
            uint endColor   = (fillAlpha << 24) | (_secondaryColor & 0x00FFFFFF);

            float sA = ((startColor >> 24) & 0xFF);
            float sR = ((startColor >> 16) & 0xFF);
            float sG = ((startColor >> 8) & 0xFF);
            float sB = (startColor & 0xFF);

            float eA = ((endColor >> 24) & 0xFF);
            float eR = ((endColor >> 16) & 0xFF);
            float eG = ((endColor >> 8) & 0xFF);
            float eB = (endColor & 0xFF);

            double dx = endX - startX;
            double dy = endY - startY;
            double lenSq = dx * dx + dy * dy;

            for (int y = 0; y < _document.Height; y++)
            {
                for (int x = 0; x < _document.Width; x++)
                {
                    double t = 0;
                    if (lenSq > 0)
                    {
                        t = ((x - startX) * dx + (y - startY) * dy) / lenSq;
                        t = Math.Clamp(t, 0, 1);
                    }

                    uint A = (uint)Math.Clamp(sA + (eA - sA) * t, 0, 255);
                    uint R = (uint)Math.Clamp(sR + (eR - sR) * t, 0, 255);
                    uint G = (uint)Math.Clamp(sG + (eG - sG) * t, 0, 255);
                    uint B = (uint)Math.Clamp(sB + (eB - sB) * t, 0, 255);

                    uint gradPx = (A << 24) | (R << 16) | (G << 8) | B;

                    // Alpha composite using shared utility
                    pixels[y * _document.Width + x] = ColorMath.AlphaComposite(
                        gradPx, pixels[y * _document.Width + x]);
                }
            }
            RedrawCanvas();
        }

        // ── Rendering ────────────────────────────────────────────────────────

        private void RedrawCanvas()
        {
            // Composite + blit only the union dirty rect (much cheaper than full-canvas work).
            // NOTE: bitmap invalidation is still visual-level, but pixel work is now dirty-rect scoped.
            IntRect dirty = _document.DirtyRegion;
            for (int i = 0; i < _layers.Count; i++)
                dirty = IntRect.Union(dirty, _layers[i].DirtyRegion);

            if (dirty.IsEmpty)
                dirty = new IntRect(0, 0, _document.Width, _document.Height);

            LayerCompositor.Composite(_document, _layers, dirty);
            _renderer.Render(_document, _canvasBitmap, dirty);
            _document.ClearDirty();

            // T1: InvalidateVisual MUST run on the UI thread
            Dispatcher.UIThread.Post(() => 
            {
                UpdateGridOverlay();
                _canvasImage?.InvalidateVisual();
            }, DispatcherPriority.Render);
        }

        // ── Undo / Redo ──────────────────────────────────────────────────────

        public void SaveStateForUndo(string actionName = "Action")
        {
            History.AddStep(actionName, _layers, _activeLayerIndex);
            System.Diagnostics.Debug.WriteLine($"✅ Saved undo state: {actionName}. Steps: {History.Steps.Count}");
        }

        public void Undo()
        {
            var step = History.Undo();
            if (step != null)
            {
                RestoreHistoryStep(step);
                System.Diagnostics.Debug.WriteLine($"✅ Undid to: {step.ActionName}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Nothing to undo");
            }
        }

        public void Redo()
        {
            var step = History.Redo();
            if (step != null)
            {
                RestoreHistoryStep(step);
                System.Diagnostics.Debug.WriteLine($"✅ Redid to: {step.ActionName}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Nothing to redo");
            }
        }

        public void JumpToHistoryState(int index)
        {
            var step = History.JumpTo(index);
            if (step != null)
            {
                RestoreHistoryStep(step);
                System.Diagnostics.Debug.WriteLine($"✅ Jumped to history step: {step.ActionName}");
            }
        }

        private void RestoreHistoryStep(HistoryStep step)
        {
            _layers.Clear();

            // Determine canvas dimensions from the first snapshot (all layers share the same size)
            int snapW = _document.Width;
            int snapH = _document.Height;
            if (step.Layers.Count > 0)
            {
                var first = step.Layers[0];
                if (first.Width > 0 && first.Height > 0)
                {
                    snapW = first.Width;
                    snapH = first.Height;
                }
            }

            // Rebuild document and bitmap if dimensions changed
            if (snapW != _document.Width || snapH != _document.Height)
            {
                _document = new Document(snapW, snapH);
                _canvasBitmap = BitmapFactory.Create(snapW, snapH);
                if (_canvasImage != null) _canvasImage.Source = _canvasBitmap;
            }

            foreach (var s in step.Layers)
            {
                int layerW = s.Width  > 0 ? s.Width  : snapW;
                int layerH = s.Height > 0 ? s.Height : snapH;

                var layer = new Layer(layerW, layerH, s.Name)
                {
                    Opacity          = s.Opacity,
                    Mode             = s.BlendMode,
                    Visible          = s.IsVisible,
                    LockTransparency = s.LockTransparency,
                    LockPixels       = s.LockPixels,
                    LockPosition     = s.LockPosition,
                    IsClippingMask   = s.IsClippingMask
                };
                var ptr = layer.GetPixels();
                int copyLen = Math.Min(s.Pixels.Length, ptr.Length);
                Array.Copy(s.Pixels, ptr, copyLen);
                _layers.Add(layer);
            }

            _activeLayerIndex = Math.Min(step.ActiveLayerIndex, Math.Max(0, _layers.Count - 1));
            TriggerRedraw();
        }
    }
}