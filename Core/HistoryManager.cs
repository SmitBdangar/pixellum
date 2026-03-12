using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Pixellum.Core
{
    public class LayerSnapshot
    {
        public string Name { get; set; } = "";
        public uint[] Pixels { get; set; } = Array.Empty<uint>();
        public float Opacity { get; set; }
        public BlendMode BlendMode { get; set; }
        public bool IsVisible { get; set; }
        public bool LockTransparency { get; set; }
        public bool LockPixels { get; set; }
        public bool IsClippingMask { get; set; }
    }

    public class HistoryStep
    {
        public string ActionName { get; set; } = "";
        public List<LayerSnapshot> Layers { get; set; } = new();
        public int ActiveLayerIndex { get; set; }
    }

    public class HistoryManager
    {
        private readonly int _maxHistory;
        
        public ObservableCollection<HistoryStep> Steps { get; } = new();
        
        private int _currentIndex = -1;
        public int CurrentIndex 
        { 
            get => _currentIndex; 
            private set
            {
                if (_currentIndex != value)
                {
                    _currentIndex = value;
                    HistoryChanged?.Invoke(this, EventArgs.Empty);
                }
            } 
        }

        public event EventHandler? HistoryChanged;

        public HistoryManager(int maxHistory = 50)
        {
            _maxHistory = maxHistory;
        }

        public void AddStep(string actionName, List<Layer> currentLayers, int activeLayerIndex)
        {
            // Truncate future steps if we are adding a new action after undoing
            if (CurrentIndex < Steps.Count - 1 && CurrentIndex >= 0)
            {
                while (Steps.Count > CurrentIndex + 1)
                {
                    Steps.RemoveAt(Steps.Count - 1);
                }
            }

            var step = new HistoryStep
            {
                ActionName = actionName,
                ActiveLayerIndex = activeLayerIndex
            };

            foreach (var layer in currentLayers)
            {
                var pixels = layer.GetPixels();
                var snapshot = new uint[pixels.Length];
                Array.Copy(pixels, snapshot, snapshot.Length);

                step.Layers.Add(new LayerSnapshot
                {
                    Name = layer.Name,
                    Pixels = snapshot,
                    Opacity = layer.Opacity,
                    BlendMode = layer.Mode,
                    IsVisible = layer.Visible,
                    LockTransparency = layer.LockTransparency,
                    LockPixels = layer.LockPixels,
                    IsClippingMask = layer.IsClippingMask
                });
            }

            Steps.Add(step);

            if (Steps.Count > _maxHistory)
            {
                Steps.RemoveAt(0);
            }
            else
            {
                CurrentIndex = Steps.Count - 1;
            }
        }

        public bool CanUndo => CurrentIndex > 0;
        public bool CanRedo => CurrentIndex < Steps.Count - 1;

        public HistoryStep? Undo()
        {
            if (!CanUndo) return null;
            CurrentIndex--;
            return Steps[CurrentIndex];
        }

        public HistoryStep? Redo()
        {
            if (!CanRedo) return null;
            CurrentIndex++;
            return Steps[CurrentIndex];
        }

        public HistoryStep? JumpTo(int index)
        {
            if (index >= 0 && index < Steps.Count)
            {
                CurrentIndex = index;
                return Steps[CurrentIndex];
            }
            return null;
        }

        public void Clear()
        {
            Steps.Clear();
            CurrentIndex = -1;
        }
    }
}