using System.Collections.Generic;
using Sandspike.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sandspike.Players
{
    /// <summary>
    /// Shows a rigged low-poly character instead of the primitive body.
    /// Sits on the same object as PlayerVisual, so the model inherits its dive lean, jump crouch and squash for free.
    /// - Spawns one of the character models, scales it to player height and colours it from the Palette.
    /// - Feeds the Animator (idle / run / jump clips that come with the model).
    /// - After the Animator runs, aims the arm bones into the bump / set / spike poses.
    /// Gameplay is untouched: contact points live in PlayerController, this only draws.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] bool useCharacterModel = true;     // off = the old primitive body
        [SerializeField] GameObject[] models;               // character FBX models (all share one skeleton)
        [SerializeField] RuntimeAnimatorController controller;
        [SerializeField] float height = 1.85f;              // metres, feet to top of head

        [Header("Poses")]
        [SerializeField] float poseBlendSeconds = 0.1f;     // how fast the arms move into and out of a hit pose
        [SerializeField] float swingSeconds = 0.12f;        // keep in step with PlayerVisual
        [SerializeField] float bumpArmsInward = 0.35f;      // how much the arms close together for a bump
        [SerializeField] float setHandsInward = 0.45f;      // hands meet above the forehead for a set
        [SerializeField] float diveArmDegrees = -150f;      // arms reach ahead in a dive
        [SerializeField] float bumpForwardDegrees = 35f;    // bump platform: arms at least this far forward of hanging
        [SerializeField] float setRaiseDegrees = 20f;       // set: hands this much higher than the shared pose

        Animator animator;
        Transform upperL, lowerL, fistL, upperR, lowerR, fistR;
        float poseWeight;
        bool spawned;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();

        void Update()
        {
            if (!useCharacterModel || player == null) return;
            if (!spawned) Spawn();                          // first frame: PlayerId and team are set by then
            if (animator == null) return;
            animator.SetFloat(SpeedId, player.Speed01, 0.1f, Time.deltaTime);
            animator.SetBool(GroundedId, player.IsGrounded && !player.IsDiving);
        }

        void Spawn()
        {
            spawned = true;
            if (models == null || models.Length == 0) return;

            // Hide the primitive body (kept in the prefab so the old look is one toggle away).
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            int variant = Mathf.Abs(player.PlayerId) % models.Length;
            GameObject model = Instantiate(models[variant], transform);
            model.name = "Character Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null) return;
            skin.updateWhenOffscreen = true;                // poses move the arms outside the import-time bounds
            model.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, skin.bounds.size.y / transform.lossyScale.y));

            // One flat palette colour per body part. Slots are matched by the material names in the model.
            bool teamA = player.Team == 0;
            bool altLook = (player.PlayerId / 2 + player.PlayerId) % 2 == 1;   // vary skin and hair between players
            var slots = skin.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                string n = slots[i] != null ? slots[i].name : "";
                Color c = n.StartsWith("Shirt") ? (teamA ? Palette.TeamA : Palette.TeamB)
                        : n.StartsWith("Skin") ? (altLook ? Palette.SkinDeep : Palette.Skin)
                        : n.StartsWith("Pants") ? Palette.Shorts
                        : n.StartsWith("Belt") ? Palette.Belt
                        : n.StartsWith("Face") ? Palette.Eyes
                        : (altLook ? Palette.HairDark : Palette.HairLight);
                slots[i] = Flat(c);
            }
            skin.sharedMaterials = slots;

            animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            upperL = Find(model.transform, "UpperArm.L"); lowerL = Find(model.transform, "LowerArm.L"); fistL = Find(model.transform, "Fist.L");
            upperR = Find(model.transform, "UpperArm.R"); lowerR = Find(model.transform, "LowerArm.R"); fistR = Find(model.transform, "Fist.R");
        }

        // Runs after the Animator has posed the skeleton, so our arm aim wins.
        void LateUpdate()
        {
            if (animator == null || upperL == null || upperR == null) return;

            bool swinging = Time.time - player.LastHitTime < swingSeconds;
            bool hitPose = swinging || player.WindingUp;
            bool diving = player.DiveAmount > 0.1f;
            poseWeight = Mathf.MoveTowards(poseWeight, hitPose || diving ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, poseBlendSeconds));
            if (poseWeight <= 0f) return;

            HitType type = swinging ? player.LastReport.type : player.WindUpType;
            float left, right;
            if (hitPose) PlayerVisual.ArmPose(type, swinging ? 0f : player.SpikeCharge.Value, swinging, out left, out right);
            else { left = right = diveArmDegrees; type = HitType.Bump; }

            // The shared pose table was tuned for the primitive body. On this model a bump needs the arms held
            // forward as a platform (never hanging down), and a set needs the hands a little higher.
            if (hitPose && type == HitType.Bump) left = right = Mathf.Min(left - bumpForwardDegrees, -bumpForwardDegrees);
            if (hitPose && type == HitType.Set) { left -= setRaiseDegrees; right -= setRaiseDegrees; }

            float inward = !hitPose ? 0.15f : type == HitType.Bump ? bumpArmsInward : type == HitType.Set ? setHandsInward : 0f;
            bool bendElbows = hitPose && (type == HitType.Set || (!swinging && type != HitType.Bump));   // set, and the cocked spike arm
            AimArm(upperL, lowerL, fistL, left, +inward, bendElbows && type == HitType.Set);
            AimArm(upperR, lowerR, fistR, right, -inward, bendElbows);
        }

        // Points an arm along 'degrees' (0 = hanging down, -90 = forward, -180 = straight up), measured on the body.
        // 'inward' leans it toward the body's centre line; a bent elbow turns the forearm up toward the head.
        void AimArm(Transform upper, Transform lower, Transform fist, float degrees, float inward, bool bendElbow)
        {
            Quaternion body = transform.rotation;
            Vector3 dir = body * (Quaternion.Euler(degrees, 0f, 0f) * Vector3.down + Vector3.right * inward).normalized;
            Aim(upper, lower, dir);
            if (lower == null || fist == null) return;
            Vector3 fore = bendElbow ? (dir + body * (Vector3.up * 1.2f + Vector3.right * inward)).normalized : dir;
            Aim(lower, fist, fore);
        }

        // Turns 'bone' so that the line from it to 'child' points along 'dir', blended by the pose weight.
        void Aim(Transform bone, Transform child, Vector3 dir)
        {
            if (bone == null || child == null) return;
            Vector3 current = (child.position - bone.position).normalized;
            Quaternion target = Quaternion.FromToRotation(current, dir) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, target, poseWeight);
        }

        // A matte material in one palette colour, shared by every part that uses that colour.
        static Material Flat(Color c)
        {
            if (materialCache.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial) { name = "Character " + ColorUtility.ToHtmlStringRGB(c) };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.15f);
            materialCache[c] = m;
            return m;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
