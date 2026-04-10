using System;

namespace Pixellum.Core
{
    /// <summary>
    /// Shared color math utilities: HSL↔RGB, alpha compositing, blend mode parsing.
    /// Consolidates the duplicated implementations from LayerCompositor and Adjustments.
    /// </summary>
    public static class ColorMath
    {
        // ─── HSL ↔ RGB ───────────────────────────────────────────────────────

        public static void RgbToHsl(float r, float g, float b,
            out float h, out float s, out float l)
        {
            float max   = Math.Max(r, Math.Max(g, b));
            float min   = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            l = (max + min) / 2f;

            if (delta < 1e-6f) { h = s = 0f; return; }

            s = l < 0.5f ? delta / (max + min) : delta / (2f - max - min);

            if      (max == r) h = ((g - b) / delta + (g < b ? 6f : 0f)) / 6f;
            else if (max == g) h = ((b - r) / delta + 2f) / 6f;
            else               h = ((r - g) / delta + 4f) / 6f;
        }

        public static void HslToRgb(float h, float s, float l,
            out float r, out float g, out float b)
        {
            if (s < 1e-6f) { r = g = b = l; return; }

            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;

            r = Hue2Rgb(p, q, h + 1f / 3f);
            g = Hue2Rgb(p, q, h);
            b = Hue2Rgb(p, q, h - 1f / 3f);
        }

        private static float Hue2Rgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        // ─── Alpha compositing (straight-alpha src-over) ─────────────────────

        /// <summary>
        /// Performs straight-alpha src-over composite of two ARGB uint pixels.
        /// Returns the composited pixel.
        /// </summary>
        public static uint AlphaComposite(uint src, uint dst)
        {
            float srcA = ((src >> 24) & 0xFF) / 255.0f;
            float dstA = ((dst >> 24) & 0xFF) / 255.0f;
            float invSrcA = 1.0f - srcA;
            float outA = srcA + dstA * invSrcA;

            if (outA < 1e-6f) return 0;

            float srcR = ((src >> 16) & 0xFF) / 255.0f;
            float srcG = ((src >>  8) & 0xFF) / 255.0f;
            float srcB = ( src        & 0xFF) / 255.0f;
            float dstR = ((dst >> 16) & 0xFF) / 255.0f;
            float dstG = ((dst >>  8) & 0xFF) / 255.0f;
            float dstB = ( dst        & 0xFF) / 255.0f;

            uint A = (uint)Math.Clamp(outA * 255f, 0, 255);
            uint R = (uint)Math.Clamp(((srcR * srcA + dstR * dstA * invSrcA) / outA) * 255f, 0, 255);
            uint G = (uint)Math.Clamp(((srcG * srcA + dstG * dstA * invSrcA) / outA) * 255f, 0, 255);
            uint B = (uint)Math.Clamp(((srcB * srcA + dstB * dstA * invSrcA) / outA) * 255f, 0, 255);

            return (A << 24) | (R << 16) | (G << 8) | B;
        }

        /// <summary>
        /// Same as AlphaComposite but with an additional layer opacity multiplier on the source.
        /// </summary>
        public static uint AlphaComposite(uint src, uint dst, float layerOpacity)
        {
            float srcA = ((src >> 24) & 0xFF) / 255.0f * layerOpacity;
            if (srcA <= 0.001f) return dst;

            float dstA = ((dst >> 24) & 0xFF) / 255.0f;
            float invSrcA = 1.0f - srcA;
            float outA = srcA + dstA * invSrcA;

            if (outA < 1e-6f) return 0;

            float srcR = ((src >> 16) & 0xFF) / 255.0f;
            float srcG = ((src >>  8) & 0xFF) / 255.0f;
            float srcB = ( src        & 0xFF) / 255.0f;
            float dstR = ((dst >> 16) & 0xFF) / 255.0f;
            float dstG = ((dst >>  8) & 0xFF) / 255.0f;
            float dstB = ( dst        & 0xFF) / 255.0f;

            uint A = (uint)Math.Clamp(outA * 255f, 0, 255);
            uint R = (uint)Math.Clamp(((srcR * srcA + dstR * dstA * invSrcA) / outA) * 255f, 0, 255);
            uint G = (uint)Math.Clamp(((srcG * srcA + dstG * dstA * invSrcA) / outA) * 255f, 0, 255);
            uint B = (uint)Math.Clamp(((srcB * srcA + dstB * dstA * invSrcA) / outA) * 255f, 0, 255);

            return (A << 24) | (R << 16) | (G << 8) | B;
        }

        // ─── Blend mode parsing ──────────────────────────────────────────────

        /// <summary>
        /// Parses a blend mode name string to the BlendMode enum.
        /// Replaces the duplicated switch expressions in MainWindow and LayersPanel.
        /// </summary>
        public static BlendMode ParseBlendMode(string? name) => name switch
        {
            "Darken"     => BlendMode.Darken,
            "Multiply"   => BlendMode.Multiply,
            "ColorBurn"  => BlendMode.ColorBurn,
            "Lighten"    => BlendMode.Lighten,
            "Screen"     => BlendMode.Screen,
            "ColorDodge" => BlendMode.ColorDodge,
            "Overlay"    => BlendMode.Overlay,
            "SoftLight"  => BlendMode.SoftLight,
            "HardLight"  => BlendMode.HardLight,
            "Difference" => BlendMode.Difference,
            "Exclusion"  => BlendMode.Exclusion,
            "Hue"        => BlendMode.Hue,
            "Saturation" => BlendMode.Saturation,
            "Color"      => BlendMode.Color,
            "Luminosity" => BlendMode.Luminosity,
            _            => BlendMode.Normal
        };

        // ─── Pixel component helpers ─────────────────────────────────────────

        /// <summary>Builds an ARGB uint from individual byte components.</summary>
        public static uint PackArgb(byte a, byte r, byte g, byte b) =>
            ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;

        /// <summary>Builds an ARGB uint from float components (0-1 range).</summary>
        public static uint PackArgb(float a, float r, float g, float b) =>
            ((uint)Math.Clamp(a * 255f, 0, 255) << 24) |
            ((uint)Math.Clamp(r * 255f, 0, 255) << 16) |
            ((uint)Math.Clamp(g * 255f, 0, 255) <<  8) |
             (uint)Math.Clamp(b * 255f, 0, 255);
    }
}
