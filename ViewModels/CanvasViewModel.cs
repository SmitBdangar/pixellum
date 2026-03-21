using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Pixellum.Core;
using Pixellum.Views;
using Pixellum.Rendering;
using Avalonia.Media.Imaging;
using Avalonia;

namespace Pixellum.ViewModels
{
    public class CanvasViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private Document _document;
        private ObservableCollection<Layer> _layers = new();
        private int _activeLayerIndex = 0;
        private Pixellum.Core.ToolType _activeTool = Pixellum.Core.ToolType.Brush;
        private uint _activeColor = 0xFFFF0000;
        private float _brushRadius = 15f;
        private float _brushOpacity = 1f;
        private double _zoom = 1.0;
        public HistoryManager History { get; } = new HistoryManager(50);
        private BrushEngine _brushEngine = new();
        private bool _gridVisible = false;

        // Events (MVVM-friendly)
        public event EventHandler<uint>? ColorPicked;
        public event EventHandler<ToolType>? ToolChanged;
        public event EventHandler<double>? ZoomChanged;
        public event EventHandler? RequestColorSwap;

        public CanvasViewModel()
        {
            NewDocument(800, 600, 0);
        }

        public Document Document => _document!;
        public ReadOnlyObservableCollection<Layer> Layers => new ReadOnlyObservableCollection<Layer>(_layers);
        public Layer? ActiveLayer => _activeLayerIndex < _layers.Count ? _layers[_activeLayerIndex] : null;

        public Pixellum.Core.ToolType ActiveTool 
        { 
            get => _activeTool; 
            set 
            { 
                if (_activeTool != value) 
                { 
                    _activeTool = value; 
                    ToolChanged?.Invoke(this, value);
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
                OnPropertyChanged(); 
            } 
        }

        public float BrushRadius 
        { 
            get => _brushRadius; 
            set 
            { 
                _brushRadius = value; 
                OnPropertyChanged(); 
            } 
        }

        public float BrushOpacity 
        { 
            get => _brushOpacity; 
            set 
            { 
                _brushOpacity = value; 
                OnPropertyChanged(); 
            } 
        }

        public double Zoom 
        { 
            get => _zoom; 
            set 
            { 
                _zoom = value; 
                ZoomChanged?.Invoke(this, value);
                OnPropertyChanged(); 
            } 
        }

        public bool GridVisible 
        { 
            get => _gridVisible; 
            set 
            { 
                _gridVisible = value; 
                OnPropertyChanged(); 
            } 
        }

        public System.Windows.Input.ICommand NewDocumentCommand => new RelayCommand(_ => NewDocumentDialog());
        public System.Windows.Input.ICommand LoadImageCommand => new RelayCommand(_ => LoadImageDialog());
        public System.Windows.Input.ICommand UndoCommand => new RelayCommand(_ => Undo(), _ => History.CanUndo);
        public System.Windows.Input.ICommand RedoCommand => new RelayCommand(_ => Redo(), _ => History.CanRedo);

        public void SetActiveLayerIndex(int index)
        {
            if (_activeLayerIndex != index && index >= 0 && index < _layers.Count)
            {
                _activeLayerIndex = index;
                OnPropertyChanged(nameof(ActiveLayer));
            }
        }

        public void SetActiveTool(ToolType tool)
        {
            ActiveTool = tool;
        }

        public void SetActiveColor(uint color)
        {
            ActiveColor = color;
        }

        public void SetBrushRadius(float radius)
        {
            BrushRadius = radius;
        }

        public void SetBrushOpacity(float opacity)
        {
            BrushOpacity = opacity;
        }

        private void NewDocumentDialog()
        {
            // Impl dialog logic here or event
            NewDocument(800, 600, 0);
        }

        private void LoadImageDialog()
        {
            // Impl file picker
        }

        private void Undo()
        {
            var step = History.Undo();
            if (step != null)
            {
                // Restore layers from step
                RestoreHistoryStep(step);
            }
        }

        private void Redo()
        {
            var step = History.Redo();
            if (step != null)
            {
                RestoreHistoryStep(step);
            }
        }

        private void RestoreHistoryStep(HistoryStep step)
        {
            _layers.Clear();
            foreach (var snapshot in step.Layers)
            {
                var layer = new Layer(_document.Width, _document.Height, snapshot.Name);
                Array.Copy(snapshot.Pixels, layer.GetPixels(), snapshot.Pixels.Length);
                layer.Opacity = snapshot.Opacity;
                // Restore other props
                _layers.Add(layer);
            }
            SetActiveLayerIndex(step.ActiveLayerIndex);
        }

        private void NewDocument(int w, int h, uint bg)
        {
            _document = new Document(w, h);
            _layers.Clear();
            _layers.Add(new Layer(w, h, "Background"));
            _activeLayerIndex = 0;
            History.Clear();
            OnPropertyChanged(nameof(Document));
            OnPropertyChanged(nameof(Layers));
            OnPropertyChanged(nameof(ActiveLayer));
        }

        public WriteableBitmap? CanvasBitmap { get; private set; }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void InvalidateDirtyRect(IntRect rect) 
        { 
            // Trigger redraw
            OnPropertyChanged(nameof(CanvasBitmap));
        }
    }
}

