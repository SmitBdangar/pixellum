using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Pixellum.Controls
{
    public partial class ColorWheel : UserControl
    {
        private bool _isDragging;
        private bool _showPreviewCircle;

        public bool ShowBrushPreview
        {
            get => _showPreviewCircle;
            set
            {
                _showPreviewCircle = value;
                InvalidateVisual();
            }
        }

        public double PreviewBrushRadius { get; set; } = 10;

        private Color _activeColor = Colors.White;
        public Color ActiveColor
        {
            get => _activeColor;
            set
            {
                _activeColor = value;
                InvalidateVisual();
                ActiveColorChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<Color>? ActiveColorChanged;

        public ColorWheel()
        {
            InitializeComponent();
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double radius = Math.Min(Bounds.Width, Bounds.Height) / 2;
            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);

            for (int angle = 0; angle < 360; angle++)
            {
                var color = HsvToColor(angle, 1, 1);
                var brush = new SolidColorBrush(color);

                double rad1 = radius * 0.20; // inner empty circle
                double rad2 = radius;        // outer circle radius
                double rad = angle * Math.PI / 180;

                var p1 = center + new Vector(Math.Cos(rad), Math.Sin(rad)) * rad1;
                var p2 = center + new Vector(Math.Cos(rad), Math.Sin(rad)) * rad2;

                context.DrawLine(new Pen(brush, 10), p1, p2);
            }


            if (ShowBrushPreview)
            {
                var previewBrush = new SolidColorBrush(ActiveColor);
                context.DrawEllipse(previewBrush, new Pen(Brushes.White, 2),
                    center, PreviewBrushRadius, PreviewBrushRadius);
            }
        }



        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            _isDragging = true;
            ShowBrushPreview = true;
            UpdateColorFromPointer(e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (_isDragging)
                UpdateColorFromPointer(e.GetPosition(this));
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            _isDragging = false;
            ShowBrushPreview = false;
        }

        private void UpdateColorFromPointer(Point pos)
        {
            var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
            var vector = pos - center;

            double angle = Math.Atan2(vector.Y, vector.X) * (180 / Math.PI);
            if (angle < 0) angle += 360;

            ActiveColor = HsvToColor((float)angle, 1, 1);
        }

        public static uint HsvToArgb(float h, float s, float v, float a = 1.0f)
        {
            h = h % 360f;
            if (h < 0) h += 360f;
            s = Math.Clamp(s, 0f, 1f);
            v = Math.Clamp(v, 0f, 1f);
            a = Math.Clamp(a, 0f, 1f);

            float c = v * s;
            float x = c * (1 - Math.Abs((h / 60f) % 2 - 1));
            float m = v - c;

            float r, g, b;

            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return ((uint)(a * 255) << 24)
                 | (uint)((r + m) * 255) << 16
                 | (uint)((g + m) * 255) << 8
                 | (uint)((b + m) * 255);
        }

        public static Color HsvToColor(float h, float s, float v, float a = 1.0f)
            => ArgbToColor(HsvToArgb(h, s, v, a));

        public static Color ArgbToColor(uint argb)
        {
            return Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }
    }
}
