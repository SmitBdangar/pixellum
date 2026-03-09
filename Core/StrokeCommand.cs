using System;
using Pixellum.Core;

namespace Pixellum.Core
{
    /// <summary>
    /// Represents a single painting stroke that can be undone and redone
    /// using delta regions (dirty rectangle + pixel delta buffers).
    /// This minimizes memory usage compared to full layer snapshots.
    /// </summary>
    public class StrokeCommand : ICommand
    {
        private readonly Layer _targetLayer;
        private readonly IntRect _dirtyRect;
        private readonly uint[] _undoPixels;
        private readonly uint[] _redoPixels;
        private bool _isExecuted = false;

        public StrokeCommand(Layer layer, IntRect dirtyRect, uint[] beforePixels, uint[] afterPixels)
        {
            _targetLayer = layer ?? throw new ArgumentNullException(nameof(layer));
            _dirtyRect = dirtyRect;
            _undoPixels = beforePixels ?? throw new ArgumentNullException(nameof(beforePixels));
            _redoPixels = afterPixels ?? throw new ArgumentNullException(nameof(afterPixels));

            // ✅ Validate pixel buffer sizes
            int expectedSize = dirtyRect.Width * dirtyRect.Height;
            if (_undoPixels.Length != expectedSize || _redoPixels.Length != expectedSize)
            {
                throw new ArgumentException($"Pixel buffer size mismatch. Expected {expectedSize}, got undo:{_undoPixels.Length}, redo:{_redoPixels.Length}");
            }
        }

        // ICommand Implementation
        public void Execute() => Redo();

        public void Undo()
        {
            if (_isExecuted)
            {
                ApplyPixels(_undoPixels);
                _isExecuted = false;
                _targetLayer.MarkDirty(_dirtyRect);
            }
        }

        public void Redo()
        {
            if (!_isExecuted)
            {
                ApplyPixels(_redoPixels);
                _isExecuted = true;
                _targetLayer.MarkDirty(_dirtyRect);
            }
        }

        /// <summary>
        /// Copies the delta-region pixels into the layer buffer.
        /// Only affects the dirty rectangle.
        /// </summary>
        private void ApplyPixels(uint[] source)
        {
            if (_dirtyRect.IsEmpty)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Attempted to apply pixels to empty dirty rect");
                return;
            }

            uint[] target = _targetLayer.GetPixels();
            int sourceIndex = 0;

            // ✅ Validate bounds before applying
            if (_dirtyRect.X < 0 || _dirtyRect.Y < 0 ||
                _dirtyRect.X + _dirtyRect.Width > _targetLayer.Width ||
                _dirtyRect.Y + _dirtyRect.Height > _targetLayer.Height)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Invalid dirty rect bounds: {_dirtyRect}");
                return;
            }

            try
            {
                for (int y = _dirtyRect.Y; y < _dirtyRect.Y + _dirtyRect.Height; y++)
                {
                    int targetStart = y * _targetLayer.Width + _dirtyRect.X;
                    
                    // ✅ Additional safety check
                    if (targetStart + _dirtyRect.Width > target.Length)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Target index out of bounds at y={y}");
                        break;
                    }

                    if (sourceIndex + _dirtyRect.Width > source.Length)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Source index out of bounds at y={y}");
                        break;
                    }

                    Array.Copy(source, sourceIndex, target, targetStart, _dirtyRect.Width);
                    sourceIndex += _dirtyRect.Width;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ApplyPixels error: {ex.Message}");
                throw;
            }
        }
    }
}