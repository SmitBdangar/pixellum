using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
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
        private int _layerCounter = 1;

        public LayersPanel()
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
                        RefreshLayersList();
                    }
                }
            };
        }

        private void OnAddLayerClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;

            var newLayerName = $"Layer {_layerCounter++}";
            _canvas.AddLayer(newLayerName);
            RefreshLayersList();
        }

        private void OnDuplicateLayerClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;

            var layers = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();
            
            if (activeIndex < 0 || activeIndex >= layers.Count) return;

            var sourceLayer = layers[activeIndex];
            var newLayerName = $"{sourceLayer.Name} Copy";
            
            _canvas.AddLayer(newLayerName);
            
            // Copy pixels from source to new layer
            var newLayer = _canvas.GetLayers().Last();
            Array.Copy(sourceLayer.GetPixels(), newLayer.GetPixels(), sourceLayer.GetPixels().Length);
            
            RefreshLayersList();
        }

        private void OnMergeDownClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;

            var layers = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();

            if (layers.Count <= 1 || activeIndex <= 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Cannot merge: need at least 2 layers");
                return;
            }

            // Simple merge: copy active layer pixels onto layer below
            var activeLayer = layers[activeIndex];
            var belowLayer = layers[activeIndex - 1];
            
            var activePixels = activeLayer.GetPixels();
            var belowPixels = belowLayer.GetPixels();

            // Basic alpha blending
            for (int i = 0; i < activePixels.Length; i++)
            {
                uint src = activePixels[i];
                uint dst = belowPixels[i];
                
                float srcA = ((src >> 24) & 0xFF) / 255.0f;
                float invSrcA = 1.0f - srcA;

                byte r = (byte)(((src >> 16) & 0xFF) * srcA + ((dst >> 16) & 0xFF) * invSrcA);
                byte g = (byte)(((src >> 8) & 0xFF) * srcA + ((dst >> 8) & 0xFF) * invSrcA);
                byte b = (byte)((src & 0xFF) * srcA + (dst & 0xFF) * invSrcA);
                byte a = (byte)Math.Max((src >> 24) & 0xFF, (dst >> 24) & 0xFF);

                belowPixels[i] = (uint)((a << 24) | (r << 16) | (g << 8) | b);
            }
            _canvas.DeleteLayer(activeIndex);
            RefreshLayersList();
        }

        private void OnLayerOpacityChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_canvas == null) return;

            var slider = sender as Slider;
            if (slider == null) return;

            int opacity = (int)slider.Value;
            var opacityText = this.FindControl<TextBlock>("OpacityValueText");
            if (opacityText != null)
            {
                opacityText.Text = $"{opacity}%";
            }

            var layers = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();
            
            if (activeIndex >= 0 && activeIndex < layers.Count)
            {
                layers[activeIndex].Opacity = opacity / 100.0f;
                // Note: Canvas would need to call RedrawCanvas() to apply opacity
            }
        }

        private void RefreshLayersList()
        {
            if (_canvas == null) return;

            var stack = this.FindControl<StackPanel>("LayersStack");
            if (stack == null) return;

            stack.Children.Clear();

            var layers = _canvas.GetLayers();
            int activeIndex = _canvas.GetActiveLayerIndex();

            // Update active layer info
            var activeLayerName = this.FindControl<TextBlock>("ActiveLayerName");
            if (activeLayerName != null && activeIndex >= 0 && activeIndex < layers.Count)
            {
                activeLayerName.Text = $"Active: {layers[activeIndex].Name}";
            }

            // Update opacity slider
            var opacitySlider = this.FindControl<Slider>("LayerOpacitySlider");
            if (opacitySlider != null && activeIndex >= 0 && activeIndex < layers.Count)
            {
                opacitySlider.Value = layers[activeIndex].Opacity * 100;
            }

            // Add layer items in reverse order (top layer first)
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                var layer = layers[i];
                bool isActive = (i == activeIndex);

                var layerItem = CreateLayerItem(layer, i, isActive);
                stack.Children.Add(layerItem);
            }
        }

        private Border CreateLayerItem(Layer layer, int layerIndex, bool isActive)
        {
            var border = new Border
            {
                Background = isActive ? new SolidColorBrush(Color.Parse("#4CAF50")) : new SolidColorBrush(Color.Parse("#444")),
                BorderBrush = isActive ? Brushes.White : Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(2),
                CornerRadius = new Avalonia.CornerRadius(5),
                Padding = new Avalonia.Thickness(8),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };

            var mainGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto")
            };

            // 1. Thumbnail preview
            var thumbnail = CreateThumbnail(layer);
            Grid.SetColumn(thumbnail, 0);

            // 2. Layer name and visibility container
            var centerPanel = new StackPanel
            {
                Spacing = 3,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(8, 0, 8, 0)
            };
            Grid.SetColumn(centerPanel, 1);

            var nameText = new TextBlock
            {
                Text = layer.Name,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold
            };

            var visibilityInfo = new TextBlock
            {
                Text = layer.Visible ? "👁 Visible" : "⊘ Hidden",
                Foreground = new SolidColorBrush(Color.Parse("#AAA")),
                FontSize = 10
            };

            centerPanel.Children.Add(nameText);
            centerPanel.Children.Add(visibilityInfo);

            // Click to select layer
            border.PointerPressed += (_, __) =>
            {
                _canvas?.SetActiveLayer(layerIndex);
                RefreshLayersList();
            };

            // 3. Visibility toggle button
            var visibilityButton = new Button
            {
                Content = layer.Visible ? "👁" : "⊘",
                FontSize = 16,
                Width = 32,
                Height = 32,
                Background = new SolidColorBrush(Color.Parse("#555")),
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Avalonia.Thickness(0),
                Margin = new Avalonia.Thickness(3, 0, 3, 0),
                CornerRadius = new Avalonia.CornerRadius(3)
            };
            ToolTip.SetTip(visibilityButton, "Toggle visibility");
            Grid.SetColumn(visibilityButton, 2);

            visibilityButton.Click += (_, e) =>
            {
                e.Handled = true;
                layer.Visible = !layer.Visible;
                RefreshLayersList();
            };

            // 4. Rename button
            var renameButton = new Button
            {
                Content = "✎",
                FontSize = 16,
                Width = 32,
                Height = 32,
                Background = new SolidColorBrush(Color.Parse("#2196F3")),
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Avalonia.Thickness(0),
                Margin = new Avalonia.Thickness(3, 0, 3, 0),
                CornerRadius = new Avalonia.CornerRadius(3)
            };
            ToolTip.SetTip(renameButton, "Rename layer");
            Grid.SetColumn(renameButton, 3);

            renameButton.Click += async (_, e) =>
            {
                e.Handled = true;
                await ShowRenameDialog(layerIndex, layer.Name);
            };

            // 5. Delete button
            var deleteButton = new Button
            {
                Content = "×",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Width = 32,
                Height = 32,
                Background = new SolidColorBrush(Color.Parse("#d32f2f")),
                Foreground = Brushes.White,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Avalonia.Thickness(0),
                Margin = new Avalonia.Thickness(3, 0, 0, 0),
                CornerRadius = new Avalonia.CornerRadius(3)
            };
            ToolTip.SetTip(deleteButton, "Delete layer");
            Grid.SetColumn(deleteButton, 4);

            deleteButton.Click += (_, e) =>
            {
                e.Handled = true;
                if (_canvas != null && _canvas.GetLayers().Count > 1)
                {
                    _canvas.DeleteLayer(layerIndex);
                    RefreshLayersList();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Cannot delete the last layer");
                }
            };

            mainGrid.Children.Add(thumbnail);
            mainGrid.Children.Add(centerPanel);
            mainGrid.Children.Add(visibilityButton);
            mainGrid.Children.Add(renameButton);
            mainGrid.Children.Add(deleteButton);

            border.Child = mainGrid;

            return border;
        }

        private Border CreateThumbnail(Layer layer)
        {
            var thumbnailBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new Avalonia.CornerRadius(3),
                BorderBrush = Brushes.White,
                BorderThickness = new Avalonia.Thickness(1),
                Background = new SolidColorBrush(Color.Parse("#222")),
                ClipToBounds = true
            };

            // Create mini preview of layer
            try
            {
                int thumbSize = 40;
                var bitmap = new WriteableBitmap(
                    new PixelSize(thumbSize, thumbSize),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul
                );

                using (var fb = bitmap.Lock())
                {
                    unsafe
                    {
                        uint* dest = (uint*)fb.Address.ToPointer();
                        uint[] source = layer.GetPixels();

                        float scaleX = (float)layer.Width / thumbSize;
                        float scaleY = (float)layer.Height / thumbSize;

                        for (int y = 0; y < thumbSize; y++)
                        {
                            for (int x = 0; x < thumbSize; x++)
                            {
                                int srcX = (int)(x * scaleX);
                                int srcY = (int)(y * scaleY);
                                int srcIndex = srcY * layer.Width + srcX;

                                if (srcIndex < source.Length)
                                {
                                    *dest++ = source[srcIndex];
                                }
                            }
                        }
                    }
                }

                thumbnailBorder.Background = new ImageBrush(bitmap)
                {
                    Stretch = Stretch.Fill
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create thumbnail: {ex.Message}");
            }

            return thumbnailBorder;
        }

        private async System.Threading.Tasks.Task ShowRenameDialog(int layerIndex, string currentName)
        {
            var dialog = new Window
            {
                Title = "Rename Layer",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15
            };

            var textBox = new TextBox
            {
                Text = currentName,
                Watermark = "Enter layer name"
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Background = new SolidColorBrush(Color.Parse("#4CAF50")),
                Foreground = Brushes.White
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80
            };

            okButton.Click += (_, __) =>
            {
                string newName = textBox.Text?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    _canvas?.RenameLayer(layerIndex, newName);
                    RefreshLayersList();
                }
                dialog.Close();
            };

            cancelButton.Click += (_, __) => dialog.Close();

            textBox.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                {
                    okButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            panel.Children.Add(new TextBlock { Text = "Layer Name:", Foreground = Brushes.White });
            panel.Children.Add(textBox);
            panel.Children.Add(buttonPanel);

            dialog.Content = panel;

            var window = this.GetVisualRoot() as Window;
            if (window != null)
            {
                await dialog.ShowDialog(window);
            }

            textBox.Focus();
            textBox.SelectAll();
        }
    }
}
