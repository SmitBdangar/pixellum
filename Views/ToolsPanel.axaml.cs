using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Pixellum.Controls;

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
                            UpdateHexInput();
                            _canvas.ActiveColor = _primaryColor;
                        };
                        _canvas.ActiveTool = _activeTool;   // ToolType enum
                    }
                }

                InitializeQuickColors();
                UpdatePrimaryColorPreview();
                UpdateSecondaryColorPreview();
            };

            // Wire up slider events
            BrushSizeSlider.ValueChanged += (_, e) =>
            {
                BrushSizeValue.Text = $"{(int)e.NewValue}px";
                UpdateBrushSettings();
            };

            OpacitySlider.ValueChanged += (_, e) =>
            {
                OpacityValue.Text = $"{(int)(e.NewValue * 100)}%";
                UpdateBrushSettings();
            };

            HardnessSlider.ValueChanged += (_, e) =>
            {
                HardnessValue.Text = $"{(int)(e.NewValue * 100)}%";
            };

            FlowSlider.ValueChanged += (_, e) =>
            {
                FlowValue.Text = $"{(int)(e.NewValue * 100)}%";
            };

            // Color wheel event
            ColorPicker.ActiveColorChanged += (_, color) =>
            {
                _primaryColor = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
                UpdatePrimaryColorPreview();
                UpdateHexInput();
                
                if (_canvas != null)
                {
                    _canvas.ActiveColor = _primaryColor;
                }
            };

            // Color swap on click
            PrimaryColorPreview.PointerPressed += (_, __) => SwapColors();
            SecondaryColorPreview.PointerPressed += (_, __) => SwapColors();
        }

        private void InitializeQuickColors()
        {
            QuickColorGrid.Children.Clear();

            foreach (var hexColor in _quickColors)
            {
                var color = Color.Parse(hexColor);
                var border = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(color),
                    Margin = new Thickness(2),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };

                border.PointerPressed += (_, __) =>
                {
                    _primaryColor = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
                    UpdatePrimaryColorPreview();
                    UpdateHexInput();
                    
                    if (_canvas != null)
                    {
                        _canvas.ActiveColor = _primaryColor;
                    }
                };

                border.PointerEntered += (_, __) =>
                {
                    border.BorderBrush = new SolidColorBrush(Color.Parse("#4CAF50"));
                    border.BorderThickness = new Thickness(2);
                };

                border.PointerExited += (_, __) =>
                {
                    border.BorderBrush = Brushes.White;
                    border.BorderThickness = new Thickness(1);
                };

                QuickColorGrid.Children.Add(border);
            }
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
            byte r = (byte)((_primaryColor >> 16) & 0xFF);
            byte g = (byte)((_primaryColor >> 8) & 0xFF);
            byte b = (byte)(_primaryColor & 0xFF);

            HexColorInput.Text = $"#{r:X2}{g:X2}{b:X2}";
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
            var input = HexColorInput.Text?.Trim();
            if (string.IsNullOrEmpty(input) || !input.StartsWith("#"))
                return;

            try
            {
                var color = Color.Parse(input);
                _primaryColor = (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
                UpdatePrimaryColorPreview();

                if (_canvas != null)
                {
                    _canvas.ActiveColor = _primaryColor;
                }
            }
            catch
            {
                // Invalid hex color, ignore
            }
        }

        private void UpdateBrushSettings()
        {
            if (_canvas == null) return;

            _canvas.BrushRadius = (float)BrushSizeSlider.Value;
            _canvas.BrushOpacity = (float)OpacitySlider.Value;
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
            System.Diagnostics.Debug.WriteLine("💧 Eyedropper tool selected");
        }

        private void OnFillToolClicked(object? sender, RoutedEventArgs e)
        {
            _activeTool = ToolType.Fill;
            UpdateToolButtons();
            System.Diagnostics.Debug.WriteLine("🪣 Fill tool selected");
        }

        private void UpdateToolButtons()
        {
            // Reset all tool buttons
            BrushToolButton.Background = new SolidColorBrush(Color.Parse("#555"));
            EraserToolButton.Background = new SolidColorBrush(Color.Parse("#555"));
            EyedropperToolButton.Background = new SolidColorBrush(Color.Parse("#555"));
            FillToolButton.Background = new SolidColorBrush(Color.Parse("#555"));

            // Highlight active tool button
            var activeColor = new SolidColorBrush(Color.Parse("#4CAF50"));

            switch (_activeTool)
            {
                case ToolType.Brush:      BrushToolButton.Background      = activeColor; break;
                case ToolType.Eraser:     EraserToolButton.Background     = activeColor; break;
                case ToolType.Eyedropper: EyedropperToolButton.Background = activeColor; break;
                case ToolType.Fill:       FillToolButton.Background       = activeColor; break;
            }

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
                layers[activeIndex].Clear();
                // Trigger redraw by calling a method on canvas
                System.Diagnostics.Debug.WriteLine("🗑️ Canvas cleared");
            }
        }

        private void OnFillCanvasClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;

            var layers = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();

            if (activeIndex >= 0 && activeIndex < layers.Count)
            {
                var layer = layers[activeIndex];
                var pixels = layer.GetPixels();
                
                // Fill entire layer with primary color
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = _primaryColor;
                }

                System.Diagnostics.Debug.WriteLine("🎨 Canvas filled with color");
            }
        }

        private void OnBrushPresetChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (BrushPresetsComboBox != null && BrushPresetsComboBox.SelectedItem is ComboBoxItem item && _activeTool == ToolType.Brush)
            {
                string preset = item.Content?.ToString() ?? "";
                switch (preset)
                {
                    case "Standard Brush":
                        BrushSizeSlider.Value = 15;
                        OpacitySlider.Value = 1.0;
                        HardnessSlider.Value = 1.0;
                        FlowSlider.Value = 1.0;
                        break;
                    case "Soft Airbrush":
                        BrushSizeSlider.Value = 40;
                        OpacitySlider.Value = 0.3;
                        HardnessSlider.Value = 0.0;
                        FlowSlider.Value = 0.2;
                        break;
                    case "Inking Pen":
                        BrushSizeSlider.Value = 5;
                        OpacitySlider.Value = 1.0;
                        HardnessSlider.Value = 1.0;
                        FlowSlider.Value = 1.0;
                        break;
                }
            }
        }
    }
}