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
        public void Render(Document document, WriteableBitmap targetBitmap)
        {
            uint[] sourcePixels = document.GetPixelsRaw();

            int bitmapPixelCount = targetBitmap.PixelSize.Width * targetBitmap.PixelSize.Height;
            int docPixelCount    = document.Width * document.Height;
            if (bitmapPixelCount != docPixelCount)
                throw new InvalidOperationException(
                    $"Bitmap size ({bitmapPixelCount} px) does not match document ({docPixelCount} px). " +
                    "Recreate the WriteableBitmap when the document is resized.");

            try
            {
                using var lock_ = targetBitmap.Lock();
                unsafe
                {
                    long byteCount = (long)docPixelCount * sizeof(uint);
                    fixed (uint* sourcePtr = sourcePixels)
                    {
                        Buffer.MemoryCopy(
                            source: sourcePtr,
                            destination: (void*)lock_.Address,
                            destinationSizeInBytes: byteCount,
                            sourceBytesToCopy: byteCount);
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
