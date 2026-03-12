using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Pixellum.Core
{
    public static class FileHandler
    {
        // ── Export ────────────────────────────────────────────────────────────

        public static async Task ExportPng(WriteableBitmap bitmap, IStorageFile storageFile)
        {
            try
            {
                await using var stream = await storageFile.OpenWriteAsync();
                stream.SetLength(0);
                bitmap.Save(stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
            }
        }

        public static async Task SavePng(WriteableBitmap bitmap, string path)
        {
            try
            {
                await using var stream = File.OpenWrite(path);
                stream.SetLength(0);
                bitmap.Save(stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save failed: {ex.Message}");
            }
        }

        // ── Open Image ────────────────────────────────────────────────────────

        /// <summary>
        /// Loads any Avalonia-decodable image (PNG, JPEG, BMP, GIF, WebP) from a
        /// storage file and returns it as a flat BGRA uint[] array.
        /// </summary>
        public static async Task<(uint[] pixels, int width, int height)?> OpenImage(IStorageFile storageFile)
        {
            try
            {
                await using var stream = await storageFile.OpenReadAsync();
                return DecodeFromStream(stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Open failed: {ex.Message}");
                return null;
            }
        }

        public static (uint[] pixels, int width, int height)? OpenImageFromPath(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                return DecodeFromStream(stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Open failed: {ex.Message}");
                return null;
            }
        }

        private static (uint[] pixels, int width, int height) DecodeFromStream(Stream stream)
        {
            // Use Avalonia's built-in bitmap decoder
            var bitmap = new Bitmap(stream);
            int w = bitmap.PixelSize.Width;
            int h = bitmap.PixelSize.Height;

            // Render to a WriteableBitmap with Bgra8888/Unpremul so we can read pixels
            var wb = new WriteableBitmap(
                new Avalonia.PixelSize(w, h),
                new Avalonia.Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);

            using (var fb = wb.Lock())
            {
                // Draw source bitmap into the writable one
                bitmap.CopyPixels(fb, AlphaFormat.Unpremul);
            }

            // Read pixel data
            var pixels = new uint[w * h];
            using (var fb = wb.Lock())
            {
                unsafe
                {
                    uint* src = (uint*)fb.Address.ToPointer();
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] = src[i];
                }
            }

            return (pixels, w, h);
        }
    }
}
