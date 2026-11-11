using Sandspike.Aim;
using Sandspike.Art;
using UnityEngine;

namespace Sandspike.Players
{
    /// <summary>
    /// A compact two-arc ring on the sand at a player's feet (replaces the corner bars):
    /// the OUTER arc is stamina, the INNER arc is the charge being built (jump, or the hit being wound up).
    /// Built from RingMesh arcs; each frame the arcs are rewritten to the current fractions.
    /// </summary>
    public class FeetRing : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] float outerInner = 0.78f, outerOuter = 0.92f;   // stamina arc radii
        [SerializeField] float innerInner = 0.58f, innerOuter = 0.72f;   // charge arc radii
        [SerializeField] float height = 0.06f;                           // above the sand (the other markers use 0.04 and 0.05)
        [SerializeField] float lowStamina = 0.4f;
        [SerializeField] float burnoutFlickerHz = 6f;

        Mesh staminaMesh, chargeMesh;
        Material staminaMat, chargeMat;

        void Start()
        {
            var trackMat = MakeMaterial(Palette.UiBarEmpty);
            MakePart("Stamina Track", RingMesh.Create(outerInner, outerOuter), trackMat, 0f);
            MakePart("Charge Track", RingMesh.Create(innerInner, innerOuter), trackMat, 0f);

            staminaMesh = new Mesh { name = "Stamina Arc" };
            chargeMesh = new Mesh { name = "Charge Arc" };
            staminaMat = MakeMaterial(Palette.Sea, true);
            chargeMat = MakeMaterial(Palette.Accent);
            MakePart("Stamina Arc", staminaMesh, staminaMat, 0.004f);
            MakePart("Charge Arc", chargeMesh, chargeMat, 0.008f);
        }

        /// <summary>An unlit URP material; 'transparent' lets the colour's alpha show (the burnout flicker, the serve preview).</summary>
        public static Material MakeMaterial(Color colour, bool transparent = false)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = colour };
            if (!transparent) return m;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        void MakePart(string partName, Mesh mesh, Material mat, float lift)
        {
            var go = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, lift, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void LateUpdate()
        {
            if (player == null || staminaMesh == null) return;
            Vector3 p = player.transform.position;
            transform.position = new Vector3(p.x, height, p.z);    // follows the feet, does not turn with the body

            float s = player.Stamina.Normalized;
            RingMesh.FillArc(staminaMesh, outerInner, outerOuter, s);
            Color staminaColour = s < lowStamina ? Palette.TeamA : Palette.Sea;
            // burned out: the arc flickers (alpha pulses about 6 times a second)
            float alpha = player.IsBurnedOut ? Mathf.Lerp(0.25f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * burnoutFlickerHz * 2f * Mathf.PI)) : 1f;
            staminaMat.color = Palette.WithAlpha(staminaColour, alpha);

            float charge = player.JumpCharge.IsCharging ? player.JumpCharge.Value : player.SpikeCharge.Value;
            RingMesh.FillArc(chargeMesh, innerInner, innerOuter, charge);
        }

        void OnDestroy()
        {
            if (staminaMesh != null) Destroy(staminaMesh);
            if (chargeMesh != null) Destroy(chargeMesh);
        }
    }
}
