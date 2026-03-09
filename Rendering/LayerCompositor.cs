using Pixellum.Core;
using System;
using System.Collections.Generic;

namespace Pixellum.Rendering
{
    /// <summary>
    /// Combines all visible layers in a Document into the final composite pixel buffer,
    /// iterating bottom-to-top using index-based access (no LINQ allocation per frame).
    /// </summary>
    public static class LayerCompositor
    {
        public static void Composite(Document document, IReadOnlyList<Layer> layers)
        {
            uint[] documentPixels = document.GetPixelsRaw();

            // Clear document buffer to transparent black
            Array.Clear(documentPixels, 0, documentPixels.Length);

            // Iterate bottom-to-top — layer[0] is the bottom layer
            for (int li = 0; li < layers.Count; li++)
            {
                var layer = layers[li];
                if (!layer.Visible || layer.Opacity <= 0.001f)
                    continue;

                uint[] layerPixels = layer.GetPixels();
                float  layerOpacity = layer.Opacity;

                for (int i = 0; i < documentPixels.Length; i++)
                {
                    uint src = layerPixels[i];

                    // Modulate layer alpha by layer opacity
                    float srcA = ((src >> 24) & 0xFF) / 255.0f * layerOpacity;
                    if (srcA <= 0.001f) continue;

                    uint  dst    = documentPixels[i];
                    float dstA   = ((dst >> 24) & 0xFF) / 255.0f;
                    float invSrcA = 1.0f - srcA;

                    // Straight-alpha Porter-Duff src-over
                    float srcR = ((src >> 16) & 0xFF) / 255.0f;
                    float srcG = ((src >>  8) & 0xFF) / 255.0f;
                    float srcB = ( src        & 0xFF) / 255.0f;

                    float dstR = ((dst >> 16) & 0xFF) / 255.0f;
                    float dstG = ((dst >>  8) & 0xFF) / 255.0f;
                    float dstB = ( dst        & 0xFF) / 255.0f;

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

                    uint a = (uint)Math.Clamp(outA * 255f, 0, 255);
                    uint r = (uint)Math.Clamp(outR * 255f, 0, 255);
                    uint g = (uint)Math.Clamp(outG * 255f, 0, 255);
                    uint b = (uint)Math.Clamp(outB * 255f, 0, 255);

                    documentPixels[i] = (a << 24) | (r << 16) | (g << 8) | b;
                }
            }
        }
    }
}