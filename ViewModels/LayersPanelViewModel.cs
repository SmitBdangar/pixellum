using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Pixellum.Core;
using Pixellum.ViewModels;

namespace Pixellum.ViewModels
{
    public class LayersPanelViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private ObservableCollection<Layer> _layers = new();
        private int _activeLayerIndex = 0;
        private CanvasViewModel? _canvasVM;

        public ObservableCollection<Layer> Layers
        {
            get => _layers;
            set
            {
                _layers = value;
                OnPropertyChanged();
            }
        }

        public int ActiveLayerIndex
        {
            get => _activeLayerIndex;
            set
            {
                if (_activeLayerIndex != value)
                {
                    _activeLayerIndex = value;
                    _canvasVM?.SetActiveLayerIndex(value);
                    OnPropertyChanged();
                }
            }
        }

        public System.Windows.Input.ICommand AddLayerCommand { get; }
        public System.Windows.Input.ICommand DeleteLayerCommand { get; }
        public System.Windows.Input.ICommand RenameLayerCommand { get; }

        public LayersPanelViewModel(CanvasViewModel canvasVM)
        {
            _canvasVM = canvasVM;
            AddLayerCommand = new RelayCommand(_ => AddLayer());
            DeleteLayerCommand = new RelayCommand(idx => { if (idx is int i) DeleteLayer(i); }, _ => CanDeleteLayer());
            RenameLayerCommand = new RelayCommand(data => { if (data is string[] d) RenameLayer(d); });
        }

        private void AddLayer()
        {
            if (_canvasVM?.Document == null) return;
            var newLayer = new Layer(_canvasVM.Document.Width, _canvasVM.Document.Height, $"Layer {Layers.Count + 1}");
            Layers.Add(newLayer);
            ActiveLayerIndex = Layers.Count - 1;
            _canvasVM.History.AddStep("Add Layer", Layers.ToList(), ActiveLayerIndex);
        }

        private bool CanDeleteLayer() => Layers.Count > 1 && ActiveLayerIndex >= 0;

        private void DeleteLayer(int index)
        {
            if (CanDeleteLayer() && index == ActiveLayerIndex)
            {
                Layers.RemoveAt(index);
                ActiveLayerIndex = System.Math.Max(0, index - 1);
                _canvasVM?.History.AddStep("Delete Layer", Layers.ToList(), ActiveLayerIndex);
            }
        }

        private void RenameLayer(string[] data)
        {
            if (data.Length == 2 && int.TryParse(data[0], out int idx) && idx >= 0 && idx < Layers.Count)
            {
                Layers[idx].Name = data[1];
                _canvasVM?.History.AddStep("Rename Layer", Layers.ToList(), ActiveLayerIndex);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

