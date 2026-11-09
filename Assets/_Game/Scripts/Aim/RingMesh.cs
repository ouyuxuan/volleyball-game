using UnityEngine;

namespace Sandspike.Aim
{
    /// <summary>Builds a flat ring (like a doughnut seen from above) lying on the XZ plane.</summary>
    public static class RingMesh
    {
        public static Mesh Create(float innerRadius, float outerRadius, int segments = 40)
        {
            var verts = new Vector3[segments * 2];
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * innerRadius;
                verts[i * 2 + 1] = dir * outerRadius;

                int next = (i + 1) % segments;
                int t = i * 6;
                // two triangles per segment, wound so the ring faces up
                tris[t] = i * 2;           tris[t + 1] = next * 2 + 1; tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2;       tris[t + 4] = next * 2;     tris[t + 5] = next * 2 + 1;
            }
            var mesh = new Mesh { name = "Ring" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
