using Sandspike.Art;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Feel
{
    /// <summary>
    /// For every player: a thin white streak from the sand up to the feet while airborne after a jump (so jump height is
    /// readable), and sand-coloured dust puffs on takeoff, landing, dive start, dive end and (lighter) while sprinting.
    /// </summary>
    public class JumpFx : MonoBehaviour
    {
        [SerializeField] float streakWidth = 0.07f;
        [SerializeField] float streakAlpha = 0.85f;
        [SerializeField] float streakFadeSeconds = 0.1f;
        [SerializeField] int puffMin = 6, puffMax = 10;      // particles per puff
        [SerializeField] float sprintPuffSeconds = 0.25f;

        class Track
        {
            public PlayerController player;
            public Transform streak;
            public Material streakMat;
            public bool airborne, wasDiving;
            public float alpha, sprintClock;
        }

        Track[] tracks;
        ParticleSystem dust;

        void Start()
        {
            BuildDust();
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            tracks = new Track[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                var t = new Track { player = players[i] };
                t.streakMat = FeetRing.MakeMaterial(Palette.WithAlpha(Palette.CourtLines, 0f), true);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.name = "JumpStreak";
                go.transform.SetParent(transform, false);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = t.streakMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                t.streak = go.transform;
                tracks[i] = t;

                players[i].OnJumped += p => { t.airborne = true; Puff(p.transform.position, 1f); };
                players[i].OnLanded += p => { t.airborne = false; Puff(p.transform.position, 1f); };
                players[i].OnDiveEnded += p => Puff(p.transform.position, 1f);
            }
        }

        void Update()
        {
            if (tracks == null) return;
            foreach (Track t in tracks)
            {
                PlayerController p = t.player;
                Vector3 pos = p.transform.position;

                // dive start (there is no event for it) and sprint puffs
                if (p.IsDiving && !t.wasDiving) Puff(pos, 1f);
                t.wasDiving = p.IsDiving;
                t.sprintClock = p.IsSprinting ? t.sprintClock + Time.deltaTime : 0f;
                if (t.sprintClock >= sprintPuffSeconds) { t.sprintClock = 0f; Puff(pos, 0.5f); }

                // streak: fades in over 0.1 s in the air, out over 0.1 s once landed
                bool want = t.airborne && !p.IsGrounded;
                t.alpha = Mathf.MoveTowards(t.alpha, want ? streakAlpha : 0f, streakAlpha / streakFadeSeconds * Time.deltaTime);
                bool show = t.alpha > 0.001f;
                if (t.streak.gameObject.activeSelf != show) t.streak.gameObject.SetActive(show);
                if (!show) continue;
                float h = Mathf.Max(0.02f, pos.y);
                t.streak.position = new Vector3(pos.x, h * 0.5f, pos.z);
                t.streak.localScale = new Vector3(streakWidth, h, streakWidth);
                t.streakMat.color = Palette.WithAlpha(Palette.CourtLines, t.alpha);
            }
        }

        void BuildDust()
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            dust = go.AddComponent<ParticleSystem>();
            var main = dust.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.4f;
            main.startSize = 0.35f;
            main.startColor = Palette.WithAlpha(Palette.Dust, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var emission = dust.emission; emission.enabled = false;     // we emit by hand (Emit)
            var shape = dust.shape; shape.enabled = false;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Palette.Dust, 0f), new GradientColorKey(Palette.Dust, 1f) },
                         new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            var col = dust.colorOverLifetime; col.enabled = true; col.color = fade;
            var size = dust.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.4f)));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = FeelFx.ParticleMaterial();
            dust.Play();
        }

        // A burst of 6 to 10 soft puffs (scaled by 'amount') thrown outward along the sand around 'feet'.
        void Puff(Vector3 feet, float amount)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Random.Range(puffMin, puffMax + 1) * amount));
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized;
                var e = new ParticleSystem.EmitParams
                {
                    position = new Vector3(feet.x, 0.08f, feet.z) + new Vector3(d.x, 0f, d.y) * 0.15f,
                    velocity = new Vector3(d.x, 0.5f, d.y) * Random.Range(0.8f, 1.6f),
                };
                dust.Emit(e, 1);
            }
        }
    }
}
