using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Pixellum.Core;
using Pixellum.ViewModels;

namespace Pixellum.ViewModels
{
    public class ToolsPanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private CanvasViewModel? _canvasVM;
        private Pixellum.Core.ToolType _activeTool = Pixellum.Core.ToolType.Brush;
        private uint _activeColor = 0xFFFF0000u;
        private float _brushRadius = 15f;
        private float _brushOpacity = 1f;

        public Pixellum.Core.ToolType ActiveTool
        {
            get => _activeTool;
            set
            {
                if (_activeTool != value)
                {
                    _activeTool = value;
                    _canvasVM?.SetActiveTool(value);
                    OnPropertyChanged();
                }
            }
        }

        public uint ActiveColor
        {
            get => _activeColor;
            set
            {
                _activeColor = value;
                _canvasVM?.SetActiveColor(value);
                OnPropertyChanged();
            }
        }

        public float BrushRadius
        {
            get => _brushRadius;
            set { _brushRadius = value; _canvasVM?.SetBrushRadius(value); OnPropertyChanged(); }
        }

        public float BrushOpacity
        {
            get => _brushOpacity;
            set { _brushOpacity = value; _canvasVM?.SetBrushOpacity(value); OnPropertyChanged(); }
        }

        public System.Windows.Input.ICommand BrushCommand { get; }
        public System.Windows.Input.ICommand EraserCommand { get; }
        // Add more tool commands...

        public ToolsPanelViewModel(CanvasViewModel canvasVM)
        {
            _canvasVM = canvasVM;
            BrushCommand = new RelayCommand(_ => ActiveTool = Pixellum.Core.ToolType.Brush);
            EraserCommand = new RelayCommand(_ => ActiveTool = Pixellum.Core.ToolType.Eraser);
            // Initialize other tools
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

