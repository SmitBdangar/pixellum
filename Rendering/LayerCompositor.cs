using Pixellum.Core;
using System;
using System.Collections.Generic;

namespace Pixellum.Rendering
{
    public static class LayerCompositor
    {
        private static float[] _baseAlphaBuffer = Array.Empty<float>();

        public static void Composite(Document document, IReadOnlyList<Layer> layers)
        {
            uint[] documentPixels = document.GetPixelsRaw();
            Array.Clear(documentPixels, 0, documentPixels.Length);

            if (_baseAlphaBuffer.Length < documentPixels.Length)
            {
                _baseAlphaBuffer = new float[documentPixels.Length];
            }

            for (int li = 0; li < layers.Count; li++)
            {
                var layer = layers[li];
                if (!layer.Visible || layer.Opacity <= 0.001f)
                    continue;

                uint[] layerPixels = layer.GetPixels();
                float  layerOpacity = layer.Opacity;

                if (!layer.IsClippingMask)
                {
                    for (int i = 0; i < documentPixels.Length; i++)
                    {
                        _baseAlphaBuffer[i] = ((layerPixels[i] >> 24) & 0xFF) / 255.0f * layerOpacity;
                    }
                }

                for (int i = 0; i < documentPixels.Length; i++)
                {
                    uint src = layerPixels[i];

                    float srcA = ((src >> 24) & 0xFF) / 255.0f * layerOpacity;
                    
                    if (layer.IsClippingMask)
                    {
                        srcA *= _baseAlphaBuffer[i];
                    }

                    if (srcA <= 0.001f) continue;

                    uint  dst    = documentPixels[i];
                    float dstA   = ((dst >> 24) & 0xFF) / 255.0f;
                    float invSrcA = 1.0f - srcA;

                    float srcR = ((src >> 16) & 0xFF) / 255.0f;
                    float srcG = ((src >>  8) & 0xFF) / 255.0f;
                    float srcB = ( src        & 0xFF) / 255.0f;

                    float dstR = ((dst >> 16) & 0xFF) / 255.0f;
                    float dstG = ((dst >>  8) & 0xFF) / 255.0f;
                    float dstB = ( dst        & 0xFF) / 255.0f;

                    // Apply blend mode to RGB channels
                    float blendR, blendG, blendB;
                    ApplyBlendMode(layer.Mode,
                        srcR, srcG, srcB,
                        dstR, dstG, dstB,
                        out blendR, out blendG, out blendB);

                    // Porter-Duff src-over compositing with blended color
                    float outA = srcA + dstA * invSrcA;
                    float outR, outG, outB;
                    if (outA < 1e-6f)
                    {
                        outR = outG = outB = 0f;
                    }
                    else
                    {
                        outR = (blendR * srcA + dstR * dstA * invSrcA) / outA;
                        outG = (blendG * srcA + dstG * dstA * invSrcA) / outA;
                        outB = (blendB * srcA + dstB * dstA * invSrcA) / outA;
                    }

                    uint a = (uint)Math.Clamp(outA * 255f, 0, 255);
                    uint r = (uint)Math.Clamp(outR * 255f, 0, 255);
                    uint g = (uint)Math.Clamp(outG * 255f, 0, 255);
                    uint b = (uint)Math.Clamp(outB * 255f, 0, 255);

                    documentPixels[i] = (a << 24) | (r << 16) | (g << 8) | b;
                }
            }
        }

        // ─── Blend mode dispatch ─────────────────────────────────────────────

