using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Platform;

namespace Pixellum.Core
{
    public static class BitmapFactory
    {
        public static WriteableBitmap Create(int width, int height)
        {
            return new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);
        }
    }
}

