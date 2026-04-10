using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Pixellum.Core;

namespace Pixellum.Views
{
    public enum AdjustmentType
    {
        BrightnessContrast,
        HueSaturation,
        Levels,
        Curves,
        ColorBalance
    }

    public partial class AdjustmentsDialog : Window
    {
        private readonly AdjustmentType _type;
        private readonly CanvasView     _canvas = null!;
        private uint[]?  _snapshot;   // pixel state before any adjustment (for cancel/reset)

        // Slider references (filled in BuildSliders)
        private Slider?  _s1, _s2, _s3, _s4, _s5;
#pragma warning disable CS0169, CS0649 // Reserved for future adjustment types
        private Slider?  _s6;
#pragma warning restore CS0169, CS0649
        private TextBlock? _v1, _v2, _v3, _v4, _v5;
#pragma warning disable CS0169
        private TextBlock? _v6;
#pragma warning restore CS0169
        private CheckBox? _previewCheck;

        public AdjustmentsDialog()
        {
            InitializeComponent();
        }

        public AdjustmentsDialog(AdjustmentType type, CanvasView canvas)
        {
            _type   = type;
            _canvas = canvas;
            InitializeComponent();

            // Take a before-snapshot so we can cancel
            _snapshot = TakeSnapshot();

            this.Loaded += (_, _) =>
            {
                var titleLabel = this.FindControl<TextBlock>("TitleLabel");
                if (titleLabel != null)
                    titleLabel.Text = TypeToTitle(type);

                _previewCheck = this.FindControl<CheckBox>("PreviewCheck");

                var panel = this.FindControl<StackPanel>("SlidersPanel");
                if (panel != null) BuildSliders(panel, type);
            };
        }

        // ── Slider construction ───────────────────────────────────────────────

        private void BuildSliders(StackPanel panel, AdjustmentType type)
        {
            switch (type)
            {
                case AdjustmentType.BrightnessContrast:
                    (_s1, _v1) = AddSlider(panel, "Brightness", -100, 100, 0);
                    (_s2, _v2) = AddSlider(panel, "Contrast",   -100, 100, 0);
                    break;

                case AdjustmentType.HueSaturation:
                    (_s1, _v1) = AddSlider(panel, "Hue",        -180, 180, 0);
                    (_s2, _v2) = AddSlider(panel, "Saturation", -100, 100, 0);
                    (_s3, _v3) = AddSlider(panel, "Lightness",  -100, 100, 0);
                    break;

                case AdjustmentType.Levels:
                    (_s1, _v1) = AddSlider(panel, "Input Black",  0,   255, 0);
                    (_s2, _v2) = AddSlider(panel, "Input White",  0,   255, 255);
                    (_s3, _v3) = AddSlider(panel, "Gamma",        10,  1000, 100, "/10");
                    (_s4, _v4) = AddSlider(panel, "Output Black", 0,   255, 0);
                    (_s5, _v5) = AddSlider(panel, "Output White", 0,   255, 255);
                    break;

                case AdjustmentType.Curves:
                    // Simplified: per-channel offset (full spline curves is a later enhancement)
                    AddLabel(panel, "Simplified curve offsets (per channel):");
                    (_s1, _v1) = AddSlider(panel, "Red offset",   -128, 128, 0);
                    (_s2, _v2) = AddSlider(panel, "Green offset", -128, 128, 0);
                    (_s3, _v3) = AddSlider(panel, "Blue offset",  -128, 128, 0);
                    break;

                case AdjustmentType.ColorBalance:
                    AddLabel(panel, "Midtone balance:");
                    (_s1, _v1) = AddSlider(panel, "Cyan ↔ Red",     -100, 100, 0);
                    (_s2, _v2) = AddSlider(panel, "Magenta ↔ Green",-100, 100, 0);
                    (_s3, _v3) = AddSlider(panel, "Yellow ↔ Blue",  -100, 100, 0);
                    break;
            }
        }

        private static void AddLabel(StackPanel panel, string text)
        {
            panel.Children.Add(new TextBlock
            {
                Text       = text,
                Foreground = new SolidColorBrush(Color.Parse("#777")),
                FontSize   = 11,
                Margin     = new Thickness(0, 4, 0, 0)
            });
        }

        private (Slider slider, TextBlock value) AddSlider(
            StackPanel panel, string label, double min, double max, double initial,
            string suffix = "")
        {
            var valueBlock = new TextBlock
            {
                Text       = FormatValue(initial, suffix),
                Foreground = new SolidColorBrush(Color.Parse("#e0e0e0")),
                FontSize   = 11,
                MinWidth   = 48,
                TextAlignment = Avalonia.Media.TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value   = initial,
                Foreground = new SolidColorBrush(Color.Parse("#4CAF50"))
            };

            slider.ValueChanged += (_, e) =>
            {
                valueBlock.Text = FormatValue(e.NewValue, suffix);
                if (_previewCheck?.IsChecked == true)
                    ApplyPreview();
            };

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("120,*,56")
            };

            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.Parse("#999")),
                FontSize   = 11,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(labelBlock,  0);
            Grid.SetColumn(slider,      1);
            Grid.SetColumn(valueBlock,  2);

            row.Children.Add(labelBlock);
            row.Children.Add(slider);
            row.Children.Add(valueBlock);
            panel.Children.Add(row);

