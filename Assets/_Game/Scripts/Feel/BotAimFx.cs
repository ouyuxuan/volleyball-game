using Sandspike.Aim;
using Sandspike.Art;
using Sandspike.Core;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Feel
{
    /// <summary>
    /// A small ring in the team colour on the sand where a bot means to send the ball (its AimPoint), shown only while that bot
    /// has a planned hit for the ball in the air. So you can read a teammate's pass or set before it is hit.
    /// The human's own aim marker is a different object and is left alone.
    /// </summary>
    public class BotAimFx : MonoBehaviour
    {
        [SerializeField] float radius = 0.35f;
        [SerializeField] float height = 0.05f;       // above the sand (other markers use 0.04 to 0.06)
        [SerializeField, Range(0f, 1f)] float alpha = 0.6f;

        MatchManager match;
        Transform[] rings;

        void Start()
        {
            match = FindFirstObjectByType<MatchManager>();
            if (match == null) { enabled = false; return; }
            Mesh mesh = RingMesh.Create(0.7f, 1f);
            rings = new Transform[match.Players.Length];
            for (int i = 0; i < rings.Length; i++)
            {
                var go = new GameObject("BotAim " + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(radius, 1f, radius);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = FeetRing.MakeMaterial(Palette.WithAlpha(match.Players[i].Team == 0 ? Palette.TeamA : Palette.TeamB, alpha), true);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                rings[i] = go.transform;
            }
        }

        void LateUpdate()
        {
            for (int i = 0; i < rings.Length; i++)
            {
                var bot = match.Players[i].Brain as SimpleBotBrain;
                bool show = bot != null && bot.HasPlan;
                if (rings[i].gameObject.activeSelf != show) rings[i].gameObject.SetActive(show);
                if (show) rings[i].position = new Vector3(bot.AimPoint.x, height, bot.AimPoint.z);
            }
        }
    }
}
