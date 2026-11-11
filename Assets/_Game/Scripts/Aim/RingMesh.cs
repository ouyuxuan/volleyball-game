using UnityEngine;

namespace Sandspike.Aim
{
    /// <summary>Builds a flat ring (like a doughnut seen from above) lying on the XZ plane.</summary>
    public static class RingMesh
    {
        public static Mesh Create(float innerRadius, float outerRadius, int segments = 40)
        {
            var mesh = new Mesh { name = "Ring" };
            FillArc(mesh, innerRadius, outerRadius, 1f, segments);
            return mesh;
        }

        /// <summary>
        /// Rewrites 'mesh' as the first 'fraction' (0..1) of a ring, starting at the top (+Z) and going clockwise seen from above.
        /// fraction = 1 is a full ring; 0 is an empty mesh. The feet ring calls this every frame to animate its meters.
        /// </summary>
        public static void FillArc(Mesh mesh, float innerRadius, float outerRadius, float fraction, int segments = 40)
        {
            fraction = Mathf.Clamp01(fraction);
            bool full = fraction >= 0.9999f;
            int count = full ? segments : Mathf.Max(0, Mathf.CeilToInt(segments * fraction));
            mesh.Clear();
            if (count == 0) return;

            int ringPoints = full ? segments : count + 1;
            var verts = new Vector3[ringPoints * 2];
            for (int i = 0; i < ringPoints; i++)
            {
                // the last point of a partial arc lands exactly on the fraction, so the arc ends smoothly
                float f = full ? i / (float)segments : Mathf.Min(fraction, i * (fraction / count));
                float a = f * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts[i * 2] = dir * innerRadius;
                verts[i * 2 + 1] = dir * outerRadius;
            }

            var tris = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % ringPoints;
                int t = i * 6;
                // two triangles per segment, wound so the ring faces up
                tris[t] = i * 2;           tris[t + 1] = i * 2 + 1;    tris[t + 2] = next * 2 + 1;
                tris[t + 3] = i * 2;       tris[t + 4] = next * 2 + 1; tris[t + 5] = next * 2;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
