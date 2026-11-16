using UnityEngine;

namespace Sandspike.CameraRig
{
    /// <summary>Which part of the screen each human's camera gets (pure maths). Rects are in 0..1 viewport units, y up.</summary>
    public static class SplitScreen
    {
        /// <summary>
        /// 1 human: the whole screen. 2: left and right halves. 3 or 4: quarters (top-left, top-right, bottom-left, bottom-right).
        /// With 3 humans the list has a 4th rect: the spare bottom-right quarter, which shows a neutral view of the court.
        /// </summary>
        public static Rect[] Layout(int humans)
        {
            if (humans <= 1) return new[] { new Rect(0f, 0f, 1f, 1f) };
            if (humans == 2) return new[] { new Rect(0f, 0f, 0.5f, 1f), new Rect(0.5f, 0f, 0.5f, 1f) };
            return new[] { new Rect(0f, 0.5f, 0.5f, 0.5f), new Rect(0.5f, 0.5f, 0.5f, 0.5f), new Rect(0f, 0f, 0.5f, 0.5f), new Rect(0.5f, 0f, 0.5f, 0.5f) };
        }
    }

    /// <summary>
    /// Human feedback objects (aim marker, rings, cues) live on a layer per human slot, so each camera shows only its own player's.
    /// Layers 24..27 are unnamed project layers; nothing else uses them.
    /// </summary>
    public static class ViewLayers
    {
        public const int First = 24;
        const int AllMask = 0xF << First;

        public static int For(int slot) => First + slot;

        /// <summary>Everything except the other humans' feedback layers.</summary>
        public static int MaskFor(int slot) => ~AllMask | (1 << For(slot));

        /// <summary>A camera that sees no personal feedback at all (the spare quarter).</summary>
        public static int NeutralMask => ~AllMask;

        /// <summary>Gives every child of 'root' the layer of 'root' (call after a script has built its parts).</summary>
        public static void ApplyToChildren(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;
        }
    }
}
