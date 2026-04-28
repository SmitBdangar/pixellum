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
using Avalonia.Controls.Shapes;
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

        public void OnAddLayerClickedPublic() =>
            OnAddLayerClicked(null, new RoutedEventArgs());

        private void OnAddFillLayerClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;
            // Use the currently selected foreground color from the brush/tools
            uint color = _canvas.ActiveColor | 0xFF000000; // Force opaque fill
            _canvas.AddSolidColorLayer($"Color Fill {_layerCounter++}", color);
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

        public void OnDuplicateLayerClickedPublic() =>
            OnDuplicateLayerClicked(null, new RoutedEventArgs());

        public void OnMergeDownClickedPublic() =>
            OnMergeDownClicked(null, new RoutedEventArgs());

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

            // Merge using shared alpha compositing
            for (int i = 0; i < srcPixels.Length; i++)
            {
                dstPixels[i] = ColorMath.AlphaComposite(srcPixels[i], dstPixels[i]);
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

            layers[activeIndex].Mode = ColorMath.ParseBlendMode(item.Content?.ToString());

            // Force the entire layer dirty so it re-composites with the new blend mode
            layers[activeIndex].MarkDirty(0, 0, layers[activeIndex].Width, layers[activeIndex].Height);
            _canvas.TriggerRedraw();
        }

        private void OnLockToggled(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;
            var layers = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();
            if (activeIndex < 0 || activeIndex >= layers.Count) return;

            var activeLayer = layers[activeIndex];

            var toggle = sender as ToggleButton;
            if (toggle == null) return;

            if (toggle.Name == "LockTransToggle")
                activeLayer.LockTransparency = toggle.IsChecked ?? false;
            else if (toggle.Name == "LockPixToggle")
                activeLayer.LockPixels = toggle.IsChecked ?? false;
            else if (toggle.Name == "LockPosToggle")
                activeLayer.LockPosition = toggle.IsChecked ?? false;
            else if (toggle.Name == "ClipMaskToggle")
                activeLayer.IsClippingMask = toggle.IsChecked ?? false;

            _canvas.TriggerRedraw();
            RefreshLayersList(); // Refresh list to show clipping mask visual indicator
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

            // Sync main-window blend combo + opacity label
            var window = this.GetVisualRoot() as Window;
            if (window != null && activeIndex >= 0 && activeIndex < layers.Count)
            {
                var layer = layers[activeIndex];

                var blendCombo = window.FindControl<ComboBox>("BlendModeComboBox");
                if (blendCombo != null)
                {
                    string modeName = layer.Mode.ToString();
                    for (int ci = 0; ci < blendCombo.Items.Count; ci++)
                    {
                        if (blendCombo.Items[ci] is ComboBoxItem cbi &&
                            cbi.Content?.ToString() == modeName)
                        { blendCombo.SelectedIndex = ci; break; }
                    }
                }

                var opacityLabel = window.FindControl<TextBlock>("LayerOpacityLabel");
                if (opacityLabel != null)
                    opacityLabel.Text = $"{(int)(layer.Opacity * 100)}%";
            }

            // Build layer rows top-to-bottom (highest index first = top of stack)
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                stack.Children.Add(CreateLayerItem(layers[i], i, i == activeIndex, layers.Count));
            }
        }

        private Border CreateLayerItem(Layer layer, int layerIndex, bool isActive, int totalLayers)
        {
            // Row border — highlighted when active
            var border = new Border
            {
                Background      = isActive
                    ? new SolidColorBrush(Color.Parse("#2d2d2d")) // Slightly lighter than panel
                    : Brushes.Transparent,
                BorderBrush     = new SolidColorBrush(Color.Parse("#1a1a1a")),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(4, 0, 8, 0),
                Cursor          = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Height          = 48
            };

            if (isActive)
            {
                // Add a left accent bar for the active layer
                border.BorderThickness = new Thickness(3, 0, 0, 1);
                border.BorderBrush = new SolidColorBrush(Color.Parse("#007acc"));
            }

            // Main row grid: eye | thumb | [name + info] | type-badge | rename | delete
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("32,44,*,Auto,Auto,Auto")
            };

            // ── Eye (visibility toggle) ─────────────────────────────
            var eyeIcon = new Path
            {
                Data = layer.Visible 
                    ? Geometry.Parse("M12,4.5C7,4.5 2.73,7.61 1,12c1.73,4.39 6,7.5 11,7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12,17c-2.76,0-5-2.24-5-5s2.24-5 5-5 5,2.24 5,5-2.24,5-5,5zm0-8c-1.66,0-3,1.34-3,3s1.34,3 3,3 3-1.34,3-3-1.34-3-3-3z")
                    : Geometry.Parse("M12,7c2.76,0,5,2.24,5,5 0,0.65-0.13,1.26-0.36,1.82l2.92,2.92c1.51-1.39,2.72-3.13,3.44-5.04-1.73-4.39-6-7.5-11-7.5-1.4,0-2.74,0.25-3.98,0.7l2.16,2.16C10.74,7.13,11.35,7,12,7zM2,4.27l2.28,2.28.46.46C3.08,8.3 1.78,10.03 1,12c1.73,4.39,6,7.5,11,7.5 1.55,0,3.03-0.3,4.38-0.84l0.42,0.42L19.73,22 21,20.73 3.27,3 2,4.27zM7.53,9.8l1.55,1.55c-0.05,0.21-0.08,0.43-0.08,0.65 0,1.66,1.34,3,3,3 0.22,0,0.44-0.03,0.65-0.08l1.55,1.55c-0.67,0.33-1.41,0.53-2.2,0.53-2.76,0-5-2.24-5-5 0-0.79,0.2-1.53,0.53-2.2zM11.84,9.02l3.15,3.15c0.01-0.06,0.01-0.12,0.01-0.17 0-1.66-1.34-3-3-3-0.05,0-0.11,0-0.16,0.02z"),
                Fill = layer.Visible 
                    ? new SolidColorBrush(Color.Parse("#aaaaaa"))
                    : new SolidColorBrush(Color.Parse("#555555")),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16
            };

            var eyeBtn = new Button
            {
                Content = eyeIcon,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Width = 32,
                Height = 32,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            ToolTip.SetTip(eyeBtn, "Toggle visibility");
            Grid.SetColumn(eyeBtn, 0);
            eyeBtn.Click += (_, e) =>
            {
                e.Handled = true;
                layer.Visible = !layer.Visible;
                _canvas?.TriggerRedraw();
                RefreshLayersList();
            };

            // ── Thumbnail ───────────────────────────────────────────
            var thumb = CreateThumbnail(layer);
            thumb.Width = 36;
            thumb.Height = 36;
            thumb.Margin = new Thickness(4, 0, 8, 0);
            thumb.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(thumb, 1);

            // ── Name + sub-info ─────────────────────────────────────
            var nameStack = new StackPanel
            {
                Spacing = 0,
                VerticalAlignment = VerticalAlignment.Center
            };
            nameStack.Children.Add(new TextBlock
            {
                Text         = layer.Name,
                Foreground   = isActive ? Brushes.White : new SolidColorBrush(Color.Parse("#cccccc")),
                FontSize     = 11.5,
                FontWeight   = isActive ? FontWeight.Bold : FontWeight.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            nameStack.Children.Add(new TextBlock
            {
                Text       = layer.Mode == BlendMode.Normal
                    ? $"{(int)(layer.Opacity * 100)}% Opacity"
                    : $"{layer.Mode} · {(int)(layer.Opacity * 100)}%",
                Foreground = new SolidColorBrush(Color.Parse(isActive ? "#808080" : "#666666")),
                FontSize   = 9.5
            });
            Grid.SetColumn(nameStack, 2);

            // ── Type badge ──────────────────────────────────────────
            var typeBadge = new TextBlock
            {
                Text              = GetLayerTypeBadge(layer),
                Foreground        = new SolidColorBrush(Color.Parse("#888")),
                FontSize          = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(4, 0)
            };
            Grid.SetColumn(typeBadge, 3);

            // ── Rename ──────────────────────────────────────────────
            var renameBtn = MakeSmallButton("M3,17.25V21h3.75L17.81,9.94l-3.75-3.75L3,17.25z M20.71,7.04c0.39-0.39,0.39-1.02,0-1.41l-2.34-2.34c-0.39-0.39-1.02-0.39-1.41,0l-1.83,1.83l3.75,3.75L20.71,7.04z", "Transparent", "#888");
            ToolTip.SetTip(renameBtn, "Rename");
            Grid.SetColumn(renameBtn, 4);
            renameBtn.Click += async (_, e) =>
            {
                e.Handled = true;
                await ShowRenameDialog(layerIndex, layer.Name);
            };

            // ── Delete ──────────────────────────────────────────────
            var deleteBtn = MakeSmallButton("M6,19c0,1.1,0.9,2,2,2h8c1.1,0,2-0.9,2-2V7H6V19z M19,4h-3.5l-1-1h-5l-1,1H5v2h14V4z", "Transparent", "#e05555");
            ToolTip.SetTip(deleteBtn, "Delete Layer");
            deleteBtn.IsEnabled = totalLayers > 1;
            Grid.SetColumn(deleteBtn, 5);
            deleteBtn.Click += (_, e) =>
            {
                e.Handled = true;
                _canvas?.DeleteLayer(layerIndex);
                RefreshLayersList();
            };

            // Click to activate
            border.PointerPressed += (_, _) =>
            {
                _canvas?.SetActiveLayer(layerIndex);
                RefreshLayersList();
            };

            row.Children.Add(eyeBtn);
            row.Children.Add(thumb);
            row.Children.Add(nameStack);
            row.Children.Add(typeBadge);
            row.Children.Add(renameBtn);
            row.Children.Add(deleteBtn);

            border.Child = row;
            return border;
        }

        private static string GetLayerTypeBadge(Layer layer)
        {
            if (layer.IsClippingMask) return "CLIP";
            if (layer.LockPosition)   return "LOCK";
            return "";
        }

        private static Button MakeSmallButton(string pathData, string bg, string fg) => new Button
        {
            Content = new Path
            {
                Data = Geometry.Parse(pathData),
                Fill = new SolidColorBrush(Color.Parse(fg)),
                Stretch = Stretch.Uniform,
                Width = 12,
                Height = 12
            },
            Width = 28,
            Height = 28,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 0),
            CornerRadius = new CornerRadius(4),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
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
