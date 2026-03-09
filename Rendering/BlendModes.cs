using Pixellum.Core;

namespace Pixellum.Rendering
{
    /// <summary>
    /// Placeholder class for advanced blending logic to be implemented in Phase 2.
    /// This module will contain static methods for Multiply, Screen, Overlay, etc.
    /// </summary>
    public static class BlendModes
    {
        // FUTURE: public static uint Multiply(uint src, uint dst)
        // FUTURE: public static uint Screen(uint src, uint dst)
        // FUTURE: public static uint Overlay(uint src, uint dst)

        // Phase 1: Only Normal (src-over) blend is used via LayerCompositor.
        // CompositeLayers is a Phase 2 feature — using Core.Layer avoids the
        // ambiguous shadow class that previously lived here.
        public static uint CompositeLayers(Layer src, Layer dst)
        {
            // Phase 2 implementation will iterate pixels and apply Layer.Mode.
            throw new System.NotImplementedException("Full layer composition is a Phase 2 feature.");
        }
    }
}
