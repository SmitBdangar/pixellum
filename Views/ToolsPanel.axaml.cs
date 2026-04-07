using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Pixellum.Controls;
using Pixellum.Core;

namespace Pixellum.Views
{
    public partial class ToolsPanel : UserControl
    {
        private CanvasView? _canvas;
        private uint _primaryColor = 0xFF000000;   // Black
        private uint _secondaryColor = 0xFFFFFFFF; // White
        private ToolType _activeTool = ToolType.Brush;

        // Quick color palette (12 common colors)
        private readonly string[] _quickColors = new[]
        {
            "#000000", "#FFFFFF", "#FF0000", "#00FF00", "#0000FF", "#FFFF00",
            "#FF00FF", "#00FFFF", "#FFA500", "#800080", "#FFC0CB", "#A52A2A"
        };

        public ToolsPanel()
        {
            InitializeComponent();

            this.Loaded += (_, __) =>
            {
                var window = this.GetVisualRoot() as Window;
                if (window != null)
                {
                    _canvas = window.GetVisualDescendants()
                                    .OfType<CanvasView>()
                                    .FirstOrDefault();
                                        
                    if (_canvas != null)
                    {
                        _canvas.ColorPicked += (_, color) =>
                        {
                            _primaryColor = color;
                            UpdatePrimaryColorPreview();
                            if (_canvas != null) _canvas.ActiveColor = _primaryColor;
                        };
                        _canvas.RequestColorSwap += (_, _) => SwapColors();
                        _canvas.ToolChanged += (_, tool) =>
                        {
                            _activeTool = tool;
                            UpdateToolButtons();
                        };
                        _canvas.ActiveTool = _activeTool;
                    }
                }

                UpdatePrimaryColorPreview();
                UpdateSecondaryColorPreview();
            };

            // Color swap on swatch click
            PrimaryColorPreview.PointerPressed += (_, __) => SwapColors();
            SecondaryColorPreview.PointerPressed += (_, __) => SwapColors();
        }

        private void InitializeQuickColors()
        {
            // Quick color grid is now in the main window's Color & Swatches section.
            // Populate the RightSwatchGrid if available.
            var window = this.GetVisualRoot() as Window;
            if (window == null) return;
            var grid = window.FindControl<UniformGrid>("RightSwatchGrid");
            if (grid == null) return;
            grid.Children.Clear();

            foreach (var hexColor in _quickColors)
            {
                var color = Color.Parse(hexColor);
                var border = new Border
                {
                    Width = 22,
                    Height = 16,
                    Background = new SolidColorBrush(color),
                    Margin = new Thickness(1),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };

                border.PointerPressed += (_, __) =>
                {
                    _primaryColor = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
                    UpdatePrimaryColorPreview();
                    if (_canvas != null) _canvas.ActiveColor = _primaryColor;
                    SyncRgbSliders(window);
                };

                grid.Children.Add(border);
            }
        }

        private static void SyncRgbSliders(Window w)
        {
            // No-op stub — sliders are synced via MainWindow.OnRgbSliderChanged
        }

        private void UpdatePrimaryColorPreview()
        {
            byte a = (byte)((_primaryColor >> 24) & 0xFF);
            byte r = (byte)((_primaryColor >> 16) & 0xFF);
            byte g = (byte)((_primaryColor >> 8) & 0xFF);
            byte b = (byte)(_primaryColor & 0xFF);

            PrimaryColorPreview.Background = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }

        private void UpdateSecondaryColorPreview()
        {
            byte a = (byte)((_secondaryColor >> 24) & 0xFF);
            byte r = (byte)((_secondaryColor >> 16) & 0xFF);
            byte g = (byte)((_secondaryColor >> 8) & 0xFF);
            byte b = (byte)(_secondaryColor & 0xFF);

            SecondaryColorPreview.Background = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }

        private void UpdateHexInput()
        {
            // Hex input is no longer in the ToolsPanel AXAML — no-op.
        }