        private static void ApplyBlendMode(BlendMode mode,
            float sr, float sg, float sb,
            float dr, float dg, float db,
            out float outR, out float outG, out float outB)
        {
            switch (mode)
            {
                case BlendMode.Multiply:
                    outR = sr * dr; outG = sg * dg; outB = sb * db;
                    return;

                case BlendMode.Screen:
                    outR = 1-(1-sr)*(1-dr); outG = 1-(1-sg)*(1-dg); outB = 1-(1-sb)*(1-db);
                    return;

                case BlendMode.Overlay:
                    outR = Overlay(sr,dr); outG = Overlay(sg,dg); outB = Overlay(sb,db);
                    return;

                case BlendMode.Darken:
                    outR = Math.Min(sr,dr); outG = Math.Min(sg,dg); outB = Math.Min(sb,db);
                    return;

                case BlendMode.Lighten:
                    outR = Math.Max(sr,dr); outG = Math.Max(sg,dg); outB = Math.Max(sb,db);
                    return;

                case BlendMode.ColorDodge:
                    outR = ColorDodge(sr,dr); outG = ColorDodge(sg,dg); outB = ColorDodge(sb,db);
                    return;

                case BlendMode.ColorBurn:
                    outR = ColorBurn(sr,dr); outG = ColorBurn(sg,dg); outB = ColorBurn(sb,db);
                    return;

                case BlendMode.HardLight:
                    // Hard light = Overlay with src/dst swapped
                    outR = Overlay(dr,sr); outG = Overlay(dg,sg); outB = Overlay(db,sb);
                    return;

                case BlendMode.SoftLight:
                    outR = SoftLight(sr,dr); outG = SoftLight(sg,dg); outB = SoftLight(sb,db);
                    return;

                case BlendMode.Difference:
                    outR = Math.Abs(sr-dr); outG = Math.Abs(sg-dg); outB = Math.Abs(sb-db);
                    return;

                case BlendMode.Exclusion:
                    outR = sr+dr-2*sr*dr; outG = sg+dg-2*sg*dg; outB = sb+db-2*sb*db;
                    return;

                case BlendMode.Hue:
                    RgbToHsl(sr,sg,sb, out float sh, out _, out _);
                    RgbToHsl(dr,dg,db, out _, out float ds, out float dl);
                    HslToRgb(sh,ds,dl, out outR, out outG, out outB);
                    return;

                case BlendMode.Saturation:
                    RgbToHsl(dr,dg,db, out float dh2, out _, out float dl2);
                    RgbToHsl(sr,sg,sb, out _, out float ss2, out _);
                    HslToRgb(dh2,ss2,dl2, out outR, out outG, out outB);
                    return;

                case BlendMode.Color:
                    RgbToHsl(sr,sg,sb, out float sh3, out float ss3, out _);
                    RgbToHsl(dr,dg,db, out _, out _, out float dl3);
                    HslToRgb(sh3,ss3,dl3, out outR, out outG, out outB);
                    return;

                case BlendMode.Luminosity:
                    RgbToHsl(dr,dg,db, out float dh4, out float ds4, out _);
                    RgbToHsl(sr,sg,sb, out _, out _, out float sl4);
                    HslToRgb(dh4,ds4,sl4, out outR, out outG, out outB);
                    return;

                default: // Normal
                    outR = sr; outG = sg; outB = sb;
                    return;
            }
        }

        // ─── Channel blend helpers ────────────────────────────────────────────

        private static float Overlay(float s, float d)
            => d < 0.5f ? 2*s*d : 1 - 2*(1-s)*(1-d);

        private static float ColorDodge(float s, float d)
            => s >= 1f ? 1f : Math.Min(1f, d / (1f - s));

        private static float ColorBurn(float s, float d)
            => s <= 0f ? 0f : Math.Max(0f, 1f - (1f - d) / s);

        private static float SoftLight(float s, float d)
        {
            if (s <= 0.5f)
                return d - (1f - 2f*s) * d * (1f - d);
            float dSqrt = d <= 0.25f
                ? ((16f*d - 12f)*d + 4f)*d
                : MathF.Sqrt(d);
            return d + (2f*s - 1f) * (dSqrt - d);
        }

        // ─── HSL ↔ RGB conversion ─────────────────────────────────────────────

        private static void RgbToHsl(float r, float g, float b,
            out float h, out float s, out float l)
        {
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
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

            r = Hue2Rgb(p, q, h + 1f/3f);
            g = Hue2Rgb(p, q, h);
            b = Hue2Rgb(p, q, h - 1f/3f);
        }

        private static float Hue2Rgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f/6f) return p + (q-p)*6f*t;
            if (t < 1f/2f) return q;
            if (t < 2f/3f) return p + (q-p)*(2f/3f-t)*6f;
            return p;
        }
    }
}