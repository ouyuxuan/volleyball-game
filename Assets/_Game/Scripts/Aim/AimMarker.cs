using UnityEngine;

namespace Sandspike.Aim
{
    /// <summary>A ring on the sand at the aim point, shown in both camera modes. Gently pulses. For a gamepad player the point includes aim assist.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class AimMarker : MonoBehaviour
    {
        IAimSource source;                       // the human brain: its AimPoint already includes any aim assist
        [SerializeField] float radius = 0.55f;
        [SerializeField] float ringThickness = 0.2f;
        [SerializeField] float height = 0.05f;
        [SerializeField] float pulseAmount = 0.08f;
        [SerializeField] float pulseHz = 1.5f;

        void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = RingMesh.Create(1f - ringThickness, 1f);
            var r = GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public void Bind(IAimSource s) { source = s; }

        void LateUpdate()
        {
            if (source == null) return;
            Vector3 p = source.AimPoint;
            transform.position = new Vector3(p.x, height, p.z);
            float s = radius * (1f + Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f) * pulseAmount);
            transform.localScale = new Vector3(s, 1f, s);
        }
    }
}