        private void SwapColors()
        {
            (_primaryColor, _secondaryColor) = (_secondaryColor, _primaryColor);
            
            UpdatePrimaryColorPreview();
            UpdateSecondaryColorPreview();
            UpdateHexInput();

            if (_canvas != null)
            {
                _canvas.ActiveColor = _primaryColor;
            }
        }

        private void OnHexColorChanged(object? sender, TextChangedEventArgs e)
        {
            // Hex input has moved to MainWindow — no-op in ToolsPanel now.
        }

        // Tool Selection Handlers
        private void OnBrushToolClicked(object? sender, RoutedEventArgs e)
        {
            _activeTool = ToolType.Brush;
            UpdateToolButtons();
            System.Diagnostics.Debug.WriteLine("🖌️ Brush tool selected");
        }

        private void OnEraserToolClicked(object? sender, RoutedEventArgs e)
        {
            _activeTool = ToolType.Eraser;
            UpdateToolButtons();
            System.Diagnostics.Debug.WriteLine("🧹 Eraser tool selected");
        }

        private void OnEyedropperToolClicked(object? sender, RoutedEventArgs e)
        {
            _activeTool = ToolType.Eyedropper;
            UpdateToolButtons();
        }

        private void OnFillToolClicked(object? sender, RoutedEventArgs e)
        {
            _activeTool = ToolType.Fill;
            UpdateToolButtons();
        }

        private void OnSelectToolClicked(object? sender, RoutedEventArgs e)
        {
            _activeTool = ToolType.Select;
            UpdateToolButtons();
        }

        private void OnMoveToolClicked(object? sender, RoutedEventArgs e)
        {
            _activeTool = ToolType.Move;
            UpdateToolButtons();
        }

        private void OnShapeToolClicked(object? sender, RoutedEventArgs e)
        {
            _activeTool = ToolType.Shape;
            UpdateToolButtons();
        }

        private void OnTextToolClicked(object? sender, RoutedEventArgs e)
        {
            _activeTool = ToolType.Text;
            UpdateToolButtons();
        }

        private void OnGradientToolClicked(object? sender, RoutedEventArgs e)
        {
            _activeTool = ToolType.Gradient;
            UpdateToolButtons();
        }

        private void UpdateToolButtons()
        {
            void SetActive(Button? btn, bool active)
            {
                if (btn == null) return;
                if (active) { if (!btn.Classes.Contains("active")) btn.Classes.Add("active"); }
                else        { btn.Classes.Remove("active"); }
            }

            SetActive(BrushToolButton,      _activeTool == ToolType.Brush);
            SetActive(EraserToolButton,     _activeTool == ToolType.Eraser);
            SetActive(EyedropperToolButton, _activeTool == ToolType.Eyedropper);
            SetActive(FillToolButton,       _activeTool == ToolType.Fill);
            SetActive(this.FindControl<Button>("SelectToolButton"),  _activeTool == ToolType.Select);
            SetActive(this.FindControl<Button>("MoveToolButton"),    _activeTool == ToolType.Move);
            SetActive(this.FindControl<Button>("ShapeToolButton"),   _activeTool == ToolType.Shape);
            SetActive(this.FindControl<Button>("TextToolButton"),    _activeTool == ToolType.Text);
            SetActive(this.FindControl<Button>("GradientToolButton"),_activeTool == ToolType.Gradient);

            if (_canvas != null)
                _canvas.ActiveTool = _activeTool;
        }

        private void OnClearCanvasClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;
            var layers = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();
            if (activeIndex >= 0 && activeIndex < layers.Count)
            {
                _canvas.SaveUndoState();
                layers[activeIndex].Clear();
                _canvas.TriggerRedraw();
            }
        }

        private void OnFillCanvasClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;
            var layers = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();
            if (activeIndex >= 0 && activeIndex < layers.Count)
            {
                _canvas.SaveUndoState();
                var layer  = layers[activeIndex];
                var pixels = layer.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = _primaryColor;
                _canvas.TriggerRedraw();
            }
        }
    }
}