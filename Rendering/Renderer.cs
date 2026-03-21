using System;
using Pixellum.Core;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Platform;
using System.Runtime.CompilerServices;

namespace Pixellum.Rendering
{
    public class Renderer
    {
        /// <summary>Full canvas render.</summary>
        public void Render(Document document, WriteableBitmap targetBitmap)
        {
            Render(document, targetBitmap, new IntRect(0, 0, document.Width, document.Height));
        }

        /// <summary>Dirty rect render (memcpy clipped).</summary>
        public void Render(Document document, WriteableBitmap targetBitmap, IntRect dirtyRect)
        {
            uint[] sourcePixels = document.GetPixelsRaw();

            int bitmapPixelCount = targetBitmap.PixelSize.Width * targetBitmap.PixelSize.Height;
            int docPixelCount    = document.Width * document.Height;
            if (bitmapPixelCount != docPixelCount)
                throw new InvalidOperationException(
                    $"Bitmap size ({bitmapPixelCount} px) does not match document ({docPixelCount} px).");

            dirtyRect = IntRect.Intersect(dirtyRect, new IntRect(0, 0, document.Width, document.Height));
            if (dirtyRect.IsEmpty) return;

            try
            {
                using var lock_ = targetBitmap.Lock();
                unsafe
                {
                    int stride = document.Width * sizeof(uint);
                    fixed (uint* sourcePtr = sourcePixels)
                    {
                        byte* src = (byte*)sourcePtr;
                        byte* dst = (byte*)lock_.Address.ToPointer();
                        int dstStride = lock_.RowBytes;

                        for (int y = dirtyRect.Y; y < dirtyRect.Bottom; y++)
                        {
                            Buffer.MemoryCopy(
                                src + (nint)(y * stride + dirtyRect.X * sizeof(uint)),
                                dst + (nint)(y * dstStride + dirtyRect.X * sizeof(uint)),
                                dirtyRect.Width * sizeof(uint),
                                dirtyRect.Width * sizeof(uint));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rendering error: {ex.Message}");
            }
        }
    }
}
