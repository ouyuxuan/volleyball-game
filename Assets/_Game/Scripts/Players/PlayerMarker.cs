using System.Collections.Generic;
using Sandspike.Art;
using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>A small downward-pointing marker (an Accent pyramid) bobbing above the human player, so you can always find yourself.</summary>
    public class PlayerMarker : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] float height = 2.6f;       // metres above the feet
        [SerializeField] float size = 0.28f;
        [SerializeField] float bobAmount = 0.08f;
        [SerializeField] float bobHz = 1.2f;
        [SerializeField] float spinDegreesPerSecond = 90f;

        void Start()
        {
            var mf = gameObject.GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            var mr = gameObject.GetComponent<MeshRenderer>();
            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildPyramid(size);
            mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Palette.Accent };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // A four-sided pyramid with its tip pointing down (flat shaded: every face has its own vertices).
        static Mesh BuildPyramid(float s)
        {
            Vector3 tip = new Vector3(0f, -s, 0f);
            Vector3 a = new Vector3(-s * 0.7f, s * 0.6f, -s * 0.7f), b = new Vector3(-s * 0.7f, s * 0.6f, s * 0.7f);
            Vector3 c = new Vector3(s * 0.7f, s * 0.6f, s * 0.7f), d = new Vector3(s * 0.7f, s * 0.6f, -s * 0.7f);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Tri(Vector3 p, Vector3 q, Vector3 r) { int i = verts.Count; verts.Add(p); verts.Add(q); verts.Add(r); tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); }
            Tri(tip, b, a); Tri(tip, c, b); Tri(tip, d, c); Tri(tip, a, d);   // sides
            Tri(a, c, d); Tri(a, b, c);                                       // top cap
            var mesh = new Mesh { name = "PlayerMarker" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void LateUpdate()
        {
            if (player == null) return;
            Vector3 p = player.transform.position;
            transform.position = new Vector3(p.x, p.y + height + Mathf.Sin(Time.time * bobHz * Mathf.PI * 2f) * bobAmount, p.z);
            transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.World);
        }
    }
}
