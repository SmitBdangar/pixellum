using Pixellum.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixellum.Rendering
{
    /// <summary>
    /// Handles combining all layers in a Document into the final composite pixel buffer.
    /// Phase 1: Simple copy of the active layer's pixels.
    /// Phase 2: Will implement blend modes and opacity for each layer.
    /// </summary>
    public static class LayerCompositor
    {
        public static void Composite(Document document, IEnumerable<Layer> layers)
        {
            uint[] documentPixels = document.GetPixelsRaw();
            
            // Clear document buffer to transparent black initially
            Array.Clear(documentPixels, 0, documentPixels.Length);

            // Get layers in bottom-to-top order for rendering
            var visibleLayers = layers.Where(l => l.Visible).Reverse().ToList();

            if (visibleLayers.Count == 0)
                return;

            foreach (var layer in visibleLayers)
            {
                uint[] layerPixels = layer.GetPixels();
                float layerOpacity = layer.Opacity;

                if (layerOpacity <= 0.001f)
                    continue;

                // Fast path for 100% opaque layers overriding everything below (if we tracked opaque regions)
                // For now, alpha blend every pixel
                for (int i = 0; i < documentPixels.Length; i++)
                {
                    uint src = layerPixels[i];
                    uint dst = documentPixels[i];

                    // Premultiplied alpha blend
                    float srcA = ((src >> 24) & 0xFF) / 255.0f * layerOpacity;
                    if (srcA <= 0.001f) continue; // Skip transparent pixels

                    float dstA = ((dst >> 24) & 0xFF) / 255.0f;
                    float invSrcA = 1.0f - srcA;

                    float srcR = ((src >> 16) & 0xFF) / 255.0f * layerOpacity; // Apply layer opacity to colors too if not already premul
                    float srcG = ((src >> 8) & 0xFF) / 255.0f * layerOpacity;
                    float srcB = (src & 0xFF) / 255.0f * layerOpacity;

                    float dstR = ((dst >> 16) & 0xFF) / 255.0f;
                    float dstG = ((dst >> 8) & 0xFF) / 255.0f;
                    float dstB = (dst & 0xFF) / 255.0f;

                    float outR = srcR + dstR * invSrcA;
                    float outG = srcG + dstG * invSrcA;
                    float outB = srcB + dstB * invSrcA;
                    float outA = srcA + dstA * invSrcA;

                    uint a = (uint)Math.Clamp(outA * 255.0f, 0, 255);
                    uint r = (uint)Math.Clamp(outR * 255.0f, 0, 255);
                    uint g = (uint)Math.Clamp(outG * 255.0f, 0, 255);
                    uint b = (uint)Math.Clamp(outB * 255.0f, 0, 255);

                    documentPixels[i] = (a << 24) | (r << 16) | (g << 8) | b;
                }
            }
        }
    }
}