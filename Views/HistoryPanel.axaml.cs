using Avalonia.Controls;
using Pixellum.Core;

namespace Pixellum.Views
{
    public partial class HistoryPanel : UserControl
    {
        private CanvasView? _canvas;
        private bool _isUpdatingFromModel = false;

        public HistoryPanel()
        {
            InitializeComponent();
        }

        public void SetCanvas(CanvasView canvasView)
        {
            _canvas = canvasView;
            
            var listBox = this.FindControl<ListBox>("HistoryListBox");
            if (listBox != null)
            {
                listBox.ItemsSource = _canvas.History.Steps;
            }

            _canvas.History.HistoryChanged += (s, e) =>
            {
                _isUpdatingFromModel = true;
                if (listBox != null)
                {
                    listBox.SelectedIndex = _canvas.History.CurrentIndex;
                    if (_canvas.History.CurrentIndex >= 0 && listBox.SelectedItem != null)
                    {
                        listBox.ScrollIntoView(listBox.SelectedItem);
                    }
                }
                _isUpdatingFromModel = false;
            };
        }

        private void OnHistorySelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFromModel || _canvas == null) return;

            var listBox = sender as ListBox;
            if (listBox == null || listBox.SelectedIndex < 0) return;

            int tgtIndex = listBox.SelectedIndex;
            if (tgtIndex != _canvas.History.CurrentIndex)
            {
                _canvas.JumpToHistoryState(tgtIndex);
            }
        }
    }
}
