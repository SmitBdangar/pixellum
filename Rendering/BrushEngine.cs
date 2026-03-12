using System;
using Pixellum.Core;

namespace Pixellum.Rendering
{
    public class BrushEngine
    {
        public void ApplyBrush(Layer layer, int centerX, int centerY, uint brushColor, float radius)
        {
            float srcA = ((brushColor >> 24) & 0xFF) / 255.0f;

            float srcR = ((brushColor >> 16) & 0xFF) / 255.0f;
            float srcG = ((brushColor >> 8)  & 0xFF) / 255.0f;
            float srcB = ( brushColor        & 0xFF) / 255.0f;

            uint[] pixels = layer.GetPixels();

            IterateCircle(layer, centerX, centerY, radius, (index, dist) =>
            {
                float t = dist / radius;
                float falloff = 1.0f - (t * t);
                float stampA  = srcA * falloff;

                pixels[index] = AlphaBlend(srcR, srcG, srcB, stampA, pixels[index]);
            });
        }

        public void ApplyEraser(Layer layer, int centerX, int centerY, float radius)
        {
            uint[] pixels = layer.GetPixels();

            IterateCircle(layer, centerX, centerY, radius, (index, dist) =>
            {
                float t        = dist / radius;
                float falloff  = 1.0f - (t * t);   // same soft edge as the brush

                uint  dst      = pixels[index];
                float dstA     = ((dst >> 24) & 0xFF) / 255.0f;
                float newA     = Math.Max(0f, dstA - falloff);

                uint  dstR     = (dst >> 16) & 0xFF;
                uint  dstG     = (dst >>  8) & 0xFF;
                uint  dstB     =  dst        & 0xFF;
                uint  outA     = (uint)Math.Clamp(newA * 255f, 0, 255);

                pixels[index]  = (outA << 24) | (dstR << 16) | (dstG << 8) | dstB;
            });
        }

        private static void IterateCircle(Layer layer, int cx, int cy, float radius,
            Action<int, float> callback)
        {
            int minX = Math.Max(0, (int)(cx - radius));
            int minY = Math.Max(0, (int)(cy - radius));
            int maxX = Math.Min(layer.Width,  (int)MathF.Ceiling(cx + radius));
            int maxY = Math.Min(layer.Height, (int)MathF.Ceiling(cy + radius));

            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    float dx   = x - cx;
                    float dy   = y - cy;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist <= radius)
                        callback(y * layer.Width + x, dist);
                }
            }
        }

        private static uint AlphaBlend(float srcR, float srcG, float srcB, float srcA, uint dst)
        {
            float dstA = ((dst >> 24) & 0xFF) / 255.0f;
            float dstR = ((dst >> 16) & 0xFF) / 255.0f;
            float dstG = ((dst >>  8) & 0xFF) / 255.0f;
            float dstB = ( dst        & 0xFF) / 255.0f;

            float invSrcA = 1.0f - srcA;

            float outA = srcA + dstA * invSrcA;
            float outR, outG, outB;
            if (outA < 1e-6f)
            {
                outR = outG = outB = 0f;
            }
            else
            {
                outR = (srcR * srcA + dstR * dstA * invSrcA) / outA;
                outG = (srcG * srcA + dstG * dstA * invSrcA) / outA;
                outB = (srcB * srcA + dstB * dstA * invSrcA) / outA;
            }

            uint A = (uint)Math.Clamp(outA * 255f, 0, 255);
            uint R = (uint)Math.Clamp(outR * 255f, 0, 255);
            uint G = (uint)Math.Clamp(outG * 255f, 0, 255);
            uint B = (uint)Math.Clamp(outB * 255f, 0, 255);

            return (A << 24) | (R << 16) | (G << 8) | B;
        }
    }
}
