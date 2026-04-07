using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;
using Pixellum.Core;

namespace Pixellum.Views
{
    public partial class AdjustmentsPanel : UserControl
    {
        private CanvasView? _canvas;

        public AdjustmentsPanel()
        {
            InitializeComponent();
            this.Loaded += (_, _) =>
            {
                var window = this.GetVisualRoot() as Window;
                _canvas = window?.GetVisualDescendants()
                                 .OfType<CanvasView>()
                                 .FirstOrDefault();
            };
        }

        private async void OpenAdjustment(AdjustmentType type)
        {
            if (_canvas == null) return;
            var window = this.GetVisualRoot() as Window;
            if (window == null) return;
            var dialog = new AdjustmentsDialog(type, _canvas);
            await dialog.ShowDialog(window);
        }

        private void OnBrightnessContrastClicked(object? sender, RoutedEventArgs e) =>
            OpenAdjustment(AdjustmentType.BrightnessContrast);

        private void OnLevelsClicked(object? sender, RoutedEventArgs e) =>
            OpenAdjustment(AdjustmentType.Levels);

        private void OnCurvesClicked(object? sender, RoutedEventArgs e) =>
            OpenAdjustment(AdjustmentType.Curves);

        private void OnHueSaturationClicked(object? sender, RoutedEventArgs e) =>
            OpenAdjustment(AdjustmentType.HueSaturation);

        private void OnColorBalanceClicked(object? sender, RoutedEventArgs e) =>
            OpenAdjustment(AdjustmentType.ColorBalance);

        private void OnVibranceClicked(object? sender, RoutedEventArgs e) =>
            OpenAdjustment(AdjustmentType.HueSaturation); // reuse HueSat for vibrance

        private void OnExposureClicked(object? sender, RoutedEventArgs e) =>
            OpenAdjustment(AdjustmentType.BrightnessContrast); // reuse for exposure

        private void OnInvertClicked(object? sender, RoutedEventArgs e)
        {
            if (_canvas == null) return;
            _canvas.SaveUndoState();
            var layers = _canvas.GetLayers();
            int idx = _canvas.GetActiveLayerIndex();
            if (idx < 0 || idx >= layers.Count) return;
            var pixels = layers[idx].GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                uint p = pixels[i];
                uint a = p & 0xFF000000u;
                uint r = (~p >> 16) & 0xFF;
                uint g = (~p >> 8) & 0xFF;
                uint b = (~p) & 0xFF;
                pixels[i] = a | (r << 16) | (g << 8) | b;
            }
            _canvas.TriggerRedraw();
        }
    }
}
