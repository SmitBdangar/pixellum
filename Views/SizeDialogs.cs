using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Pixellum.Views
{
    // ── Canvas Size Dialog ────────────────────────────────────────────────────
    public class CanvasSizeDialog : Window
    {
        public int  NewWidth  { get; private set; }
        public int  NewHeight { get; private set; }
        public int  AnchorX   { get; private set; } = 0;
        public int  AnchorY   { get; private set; } = 0;
        public bool Confirmed { get; private set; } = false;

        private TextBox? _wBox, _hBox;

        public CanvasSizeDialog(int currentW, int currentH)
        {
            Title                 = "Canvas Size";
            Width                 = 360;
            CanResize             = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background            = new SolidColorBrush(Color.Parse("#1e1e1e"));
            FontFamily            = new Avalonia.Media.FontFamily("Segoe UI, Inter, sans-serif");
            SizeToContent         = SizeToContent.Height;

            _wBox = new TextBox { Text = currentW.ToString(), Background = new SolidColorBrush(Color.Parse("#2a2a2a")), Foreground = new SolidColorBrush(Color.Parse("#e0e0e0")), BorderBrush = new SolidColorBrush(Color.Parse("#444")), BorderThickness = new Avalonia.Thickness(1), CornerRadius = new Avalonia.CornerRadius(5), Padding = new Avalonia.Thickness(8,6) };
            _hBox = new TextBox { Text = currentH.ToString(), Background = new SolidColorBrush(Color.Parse("#2a2a2a")), Foreground = new SolidColorBrush(Color.Parse("#e0e0e0")), BorderBrush = new SolidColorBrush(Color.Parse("#444")), BorderThickness = new Avalonia.Thickness(1), CornerRadius = new Avalonia.CornerRadius(5), Padding = new Avalonia.Thickness(8,6) };

            var okBtn = MakeButton("OK",    "#1e3a2a", "#4CAF50", "#4CAF50");
            var caBtn = MakeButton("Cancel","#2a2a2a", "#aaa",    "#444");
            okBtn.Click += OnOk;
            caBtn.Click += (_, _) => { Confirmed = false; Close(); };

            var wRow = MakeRow("Width",  _wBox);
            var hRow = MakeRow("Height", _hBox);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
            btns.Children.Add(caBtn);
            btns.Children.Add(okBtn);

            var panel = new StackPanel { Margin = new Avalonia.Thickness(24, 20), Spacing = 14 };
            panel.Children.Add(new TextBlock { Text = "Canvas Size", Foreground = new SolidColorBrush(Color.Parse("#e0e0e0")), FontSize = 15, FontWeight = FontWeight.SemiBold });
            panel.Children.Add(wRow);
            panel.Children.Add(hRow);
            panel.Children.Add(btns);
            Content = panel;
        }

        private void OnOk(object? sender, RoutedEventArgs e)
        {
            if (!int.TryParse(_wBox?.Text, out int w) || w <= 0) w = 800;
            if (!int.TryParse(_hBox?.Text, out int h) || h <= 0) h = 600;
            NewWidth = System.Math.Clamp(w, 1, 16384);
            NewHeight = System.Math.Clamp(h, 1, 16384);
            Confirmed = true;
            Close();
        }

        private static Grid MakeRow(string label, Control ctrl)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("100,*"), Margin = new Avalonia.Thickness(0,0,0,2) };
            var l = new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.Parse("#999")), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(l, 0); Grid.SetColumn(ctrl, 1);
            g.Children.Add(l); g.Children.Add(ctrl);
            return g;
        }

        private static Button MakeButton(string text, string bg, string fg, string border) => new Button
        {
            Content = text, Background = new SolidColorBrush(Color.Parse(bg)),
            Foreground = new SolidColorBrush(Color.Parse(fg)),
            BorderBrush = new SolidColorBrush(Color.Parse(border)),
            BorderThickness = new Avalonia.Thickness(1), CornerRadius = new Avalonia.CornerRadius(6),
            Padding = new Avalonia.Thickness(18, 8), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
    }

    // ── Image Size Dialog ─────────────────────────────────────────────────────
    public class ImageSizeDialog : Window
    {
        public int  NewWidth  { get; private set; }
        public int  NewHeight { get; private set; }
        public bool Confirmed { get; private set; } = false;

        private TextBox? _wBox, _hBox;

        public ImageSizeDialog(int currentW, int currentH)
        {
            Title                 = "Image Size";
            Width                 = 360;
            CanResize             = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background            = new SolidColorBrush(Color.Parse("#1e1e1e"));
            FontFamily            = new Avalonia.Media.FontFamily("Segoe UI, Inter, sans-serif");
            SizeToContent         = SizeToContent.Height;

            _wBox = new TextBox { Text = currentW.ToString(), Background = new SolidColorBrush(Color.Parse("#2a2a2a")), Foreground = new SolidColorBrush(Color.Parse("#e0e0e0")), BorderBrush = new SolidColorBrush(Color.Parse("#444")), BorderThickness = new Avalonia.Thickness(1), CornerRadius = new Avalonia.CornerRadius(5), Padding = new Avalonia.Thickness(8,6) };
            _hBox = new TextBox { Text = currentH.ToString(), Background = new SolidColorBrush(Color.Parse("#2a2a2a")), Foreground = new SolidColorBrush(Color.Parse("#e0e0e0")), BorderBrush = new SolidColorBrush(Color.Parse("#444")), BorderThickness = new Avalonia.Thickness(1), CornerRadius = new Avalonia.CornerRadius(5), Padding = new Avalonia.Thickness(8,6) };

            var okBtn = MakeButton("OK",    "#1e3a2a", "#4CAF50", "#4CAF50");
            var caBtn = MakeButton("Cancel","#2a2a2a", "#aaa",    "#444");
            okBtn.Click += OnOk;
            caBtn.Click += (_, _) => { Confirmed = false; Close(); };

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
            btns.Children.Add(caBtn);
            btns.Children.Add(okBtn);

            var panel = new StackPanel { Margin = new Avalonia.Thickness(24, 20), Spacing = 14 };
            panel.Children.Add(new TextBlock { Text = "Image Size (Resampled)", Foreground = new SolidColorBrush(Color.Parse("#e0e0e0")), FontSize = 15, FontWeight = FontWeight.SemiBold });
            panel.Children.Add(new TextBlock { Text = "Pixels will be resampled using bilinear interpolation.", Foreground = new SolidColorBrush(Color.Parse("#777")), FontSize = 11, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(MakeRow("Width",  _wBox));
            panel.Children.Add(MakeRow("Height", _hBox));
            panel.Children.Add(btns);
            Content = panel;
        }

        private void OnOk(object? sender, RoutedEventArgs e)
        {
            if (!int.TryParse(_wBox?.Text, out int w) || w <= 0) w = 800;
            if (!int.TryParse(_hBox?.Text, out int h) || h <= 0) h = 600;
            NewWidth  = System.Math.Clamp(w, 1, 16384);
            NewHeight = System.Math.Clamp(h, 1, 16384);
            Confirmed = true;
            Close();
        }

        private static Grid MakeRow(string label, Control ctrl)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("100,*"), Margin = new Avalonia.Thickness(0,0,0,2) };
            var l = new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.Parse("#999")), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(l, 0); Grid.SetColumn(ctrl, 1);
            g.Children.Add(l); g.Children.Add(ctrl);
            return g;
        }

        private static Button MakeButton(string text, string bg, string fg, string border) => new Button
        {
            Content = text, Background = new SolidColorBrush(Color.Parse(bg)),
            Foreground = new SolidColorBrush(Color.Parse(fg)),
            BorderBrush = new SolidColorBrush(Color.Parse(border)),
            BorderThickness = new Avalonia.Thickness(1), CornerRadius = new Avalonia.CornerRadius(6),
            Padding = new Avalonia.Thickness(18, 8), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
    }
}
