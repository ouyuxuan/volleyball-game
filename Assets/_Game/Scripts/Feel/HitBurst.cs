using Sandspike.Aim;
using Sandspike.Art;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Feel
{
    /// <summary>
    /// A white burst where a spike or serve connects: a quick expanding ring and a few short speed lines, about 0.15 s,
    /// facing the camera and scaled by the charge of the hit.
    /// </summary>
    public class HitBurst : MonoBehaviour
    {
        [SerializeField] float seconds = 0.15f;
        [SerializeField] int lineCount = 7;
        [SerializeField] float baseRadius = 0.9f;      // ring radius at the end, at full charge (less for a light hit)

        Transform root, ring;
        Transform[] lines;
        Material mat;
        float age = 99f;
        float size;

        void Start()
        {
            mat = FeetRing.MakeMaterial(Palette.CourtLines, true);
            root = new GameObject("HitBurstShape").transform;
            root.SetParent(transform, false);

            var rg = new GameObject("Ring", typeof(MeshFilter), typeof(MeshRenderer));
            ring = rg.transform;
            ring.SetParent(root, false);
            ring.localRotation = Quaternion.Euler(90f, 0f, 0f);   // the ring mesh lies flat; stand it up to face the camera
            rg.GetComponent<MeshFilter>().sharedMesh = RingMesh.Create(0.8f, 1f);
            Setup(rg.GetComponent<MeshRenderer>());

            lines = new Transform[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                var l = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(l.GetComponent<Collider>());
                l.transform.SetParent(root, false);
                Setup(l.GetComponent<MeshRenderer>());
                lines[i] = l.transform;
            }
            root.gameObject.SetActive(false);

            foreach (PlayerController p in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                p.OnHitAttempt += r => { if (r.contact && (r.type == HitType.Spike || r.type == HitType.Serve)) Play(p.Ball.Position, r.charge); };
        }

        void Setup(MeshRenderer r)
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void Play(Vector3 at, float charge)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            root.position = at;
            root.rotation = cam.transform.rotation;      // local XY plane faces the camera
            size = baseRadius * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(charge));
            age = 0f;
            root.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (age > seconds) { if (root.gameObject.activeSelf) root.gameObject.SetActive(false); return; }
            age += Time.deltaTime;
            float k = Mathf.Clamp01(age / seconds);
            float eased = 1f - (1f - k) * (1f - k);                  // fast out, then settles
            mat.color = Palette.WithAlpha(Palette.CourtLines, 1f - k);

            float r = Mathf.Lerp(0.15f, size, eased);
            ring.localScale = new Vector3(r, r, r);
            for (int i = 0; i < lines.Length; i++)
            {
                float a = (i + 0.5f) / lines.Length * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                float inner = r * 1.15f, length = size * 0.6f * (1f - k * 0.5f);
                lines[i].localPosition = dir * (inner + length * 0.5f);
                lines[i].localRotation = Quaternion.FromToRotation(Vector3.forward, dir);
                lines[i].localScale = new Vector3(0.04f, 0.04f, length);
            }
        }
    }
}
