namespace Pixellum.Core
{
    public readonly struct IntRect
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public int Right => X + Width;
        public int Bottom => Y + Height;

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public IntRect(int x, int y, int width, int height)
        {
            if (width < 0) { x += width; width = -width; }
            if (height < 0) { y += height; height = -height; }

            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public static IntRect Union(IntRect a, IntRect b)
        {
            if (a.IsEmpty) return b;
            if (b.IsEmpty) return a;

            int x1 = System.Math.Min(a.X, b.X);
            int y1 = System.Math.Min(a.Y, b.Y);
            int x2 = System.Math.Max(a.Right, b.Right);
            int y2 = System.Math.Max(a.Bottom, b.Bottom);

            return new IntRect(x1, y1, x2 - x1, y2 - y1);
        }

        public static IntRect Intersect(IntRect a, IntRect b)
        {
            int x1 = System.Math.Max(a.X, b.X);
            int y1 = System.Math.Max(a.Y, b.Y);
            int x2 = System.Math.Min(a.Right, b.Right);
            int y2 = System.Math.Min(a.Bottom, b.Bottom);

            return (x2 > x1 && y2 > y1) ? new IntRect(x1, y1, x2 - x1, y2 - y1) : default;
        }

        public bool Contains(int x, int y) =>
            x >= X && x < Right && y >= Y && y < Bottom;

        public bool Intersects(IntRect other) =>
            !Intersect(this, other).IsEmpty;

        public override string ToString() =>
            $"IntRect [X={X}, Y={Y}, W={Width}, H={Height}]";
    }
}
