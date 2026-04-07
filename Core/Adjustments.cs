using System;

namespace Pixellum.Core
{
    /// <summary>
    /// Pixel-level image adjustments operating on ARGB uint[] arrays (same format as Layer.GetPixels()).
    /// All operations are in-place and designed to be wrapped in a SaveStateForUndo() call.
    /// </summary>
    public static class Adjustments
    {
        // ─── Brightness / Contrast ────────────────────────────────────────────
        // brightness: -1..+1, contrast: -1..+1

        public static void BrightnessContrast(uint[] pixels, float brightness, float contrast)
        {
            // Photoshop-style: contrast rotates around the midpoint 0.5
            float slope = contrast >= 0
                ? 1f / (1f - contrast * 0.985f)
                : 1f + contrast;

            for (int i = 0; i < pixels.Length; i++)
            {
                uint p = pixels[i];
                uint a = (p >> 24) & 0xFF;
                if (a == 0) continue;

                float r = ((p >> 16) & 0xFF) / 255f;
                float g = ((p >>  8) & 0xFF) / 255f;
                float b = ( p        & 0xFF) / 255f;

                // Apply brightness (additive)
                r = Math.Clamp(r + brightness, 0, 1);
                g = Math.Clamp(g + brightness, 0, 1);
                b = Math.Clamp(b + brightness, 0, 1);

                // Apply contrast (around midpoint)
                r = Math.Clamp((r - 0.5f) * slope + 0.5f, 0, 1);
                g = Math.Clamp((g - 0.5f) * slope + 0.5f, 0, 1);
                b = Math.Clamp((b - 0.5f) * slope + 0.5f, 0, 1);

                uint R = (uint)(r * 255f + 0.5f);
                uint G = (uint)(g * 255f + 0.5f);
                uint B = (uint)(b * 255f + 0.5f);
                pixels[i] = (a << 24) | (R << 16) | (G << 8) | B;
            }
        }

        // ─── Hue / Saturation / Lightness ────────────────────────────────────
        // hue: -180..+180 degrees, sat: -1..+1, lightness: -1..+1

        public static void HueSaturation(uint[] pixels, float hueDelta, float satDelta, float lightDelta)
        {
            float hueShift = hueDelta / 360f;

            for (int i = 0; i < pixels.Length; i++)
            {
                uint p = pixels[i];
                uint a = (p >> 24) & 0xFF;
                if (a == 0) continue;

                float r = ((p >> 16) & 0xFF) / 255f;
                float g = ((p >>  8) & 0xFF) / 255f;
                float b = ( p        & 0xFF) / 255f;

                RgbToHsl(r, g, b, out float h, out float s, out float l);

                h = (h + hueShift) % 1f;
                if (h < 0) h += 1f;

                s = Math.Clamp(s + satDelta, 0, 1);
                l = Math.Clamp(l + lightDelta, 0, 1);

                HslToRgb(h, s, l, out r, out g, out b);

                uint R = (uint)(r * 255f + 0.5f);
                uint G = (uint)(g * 255f + 0.5f);
                uint B = (uint)(b * 255f + 0.5f);
                pixels[i] = (a << 24) | (R << 16) | (G << 8) | B;
            }
        }

        // ─── Levels ───────────────────────────────────────────────────────────
        // inBlack/inWhite: 0-255 input black/white points
        // gamma: 0.1-10 midtone (1.0 = neutral)
        // outBlack/outWhite: 0-255 output black/white points

        public static void Levels(uint[] pixels,
            int inBlack, int inWhite, float gamma,
            int outBlack, int outWhite)
        {
            // Build 256-entry LUT for speed
            var lut = new byte[256];
            float inRange  = Math.Max(1, inWhite - inBlack);
            float outRange = outWhite - outBlack;

            for (int v = 0; v < 256; v++)
            {
                float norm = Math.Clamp((v - inBlack) / inRange, 0f, 1f);
                if (gamma != 1f) norm = MathF.Pow(norm, 1f / gamma);
                lut[v] = (byte)Math.Clamp(outBlack + norm * outRange + 0.5f, 0, 255);
            }

            ApplyLut(pixels, lut, lut, lut);
        }

        // ─── Curves (per-channel 256-entry LUT) ──────────────────────────────

        public static void Curves(uint[] pixels, byte[] rMap, byte[] gMap, byte[] bMap)
        {
            if (rMap.Length != 256 || gMap.Length != 256 || bMap.Length != 256)
                throw new ArgumentException("Curve LUT must have exactly 256 entries.");

            ApplyLut(pixels, rMap, gMap, bMap);
        }

        // ─── Color Balance (shadow / midtone / highlight RGB shifts) ──────────

        public static void ColorBalance(uint[] pixels,
            float shadowR,   float shadowG,   float shadowB,
            float midtoneR,  float midtoneG,  float midtoneB,
            float highlightR,float highlightG,float highlightB)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                uint p = pixels[i];
                uint a = (p >> 24) & 0xFF;
                if (a == 0) continue;

                float r = ((p >> 16) & 0xFF) / 255f;
                float g = ((p >>  8) & 0xFF) / 255f;
                float b = ( p        & 0xFF) / 255f;

                // Luminosity-based zone weights (shadows → 0, midtones → 0.5, highlights → 1)
                float lum    = 0.299f * r + 0.587f * g + 0.114f * b;
                float shadow = Math.Clamp((1f - lum * 2f), 0, 1);
                float high   = Math.Clamp((lum * 2f - 1f), 0, 1);
                float mid    = 1f - shadow - high;

                r = Math.Clamp(r + shadow * shadowR + mid * midtoneR + high * highlightR, 0, 1);
                g = Math.Clamp(g + shadow * shadowG + mid * midtoneG + high * highlightG, 0, 1);
                b = Math.Clamp(b + shadow * shadowB + mid * midtoneB + high * highlightB, 0, 1);

                uint R = (uint)(r * 255f + 0.5f);
                uint G = (uint)(g * 255f + 0.5f);
                uint B = (uint)(b * 255f + 0.5f);
                pixels[i] = (a << 24) | (R << 16) | (G << 8) | B;
            }
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private static void ApplyLut(uint[] pixels, byte[] rMap, byte[] gMap, byte[] bMap)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                uint p = pixels[i];
                uint a = (p >> 24) & 0xFF;
                if (a == 0) continue;

                uint r  = (p >> 16) & 0xFF;
                uint g  = (p >>  8) & 0xFF;
                uint bv =  p        & 0xFF;

                // Apply all three channel look-up tables in a single write.
                pixels[i] = (a << 24) | ((uint)rMap[r] << 16) | ((uint)gMap[g] << 8) | bMap[bv];
            }
        }

        // ─── HSL helpers (same as LayerCompositor – kept private here) ────────

        private static void RgbToHsl(float r, float g, float b,
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

        private static void HslToRgb(float h, float s, float l,
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
    }
}