            return (slider, valueBlock);
        }

        private static string FormatValue(double v, string suffix) =>
            suffix == "/10"
                ? $"{v / 10:F1}"
                : $"{(int)v}{suffix}";

        // ── Adjustment application ────────────────────────────────────────────

        private void ApplyPreview()
        {
            // Restore snapshot then apply current values so preview is live / non-cumulative
            RestoreSnapshot();
            ApplyCurrentValues();
            _canvas.TriggerRedraw();
        }

        private void ApplyCurrentValues()
        {
            var layer = GetActiveLayer();
            if (layer == null) return;
            var px = layer.GetPixels();

            switch (_type)
            {
                case AdjustmentType.BrightnessContrast:
                    Adjustments.BrightnessContrast(px,
                        (float)(_s1?.Value ?? 0) / 100f,
                        (float)(_s2?.Value ?? 0) / 100f);
                    break;

                case AdjustmentType.HueSaturation:
                    Adjustments.HueSaturation(px,
                        (float)(_s1?.Value ?? 0),
                        (float)(_s2?.Value ?? 0) / 100f,
                        (float)(_s3?.Value ?? 0) / 100f);
                    break;

                case AdjustmentType.Levels:
                    Adjustments.Levels(px,
                        (int)(_s1?.Value ?? 0),
                        (int)(_s2?.Value ?? 255),
                        (float)(_s3?.Value ?? 100) / 100f,
                        (int)(_s4?.Value ?? 0),
                        (int)(_s5?.Value ?? 255));
                    break;

                case AdjustmentType.Curves:
                {
                    float rd = (float)(_s1?.Value ?? 0) / 128f;
                    float gd = (float)(_s2?.Value ?? 0) / 128f;
                    float bd = (float)(_s3?.Value ?? 0) / 128f;
                    var rMap = BuildOffsetLut(rd);
                    var gMap = BuildOffsetLut(gd);
                    var bMap = BuildOffsetLut(bd);
                    Adjustments.Curves(px, rMap, gMap, bMap);
                    break;
                }

                case AdjustmentType.ColorBalance:
                {
                    float cr = (float)(_s1?.Value ?? 0) / 100f;
                    float mg = (float)(_s2?.Value ?? 0) / 100f;
                    float yb = (float)(_s3?.Value ?? 0) / 100f;
                    Adjustments.ColorBalance(px,
                        0, 0, 0,
                        cr, mg, yb,
                        0, 0, 0);
                    break;
                }
            }
        }

        private static byte[] BuildOffsetLut(float offset)
        {
            var lut = new byte[256];
            for (int i = 0; i < 256; i++)
                lut[i] = (byte)Math.Clamp(i + (int)(offset * 128), 0, 255);
            return lut;
        }

        // ── Snapshot helpers ──────────────────────────────────────────────────

        private Layer? GetActiveLayer()
        {
            var layers = _canvas.GetLayers();
            int idx    = _canvas.GetActiveLayerIndex();
            if (idx < 0 || idx >= layers.Count) return null;
            return layers[idx];
        }

        private uint[]? TakeSnapshot()
        {
            var layer = GetActiveLayer();
            if (layer == null) return null;
            var snap = new uint[layer.GetPixels().Length];
            Array.Copy(layer.GetPixels(), snap, snap.Length);
            return snap;
        }

        private void RestoreSnapshot()
        {
            var layer = GetActiveLayer();
            if (layer == null || _snapshot == null) return;
            Array.Copy(_snapshot, layer.GetPixels(), _snapshot.Length);
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void OnPreviewToggled(object? sender, RoutedEventArgs e)
        {
            if (_previewCheck?.IsChecked == true)
                ApplyPreview();
            else
            {
                RestoreSnapshot();
                _canvas.TriggerRedraw();
            }
        }

        private void OnResetClicked(object? sender, RoutedEventArgs e)
        {
            // Reset all sliders to their default (zero / neutral)
            var sliders = new[] { _s1, _s2, _s3, _s4, _s5, _s6 };
            foreach (var s in sliders)
                if (s != null) s.Value = IsLevels(s) ? GetLevelDefault(s) : 0;
        }

        private bool IsLevels(Slider s) => _type == AdjustmentType.Levels;
        private double GetLevelDefault(Slider s)
        {
            if (s == _s2 || s == _s5) return 255;
            if (s == _s3) return 100;
            return 0;
        }

        private void OnOkClicked(object? sender, RoutedEventArgs e)
        {
            // Restore snapshot so Undo can capture the before state
            RestoreSnapshot();
            _canvas.SaveUndoState();

            // Then apply values permanently
            ApplyCurrentValues();
            _canvas.TriggerRedraw();
            Close();
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            RestoreSnapshot();
            _canvas.TriggerRedraw();
            Close();
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private static string TypeToTitle(AdjustmentType t) => t switch
        {
            AdjustmentType.BrightnessContrast => "Brightness / Contrast",
            AdjustmentType.HueSaturation      => "Hue / Saturation",
            AdjustmentType.Levels             => "Levels",
            AdjustmentType.Curves             => "Curves",
            AdjustmentType.ColorBalance       => "Color Balance",
            _                                 => "Adjustments"
        };
    }
}
