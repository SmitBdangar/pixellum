using System;


namespace Pixellum.Core
{

    public class Document
    {
        public int Width { get; }
        public int Height { get; }

        private uint[] _pixels;

        public Document(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Width and Height must be positive.");
            }

            Width = width;
            Height = height;

            _pixels = new uint[Width * Height];
        }

        [System.Obsolete("GetRectPixels is not yet implemented correctly. Use GetPixelsRaw() until Phase 2.")]
        public uint[] GetRectPixels(int x, int y, int w, int h) => _pixels;

        public uint[] GetPixelsRaw() => _pixels;
    }
}
