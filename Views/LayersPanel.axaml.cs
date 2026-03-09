using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.Platform;
using Pixellum.Core;

namespace Pixellum.Views
{
    public partial class LayersPanel : UserControl
    {
        private CanvasView? _canvas;
        private int _layerCounter = 2;   // Layer 1 is created at startup

        public LayersPanel()
        {
            InitializeComponent();

            this.Loaded += (_, _) =>
            {
                var window = this.GetVisualRoot() as Window;
                if (window != null)
                {
                    _canvas = window.GetVisualDescendants()
                                    .OfType<CanvasView>()
                                    .FirstOrDefault();

                    if (_canvas != null)
                        RefreshLayersList();
                }
            };
        }

        // ── Layer commands ────────────────────────────────────────────────

        private void OnAddLayerClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;
            _canvas.AddLayer($"Layer {_layerCounter++}");
            RefreshLayersList();
        }

        private void OnDuplicateLayerClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;

            var layers     = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();
            if (activeIndex < 0 || activeIndex >= layers.Count) return;

            var source = layers[activeIndex];
            _canvas.AddLayer($"{source.Name} Copy");

            // Copy pixels
            var newLayer = _canvas.GetLayers().Last();
            Array.Copy(source.GetPixels(), newLayer.GetPixels(), source.GetPixels().Length);
            RefreshLayersList();
        }

        private void OnMergeDownClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;

            var layers     = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();

            if (layers.Count <= 1 || activeIndex <= 0) return;

            var activeLayer = layers[activeIndex];
            var belowLayer  = layers[activeIndex - 1];
            var srcPixels   = activeLayer.GetPixels();
            var dstPixels   = belowLayer.GetPixels();

            // Straight-alpha src-over merge
            for (int i = 0; i < srcPixels.Length; i++)
            {
                uint src  = srcPixels[i];
                uint dst  = dstPixels[i];
                float srcA = ((src >> 24) & 0xFF) / 255.0f;
                float dstA = ((dst >> 24) & 0xFF) / 255.0f;
                float invSrcA = 1.0f - srcA;
                float outA = srcA + dstA * invSrcA;

                if (outA < 1e-6f) { dstPixels[i] = 0; continue; }

                float srcR = ((src >> 16) & 0xFF) / 255.0f;
                float srcG = ((src >>  8) & 0xFF) / 255.0f;
                float srcB = ( src        & 0xFF) / 255.0f;
                float dstR = ((dst >> 16) & 0xFF) / 255.0f;
                float dstG = ((dst >>  8) & 0xFF) / 255.0f;
                float dstB = ( dst        & 0xFF) / 255.0f;

                uint A = (uint)Math.Clamp(outA * 255f, 0, 255);
                uint R = (uint)Math.Clamp(((srcR * srcA + dstR * dstA * invSrcA) / outA) * 255f, 0, 255);
                uint G = (uint)Math.Clamp(((srcG * srcA + dstG * dstA * invSrcA) / outA) * 255f, 0, 255);
                uint B = (uint)Math.Clamp(((srcB * srcA + dstB * dstA * invSrcA) / outA) * 255f, 0, 255);

                dstPixels[i] = (A << 24) | (R << 16) | (G << 8) | B;
            }

            _canvas.DeleteLayer(activeIndex);
            RefreshLayersList();
        }

        private void OnLayerOpacityChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (_canvas == null) return;

            // Update label
            var opacityText = this.FindControl<TextBlock>("OpacityValueText");
            if (opacityText != null)
                opacityText.Text = $"{(int)e.NewValue}%";

            var layers     = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();
            if (activeIndex < 0 || activeIndex >= layers.Count) return;

            layers[activeIndex].Opacity = (float)e.NewValue / 100.0f;

            // ✅ Fixed: actually trigger redraw after opacity change
            _canvas.TriggerRedraw();
        }

        private void OnBlendModeChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_canvas == null) return;

            var combo = sender as ComboBox;
            if (combo?.SelectedItem is not ComboBoxItem item) return;

            var layers     = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();
            if (activeIndex < 0 || activeIndex >= layers.Count) return;

            layers[activeIndex].Mode = item.Content?.ToString() switch
            {
                "Multiply" => BlendMode.Multiply,
                "Screen"   => BlendMode.Screen,
                "Overlay"  => BlendMode.Overlay,
                _          => BlendMode.Normal
            };

            _canvas.TriggerRedraw();
        }

        // ── Layer reorder ─────────────────────────────────────────────────

        private void MoveLayerUp(int index)
        {
            if (_canvas == null) return;
            var layers = _canvas.GetLayers();
            if (index >= layers.Count - 1) return;

            (layers[index], layers[index + 1]) = (layers[index + 1], layers[index]);
            if (_canvas.GetActiveLayerIndex() == index)
                _canvas.SetActiveLayer(index + 1);
            else if (_canvas.GetActiveLayerIndex() == index + 1)
                _canvas.SetActiveLayer(index);

            _canvas.TriggerRedraw();
            RefreshLayersList();
        }

        private void MoveLayerDown(int index)
        {
            if (_canvas == null) return;
            var layers = _canvas.GetLayers();
            if (index <= 0) return;

            (layers[index], layers[index - 1]) = (layers[index - 1], layers[index]);
            if (_canvas.GetActiveLayerIndex() == index)
                _canvas.SetActiveLayer(index - 1);
            else if (_canvas.GetActiveLayerIndex() == index - 1)
                _canvas.SetActiveLayer(index);

            _canvas.TriggerRedraw();
            RefreshLayersList();
        }

        // ── Refresh ───────────────────────────────────────────────────────

        public void RefreshLayersList()
        {
            if (_canvas == null) return;

            var stack = this.FindControl<StackPanel>("LayersStack");
            if (stack == null) return;
            stack.Children.Clear();

            var layers     = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();

            // Update header info
            var activeLayerName = this.FindControl<TextBlock>("ActiveLayerName");
            if (activeLayerName != null && activeIndex >= 0 && activeIndex < layers.Count)
                activeLayerName.Text = layers[activeIndex].Name;

            var opacitySlider = this.FindControl<Slider>("LayerOpacitySlider");
            if (opacitySlider != null && activeIndex >= 0 && activeIndex < layers.Count)
                opacitySlider.Value = layers[activeIndex].Opacity * 100;

            var blendCombo = this.FindControl<ComboBox>("BlendModeComboBox");
            if (blendCombo != null && activeIndex >= 0 && activeIndex < layers.Count)
            {
                string modeName = layers[activeIndex].Mode.ToString();
                for (int ci = 0; ci < blendCombo.Items.Count; ci++)
                {
                    if (blendCombo.Items[ci] is ComboBoxItem cbi &&
                        cbi.Content?.ToString() == modeName)
                    {
                        blendCombo.SelectedIndex = ci;
                        break;
                    }
                }
            }

            // Build layer rows (top to bottom = highest index first)
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                stack.Children.Add(CreateLayerItem(layers[i], i, i == activeIndex, layers.Count));
            }
        }

        // ── Layer item UI ─────────────────────────────────────────────────

        private Border CreateLayerItem(Layer layer, int layerIndex, bool isActive, int totalLayers)
        {
            var border = new Border
            {
                Background      = isActive
                    ? new SolidColorBrush(Color.Parse("#1e3a2a"))
                    : new SolidColorBrush(Color.Parse("#252525")),
                BorderBrush     = isActive
                    ? new SolidColorBrush(Color.Parse("#4CAF50"))
                    : new SolidColorBrush(Color.Parse("#333")),
                BorderThickness = new Thickness(isActive ? 1 : 1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(8, 6),
                Cursor          = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Margin          = new Thickness(0, 0, 0, 0)
            };

            // Left accent stripe for active layer
            if (isActive)
            {
                border.BorderBrush = new SolidColorBrush(Color.Parse("#4CAF50"));
                border.BorderThickness = new Thickness(3, 1, 1, 1);
            }

            var mainGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto,Auto,Auto")
            };

            // 1. Thumbnail
            var thumb = CreateThumbnail(layer);
            Grid.SetColumn(thumb, 0);

            // 2. Name + visibility indicator
            var center = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };
            Grid.SetColumn(center, 1);

            center.Children.Add(new TextBlock
            {
                Text       = layer.Name,
                Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.Parse("#bbb")),
                FontSize   = 12,
                FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth   = 80
            });
            center.Children.Add(new TextBlock
            {
                Text       = $"{(int)(layer.Opacity * 100)}%  ·  {layer.Mode}",
                Foreground = new SolidColorBrush(Color.Parse("#666")),
                FontSize   = 10
            });

            border.PointerPressed += (_, _) =>
            {
                _canvas?.SetActiveLayer(layerIndex);
                RefreshLayersList();
            };

            // 3. Visibility toggle
            var visBtn = MakeSmallButton(layer.Visible ? "👁" : "◌", "#2a2a2a", "#999");
            ToolTip.SetTip(visBtn, "Toggle visibility");
            Grid.SetColumn(visBtn, 2);
            visBtn.Click += (_, e) =>
            {
                e.Handled      = true;
                layer.Visible  = !layer.Visible;
                _canvas?.TriggerRedraw();
                RefreshLayersList();
            };

            // 4. Move up
            var upBtn = MakeSmallButton("↑", "#2a2a2a", "#777");
            ToolTip.SetTip(upBtn, "Move layer up");
            upBtn.IsEnabled = layerIndex < totalLayers - 1;
            Grid.SetColumn(upBtn, 3);
            upBtn.Click += (_, e) => { e.Handled = true; MoveLayerUp(layerIndex); };

            // 5. Move down
            var downBtn = MakeSmallButton("↓", "#2a2a2a", "#777");
            ToolTip.SetTip(downBtn, "Move layer down");
            downBtn.IsEnabled = layerIndex > 0;
            Grid.SetColumn(downBtn, 4);
            downBtn.Click += (_, e) => { e.Handled = true; MoveLayerDown(layerIndex); };

            // 6. Rename
            var renameBtn = MakeSmallButton("✎", "#1a2a3e", "#64b5f6");
            ToolTip.SetTip(renameBtn, "Rename layer");
            Grid.SetColumn(renameBtn, 5);
            renameBtn.Click += async (_, e) =>
            {
                e.Handled = true;
                await ShowRenameDialog(layerIndex, layer.Name);
            };

            // 7. Delete
            var deleteBtn = MakeSmallButton("×", "#3a1a1a", "#f44336");
            ToolTip.SetTip(deleteBtn, "Delete layer");
            deleteBtn.IsEnabled = totalLayers > 1;
            Grid.SetColumn(deleteBtn, 6);
            deleteBtn.Click += (_, e) =>
            {
                e.Handled = true;
                _canvas?.DeleteLayer(layerIndex);
                RefreshLayersList();
            };

            mainGrid.Children.Add(thumb);
            mainGrid.Children.Add(center);
            mainGrid.Children.Add(visBtn);
            mainGrid.Children.Add(upBtn);
            mainGrid.Children.Add(downBtn);
            mainGrid.Children.Add(renameBtn);
            mainGrid.Children.Add(deleteBtn);

            border.Child = mainGrid;
            return border;
        }

        private static Button MakeSmallButton(string content, string bg, string fg) => new Button
        {
            Content                  = content,
            FontSize                 = 13,
            Width                    = 24,
            Height                   = 24,
            Background               = new SolidColorBrush(Color.Parse(bg)),
            Foreground               = new SolidColorBrush(Color.Parse(fg)),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding                  = new Thickness(0),
            Margin                   = new Thickness(2, 0, 0, 0),
            CornerRadius             = new CornerRadius(4),
            Cursor                   = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        // ── Thumbnail ─────────────────────────────────────────────────────

        private static Border CreateThumbnail(Layer layer)
        {
            // Checkerboard background for transparent layers
            var checkerBrush = CreateCheckerBrush();

            var thumbnailBorder = new Border
            {
                Width           = 36,
                Height          = 36,
                CornerRadius    = new CornerRadius(4),
                BorderBrush     = new SolidColorBrush(Color.Parse("#3a3a3a")),
                BorderThickness = new Thickness(1),
                Background      = checkerBrush,
                ClipToBounds    = true
            };

            try
            {
                const int thumbSize = 36;
                // ✅ Fixed: use Unpremul to match rest of the app
                var bitmap = new WriteableBitmap(
                    new PixelSize(thumbSize, thumbSize),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Unpremul);

                using (var fb = bitmap.Lock())
                {
                    unsafe
                    {
                        uint*  dest   = (uint*)fb.Address.ToPointer();
                        uint[] source = layer.GetPixels();

                        float scaleX = (float)layer.Width  / thumbSize;
                        float scaleY = (float)layer.Height / thumbSize;

                        for (int y = 0; y < thumbSize; y++)
                        for (int x = 0; x < thumbSize; x++)
                        {
                            int srcIdx = (int)(y * scaleY) * layer.Width + (int)(x * scaleX);
                            *dest++ = srcIdx < source.Length ? source[srcIdx] : 0;
                        }
                    }
                }

                thumbnailBorder.Background = new ImageBrush(bitmap) { Stretch = Stretch.Fill };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail error: {ex.Message}");
            }

            return thumbnailBorder;
        }

        private static DrawingBrush CreateCheckerBrush()
        {
            var light = new SolidColorBrush(Color.Parse("#555"));
            var dark  = new SolidColorBrush(Color.Parse("#333"));

            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing { Brush = dark,  Geometry = new RectangleGeometry(new Rect(0, 0, 8, 8)) });
            drawing.Children.Add(new GeometryDrawing { Brush = light, Geometry = new RectangleGeometry(new Rect(0, 0, 4, 4)) });
            drawing.Children.Add(new GeometryDrawing { Brush = light, Geometry = new RectangleGeometry(new Rect(4, 4, 4, 4)) });

            return new DrawingBrush
            {
                Drawing    = drawing,
                TileMode   = TileMode.Tile,
                SourceRect = new RelativeRect(0, 0, 8, 8, RelativeUnit.Absolute),
                DestinationRect = new RelativeRect(0, 0, 8, 8, RelativeUnit.Absolute)
            };
        }

        // ── Rename dialog ─────────────────────────────────────────────────

        private async System.Threading.Tasks.Task ShowRenameDialog(int layerIndex, string currentName)
        {
            var dialog = new Window
            {
                Title                   = "Rename Layer",
                Width                   = 320,
                Height                  = 140,
                WindowStartupLocation   = WindowStartupLocation.CenterOwner,
                CanResize               = false,
                Background              = new SolidColorBrush(Color.Parse("#252525")),
                FontFamily              = new FontFamily("Segoe UI, Inter, sans-serif")
            };

            var textBox = new TextBox
            {
                Text           = currentName,
                Watermark      = "Enter layer name",
                Background     = new SolidColorBrush(Color.Parse("#1e1e1e")),
                Foreground     = Brushes.White,
                BorderBrush    = new SolidColorBrush(Color.Parse("#444")),
                BorderThickness = new Thickness(1),
                CornerRadius   = new CornerRadius(5),
                Padding        = new Thickness(8, 6),
                Margin         = new Thickness(0, 0, 0, 12)
            };

            var okBtn = new Button
            {
                Content          = "Rename",
                Width            = 90,
                Background       = new SolidColorBrush(Color.Parse("#1e3a2a")),
                Foreground       = new SolidColorBrush(Color.Parse("#4CAF50")),
                BorderBrush      = new SolidColorBrush(Color.Parse("#4CAF50")),
                BorderThickness  = new Thickness(1),
                CornerRadius     = new CornerRadius(5),
                Padding          = new Thickness(0, 7)
            };

            var cancelBtn = new Button
            {
                Content          = "Cancel",
                Width            = 80,
                Background       = new SolidColorBrush(Color.Parse("#2a2a2a")),
                Foreground       = new SolidColorBrush(Color.Parse("#aaa")),
                BorderBrush      = new SolidColorBrush(Color.Parse("#444")),
                BorderThickness  = new Thickness(1),
                CornerRadius     = new CornerRadius(5),
                Padding          = new Thickness(0, 7),
                Margin           = new Thickness(8, 0, 0, 0)
            };

            okBtn.Click += (_, _) =>
            {
                var name = textBox.Text?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _canvas?.RenameLayer(layerIndex, name);
                    RefreshLayersList();
                }
                dialog.Close();
            };
            cancelBtn.Click += (_, _) => dialog.Close();
            textBox.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                    okBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else if (e.Key == Avalonia.Input.Key.Escape)
                    dialog.Close();
            };

            var btns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btns.Children.Add(okBtn);
            btns.Children.Add(cancelBtn);

            var panel = new StackPanel
            {
                Margin = new Thickness(20, 18)
            };
            panel.Children.Add(new TextBlock
            {
                Text = "Layer name",
                Foreground = new SolidColorBrush(Color.Parse("#888")),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 6)
            });
            panel.Children.Add(textBox);
            panel.Children.Add(btns);

            dialog.Content = panel;

            var window = this.GetVisualRoot() as Window;
            if (window != null)
                await dialog.ShowDialog(window);

            textBox.Focus();
            textBox.SelectAll();
        }
    }
}
