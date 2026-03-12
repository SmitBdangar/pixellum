using System;

namespace Pixellum.Core
{
    public static class PixelUtils
    {
        public static uint[] GetRegionPixels(uint[] source, int sourceWidth, IntRect rect, uint[]? destination = null)
        {
            if (rect.IsEmpty)
                return destination ?? Array.Empty<uint>();

            if (rect.X < 0 || rect.Y < 0 ||
                rect.X + rect.Width > sourceWidth ||
                rect.Y + rect.Height > (source.Length / sourceWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(rect), "Rectangle is outside source pixel bounds.");
            }

            int pixelCount = rect.Width * rect.Height;
            destination ??= new uint[pixelCount];

            if (destination.Length < pixelCount)
                Array.Resize(ref destination, pixelCount);

            int destIndex = 0;
            int sourceStride = sourceWidth;

            for (int y = rect.Y; y < rect.Y + rect.Height; y++)
            {
                int srcIndex = y * sourceStride + rect.X;
                source.AsSpan(srcIndex, rect.Width).CopyTo(destination.AsSpan(destIndex));
                destIndex += rect.Width;
            }

            return destination;
        }
    }
}
