using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Pixellum.Views
{
    public partial class NewDocumentDialog : Window
    {
        public int DocWidth  { get; private set; } = 800;
        public int DocHeight { get; private set; } = 600;
        public int Dpi       { get; private set; } = 300;

        /// <summary>
        /// 0 = Transparent, 1 = White, 2 = Black
        /// </summary>
        public int BackgroundChoice { get; private set; } = 0;

        public bool Confirmed { get; private set; } = false;

        public NewDocumentDialog()
        {
            InitializeComponent();
        }

        private void OnPresetChanged(object? sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo?.SelectedItem is not ComboBoxItem item) return;

            var wBox = this.FindControl<TextBox>("WidthBox");
            var hBox = this.FindControl<TextBox>("HeightBox");
            if (wBox == null || hBox == null) return;

            switch (combo.SelectedIndex)
            {
                case 1: wBox.Text = "800";  hBox.Text = "600";  break;
                case 2: wBox.Text = "1280"; hBox.Text = "720";  break;
                case 3: wBox.Text = "1920"; hBox.Text = "1080"; break;
                case 4: wBox.Text = "3840"; hBox.Text = "2160"; break;
                case 5: wBox.Text = "2480"; hBox.Text = "3508"; break;
                case 6: wBox.Text = "1080"; hBox.Text = "1080"; break;
            }
        }

        private void OnCreateClicked(object? sender, RoutedEventArgs e)
        {
            var wBox = this.FindControl<TextBox>("WidthBox");
            var hBox = this.FindControl<TextBox>("HeightBox");
            var dpiBox = this.FindControl<ComboBox>("DpiComboBox");
            var bgBox  = this.FindControl<ComboBox>("BgComboBox");

            if (!int.TryParse(wBox?.Text, out int w) || w <= 0)  w = 800;
            if (!int.TryParse(hBox?.Text, out int h) || h <= 0)  h = 600;

            // Cap at 16384 to avoid OOM
            DocWidth  = System.Math.Clamp(w, 1, 16384);
            DocHeight = System.Math.Clamp(h, 1, 16384);

            Dpi = (dpiBox?.SelectedIndex) switch
            {
                0 => 72,
                1 => 96,
                2 => 150,
                _ => 300
            };

            BackgroundChoice = bgBox?.SelectedIndex ?? 0;
            Confirmed = true;
            Close();
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
