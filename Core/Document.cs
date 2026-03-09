using System;


namespace Pixellum.Core
{

    public class Document
    {
        public int Width { get; }
        public int Height { get; }

        // Raw pixel data buffer: uint[] ARGB (0xAARRGGBB).
        private uint[] _pixels;

        public Document(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Width and Height must be positive.");
            }

            Width = width;
            Height = height;

            // Allocate memory for the pixel buffer.
            _pixels = new uint[Width * Height];
            // By default, C# initializes uint[] to 0x00000000 (transparent black).
        }

        // Corresponds to the Document API.
        /// <remarks>Phase 1 stub — ignores x/y/w/h and always returns the full buffer. Use GetPixelsRaw() instead.</remarks>
        [System.Obsolete("GetRectPixels is not yet implemented correctly. Use GetPixelsRaw() until Phase 2.")]
        public uint[] GetRectPixels(int x, int y, int w, int h) => _pixels;

        // Method to get the full raw buffer (used by Renderer/Layer Composer)
        public uint[] GetPixelsRaw() => _pixels;
    }
}
