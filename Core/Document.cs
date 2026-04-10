using System;


namespace Pixellum.Core
{

    public class Document
    {
        public int Width { get; }
        public int Height { get; }

        private uint[] _pixels;
        public IntRect DirtyRegion { get; private set; } = default;

        public Document(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Width and Height must be positive.");
            }

            Width = width;
            Height = height;

            _pixels = new uint[Width * Height];
            DirtyRegion = new IntRect(0, 0, width, height);
        }

        public void MarkDirty(IntRect rect)
        {
            DirtyRegion = IntRect.Union(DirtyRegion, rect);
        }

        public void MarkDirty(int x, int y, int w, int h)
        {
            MarkDirty(new IntRect(x, y, w, h));
        }

        public void ClearDirty() => DirtyRegion = default;

        public uint[] GetPixelsRaw() => _pixels;
    }
}
