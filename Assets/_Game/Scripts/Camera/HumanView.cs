using Sandspike.Aim;
using Sandspike.Art;
using Sandspike.Ball;
using Sandspike.Controls;
using Sandspike.Core;
using Sandspike.Feel;
using Sandspike.Players;
using Sandspike.UI;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sandspike.CameraRig
{
    /// <summary>The scene's single-player camera rig and HUD pieces. Each human's view is cloned from these.</summary>
    public class ViewTemplates
    {
        public Camera mainCamera;
        public CinemachineCamera thirdPerson, firstPerson;
        public Material aimMaterial;
        public GameObject controlsHint, crosshair, arrow;
        public CanvasScaler scaler;
        public TMP_FontAsset font;
        public BallController ball;
        public GameObject[] hideAtStart;    // everything per-human that the scene has once: switched off, the views make their own

        public static ViewTemplates Find()
        {
            var rig = Object.FindFirstObjectByType<PlayerCameraRig>(FindObjectsInactive.Include);
            var hint = Object.FindFirstObjectByType<ControlsHint>(FindObjectsInactive.Include);
            var indicator = Object.FindFirstObjectByType<BallIndicator>(FindObjectsInactive.Include);
            var aim = Object.FindFirstObjectByType<AimMarker>(FindObjectsInactive.Include);
            if (rig == null || hint == null || indicator == null || aim == null) return null;
            Canvas canvas = hint.GetComponentInParent<Canvas>();
            var t = new ViewTemplates
            {
                mainCamera = rig.CinemachineBrainRef.GetComponent<Camera>(),
                thirdPerson = rig.ThirdPersonCamera,
                firstPerson = rig.FirstPersonCamera,
                aimMaterial = aim.GetComponent<MeshRenderer>().sharedMaterial,
                controlsHint = hint.gameObject,
                crosshair = canvas.transform.Find("Crosshair").gameObject,
                arrow = indicator.Arrow.gameObject,
                scaler = canvas.GetComponent<CanvasScaler>(),
                font = Object.FindFirstObjectByType<Hud>()?.Font,
                ball = Object.FindFirstObjectByType<BallController>(),
            };
            t.hideAtStart = new[]
            {
                rig.gameObject, aim.gameObject, Object.FindFirstObjectByType<FeetRing>(FindObjectsInactive.Include).gameObject,
                Object.FindFirstObjectByType<PlayerMarker>(FindObjectsInactive.Include).gameObject,
                Object.FindFirstObjectByType<ServePreview>(FindObjectsInactive.Include).gameObject,
                t.controlsHint, t.crosshair, t.arrow,
            };
            indicator.enabled = false;
            return t;
        }
    }

    /// <summary>
    /// One human's view of the match: a camera in its share of the screen (clone of the scene's camera, one Cinemachine channel each),
    /// the aim marker, foot ring, cues and charge ring that only this camera shows (a layer per human), and a HUD canvas with the
    /// control hints, serve prompt, 1st person crosshair and ball arrow. Built in code by LocalPlayers; destroying it removes everything.
    /// </summary>
    public class HumanView : MonoBehaviour
    {
        public PlayerController Player { get; private set; }
        public HumanBrain Brain { get; private set; }
        public PlayerCameraRig Rig { get; private set; }
        public Camera Cam { get; private set; }

        MatchManager match;
        GameObject fpCamera, crosshair;
        HitCue cue;
        PlayerVisual visual;
        TMP_Text servePrompt;
        string promptShown;

        /// <summary>Builds one human's view. The scene's original camera and HUD pieces must already be switched off (the clones start off too).</summary>
        public static HumanView Create(ViewTemplates t, MatchManager match, int slot, Rect rect, bool primary, HumanBrain brain, DeviceScope scope, Gamepad pad)
        {
            PlayerController player = match.Players[slot];
            int layer = ViewLayers.For(slot);
            var channel = (OutputChannels)(2 << slot);            // Channel01.. : each camera only follows its own Cinemachine cameras
            float side = player.CourtSide;

            var go = new GameObject($"View P{slot + 1}");
            var v = go.AddComponent<HumanView>();
            v.match = match; v.Player = player; v.Brain = brain;
            GameObject Child(string n, GameObject parent, int lyr = 0) { var c = new GameObject(n) { layer = lyr }; c.transform.SetParent(parent.transform, false); return c; }

            // ---- camera (a clone of the scene's) ----
            GameObject camGo = Instantiate(t.mainCamera.gameObject, go.transform);
            camGo.name = "Camera";
            camGo.tag = primary ? "MainCamera" : "Untagged";
            Camera cam = camGo.GetComponent<Camera>();
            cam.rect = rect;
            cam.cullingMask = ViewLayers.MaskFor(slot);
            var cb = camGo.GetComponent<CinemachineBrain>();
            cb.ChannelMask = channel;
            if (!primary) DestroyImmediate(camGo.GetComponent<AudioListener>());   // only one listener in the scene
            v.Cam = cam;

            CinemachineCamera tp = Instantiate(t.thirdPerson.gameObject, go.transform).GetComponent<CinemachineCamera>();
            tp.OutputChannel = channel;
            tp.Follow = player.transform;
            if (side > 0f)      // the far side of the net: same camera, turned around
            {
                var follow = tp.GetComponent<CinemachineFollow>();
                follow.FollowOffset = new Vector3(follow.FollowOffset.x, follow.FollowOffset.y, -follow.FollowOffset.z);
                tp.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * tp.transform.rotation;
            }
            CinemachineCamera fp = Instantiate(t.firstPerson.gameObject, player.HeadTarget).GetComponent<CinemachineCamera>();
            fp.OutputChannel = channel;
            v.fpCamera = fp.gameObject;

            // ---- aim + rig ----
            GameObject rigGo = Child("Camera Rig", go);
            var cursor = rigGo.AddComponent<CursorAim>();
            var cross = rigGo.AddComponent<CrosshairAim>();
            cursor.Setup(cam, side);
            cross.Setup(fp.transform);
            var rig = rigGo.AddComponent<PlayerCameraRig>();
            v.visual = player.GetComponentInChildren<PlayerVisual>();
            rig.Setup(brain, v.visual, cb, tp, fp, player.HeadTarget, cursor, cross, side);
            v.Rig = rig;
            brain.Bind(match, rig, scope, pad);

            // ---- things only this human sees (their own layer) ----
            GameObject own = Child("Own Feedback", go, layer);
            GameObject aimGo = Child("Aim Marker", own, layer);
            var aim = aimGo.AddComponent<AimMarker>();
            aimGo.GetComponent<MeshRenderer>().sharedMaterial = t.aimMaterial;
            aim.Bind(brain);
            Child("Feet Ring", own, layer).AddComponent<FeetRing>().Bind(player);
            Child("Player Marker", own, layer).AddComponent<PlayerMarker>().Bind(player);
            Child("Serve Preview", own, layer).AddComponent<ServePreview>().Bind(player);
            v.cue = player.gameObject.AddComponent<HitCue>();
            v.cue.Bind(cam, layer);
            Child("Charge Fx", own, layer).AddComponent<SpikeChargeFx>().Bind(player, v.cue, cam);
            Child("Hit Burst", own, layer).AddComponent<HitBurst>().Bind(cam);

            // ---- HUD canvas drawn only in this camera's share of the screen ----
            GameObject hud = Child("View HUD", go);
            var canvas = hud.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = hud.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = t.scaler.uiScaleMode;
            scaler.referenceResolution = t.scaler.referenceResolution;
            scaler.screenMatchMode = t.scaler.screenMatchMode;
            scaler.matchWidthOrHeight = t.scaler.matchWidthOrHeight;

            GameObject hint = Instantiate(t.controlsHint, hud.transform, false);
            hint.GetComponent<ControlsHint>().Bind(brain);
            if (rect.x >= 0.5f)     // a view on the right half: its top-left corner is under the centred score, so use its top-right
            {
                var hr = (RectTransform)hint.transform;
                hr.anchorMin = new Vector2(1f, hr.anchorMin.y); hr.anchorMax = new Vector2(1f, hr.anchorMax.y);
                hr.pivot = new Vector2(1f, hr.pivot.y);
                hr.anchoredPosition = new Vector2(-hr.anchoredPosition.x, hr.anchoredPosition.y);
            }
            hint.SetActive(true);
            v.crosshair = Instantiate(t.crosshair, hud.transform, false);
            GameObject arrow = Instantiate(t.arrow, hud.transform, false);
            hud.AddComponent<BallIndicator>().Bind(t.ball, rig, cam, (RectTransform)arrow.transform);
            v.servePrompt = MakeServePrompt(hud.transform, t.font);

            camGo.SetActive(true);
            tp.gameObject.SetActive(true);
            fp.gameObject.SetActive(true);
            return v;
        }

        // The "Your serve: hold [Spike] to toss" line, bottom centre of this view.
        static TMP_Text MakeServePrompt(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Serve Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 150f);
            rt.sizeDelta = new Vector2(1000f, 60f);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = 34f;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 2f;
            text.outlineWidth = 0.2f;
            text.outlineColor = (Color32)Palette.Shadow;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Palette.CourtLines;
            text.raycastTarget = false;
            text.text = "";
            return text;
        }

        void Update()
        {
            if (Rig == null || match.Rules == null) return;
            if (crosshair.activeSelf != Rig.IsFirstPerson) crosshair.SetActive(Rig.IsFirstPerson);

            string text = "";
            if (match.State == MatchState.Serve && match.Server == Player && Player.Brain is HumanBrain)
                text = Player.Tossed
                    ? "Jump and hit!"
                    : $"Your serve: hold <color=#{ColorUtility.ToHtmlStringRGB(Palette.Accent)}>{ControlPrompts.Label(PromptAction.Spike, Brain.Style)}</color> to toss   {Mathf.CeilToInt(match.ServeClockLeft)}";
            if (text != promptShown) { promptShown = text; servePrompt.text = text; }
        }

        void OnDestroy()
        {
            if (fpCamera != null) Destroy(fpCamera);
            if (cue != null) Destroy(cue);
            if (visual != null && Rig != null && Rig.IsFirstPerson) visual.SetFirstPerson(false);   // never leave a body hidden
            if (Brain != null && Brain.Scope != DeviceScope.Pad) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
}
