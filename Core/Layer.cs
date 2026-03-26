using System;

namespace Pixellum.Core
{
    public enum BlendMode
    {
        // Basic
        Normal,
        // Darken group
        Darken,
        Multiply,
        ColorBurn,
        // Lighten group
        Lighten,
        Screen,
        ColorDodge,
        // Contrast group
        Overlay,
        SoftLight,
        HardLight,
        // Inversion group
        Difference,
        Exclusion,
        // Component group
        Hue,
        Saturation,
        Color,
        Luminosity,
    }

    public class Layer
    {
        public string Name { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;

        private float _opacity = 1.0f;
        public float Opacity
        {
            get => _opacity;
            set => _opacity = Math.Clamp(value, 0.0f, 1.0f);
        }

        public BlendMode Mode { get; set; } = BlendMode.Normal;

        public bool LockTransparency { get; set; } = false;
        public bool LockPixels { get; set; } = false;
        public bool LockPosition { get; set; } = false;
        
        public bool IsClippingMask { get; set; } = false;

        public int Width { get; }
        public int Height { get; }
        private readonly uint[] _pixels;

        public IntRect DirtyRegion { get; private set; } = default;

        public Layer(int width, int height, string name = "Layer")
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Width and Height must be positive.");

            Width   = width;
            Height  = height;
            Name    = name;
            _pixels = new uint[Width * Height];
            DirtyRegion = new IntRect(0, 0, width, height); // Initial full dirty
        }

        public void TranslatePixels(int dx, int dy)
        {
            var copy = (uint[])_pixels.Clone();
            Array.Clear(_pixels);
            for (int y = 0; y < Height; y++)
            {
                int srcY = y - dy;
                if (srcY < 0 || srcY >= Height) continue;
                for (int x = 0; x < Width; x++)
                {
                    int srcX = x - dx;
                    if (srcX >= 0 && srcX < Width)
                        _pixels[y * Width + x] = copy[srcY * Width + srcX];
                }
            }

            // Pixel translation affects the whole layer.
            DirtyRegion = new IntRect(0, 0, Width, Height);
        }

        public uint[] GetPixels() => _pixels;

        public void Clear() => Array.Clear(_pixels, 0, _pixels.Length);

        public void MarkDirty(IntRect rect)
        {
            var clipped = IntRect.Intersect(rect, new IntRect(0, 0, Width, Height));
            if (clipped.IsEmpty) return;
            DirtyRegion = DirtyRegion.IsEmpty ? clipped : IntRect.Union(DirtyRegion, clipped);
        }

        public void MarkDirty(int x, int y, int w, int h)
        {
            MarkDirty(new IntRect(x, y, w, h));
        }

        public void ClearDirty() => DirtyRegion = default;

        public IntRect[] GetDirtyRects() => DirtyRegion.IsEmpty ? Array.Empty<IntRect>() : new[] { DirtyRegion };
    }
}
