using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixellum.Core;
using System;

namespace Pixellum.Views
{
    public partial class TopOptionsBar : UserControl
    {
        private CanvasView? _canvas;

        public TopOptionsBar()
        {
            InitializeComponent();

            var sizeSlider = this.FindControl<Slider>("BrushSizeSlider");
            var opacSlider = this.FindControl<Slider>("OpacitySlider");
            var hardSlider = this.FindControl<Slider>("HardnessSlider");
            var flowSlider = this.FindControl<Slider>("FlowSlider");

            if (sizeSlider != null) sizeSlider.ValueChanged += (s, e) => { UpdateValues(); UpdateBrushSettings(); };
            if (opacSlider != null) opacSlider.ValueChanged += (s, e) => { UpdateValues(); UpdateBrushSettings(); };
            if (hardSlider != null) hardSlider.ValueChanged += (s, e) => { UpdateValues(); UpdateBrushSettings(); };
            if (flowSlider != null) flowSlider.ValueChanged += (s, e) => { UpdateValues(); UpdateBrushSettings(); };
        }

        public void SetCanvas(CanvasView canvasView)
        {
            _canvas = canvasView;
            _canvas.ToolChanged += (s, type) => OnToolChanged(type);
            OnToolChanged(_canvas.ActiveTool);
        }

        private void OnToolChanged(ToolType toolType)
        {
            var nameBlock = this.FindControl<TextBlock>("ActiveToolName");
            if (nameBlock != null) nameBlock.Text = toolType.ToString();

            var brushContainer = this.FindControl<StackPanel>("BrushOptionsContainer");
            if (brushContainer != null)
            {
                brushContainer.IsVisible = toolType == ToolType.Brush || toolType == ToolType.Eraser;
            }
        }

        private void UpdateValues()
        {
            var sizeBlock = this.FindControl<TextBlock>("BrushSizeValue");
            var sizeSlider = this.FindControl<Slider>("BrushSizeSlider");
            if (sizeBlock != null && sizeSlider != null)
                sizeBlock.Text = $"{sizeSlider.Value:F0}px";

            var opacBlock = this.FindControl<TextBlock>("OpacityValue");
            var opacSlider = this.FindControl<Slider>("OpacitySlider");
            if (opacBlock != null && opacSlider != null)
                opacBlock.Text = $"{(opacSlider.Value * 100):F0}%";

            var hardBlock = this.FindControl<TextBlock>("HardnessValue");
            var hardSlider = this.FindControl<Slider>("HardnessSlider");
            if (hardBlock != null && hardSlider != null)
                hardBlock.Text = $"{(hardSlider.Value * 100):F0}%";

            var flowBlock = this.FindControl<TextBlock>("FlowValue");
            var flowSlider = this.FindControl<Slider>("FlowSlider");
            if (flowBlock != null && flowSlider != null)
                flowBlock.Text = $"{(flowSlider.Value * 100):F0}%";
        }

        private void UpdateBrushSettings()
        {
            if (_canvas == null) return;
            
            var sizeSlider = this.FindControl<Slider>("BrushSizeSlider");
            var opacSlider = this.FindControl<Slider>("OpacitySlider");
            var hardSlider = this.FindControl<Slider>("HardnessSlider");
            var flowSlider = this.FindControl<Slider>("FlowSlider");

            if (sizeSlider != null) _canvas.BrushRadius = (float)sizeSlider.Value;
            if (opacSlider != null) _canvas.BrushOpacity = (float)opacSlider.Value;
            if (hardSlider != null && flowSlider != null)
                _canvas.SetBrushEngineParams((float)hardSlider.Value, (float)flowSlider.Value);
        }

        private void OnBrushPresetChanged(object? sender, SelectionChangedEventArgs e)
        {
            var combo = this.FindControl<ComboBox>("BrushPresetsComboBox");
            if (combo != null && combo.SelectedItem is ComboBoxItem item)
            {
                string preset = item.Content?.ToString() ?? "";
                
                var sizeSlider = this.FindControl<Slider>("BrushSizeSlider");
                var opacSlider = this.FindControl<Slider>("OpacitySlider");
                var hardSlider = this.FindControl<Slider>("HardnessSlider");
                var flowSlider = this.FindControl<Slider>("FlowSlider");

                if (sizeSlider == null || opacSlider == null || hardSlider == null || flowSlider == null) return;

                switch (preset)
                {
                    case "Standard Brush":
                        sizeSlider.Value = 15;
                        opacSlider.Value = 1.0;
                        hardSlider.Value = 1.0;
                        flowSlider.Value = 1.0;
                        break;
                    case "Soft Airbrush":
                        sizeSlider.Value = 40;
                        opacSlider.Value = 0.3;
                        hardSlider.Value = 0.0;
                        flowSlider.Value = 0.2;
                        break;
                    case "Inking Pen":
                        sizeSlider.Value = 5;
                        opacSlider.Value = 1.0;
                        hardSlider.Value = 1.0;
                        flowSlider.Value = 1.0;
                        break;
                }
            }
        }
    }
}
