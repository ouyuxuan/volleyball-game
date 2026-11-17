using System.Collections.Generic;
using Sandspike.CameraRig;
using Sandspike.Controls;
using Sandspike.Players;
using Sandspike.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandspike.Core
{
    /// <summary>
    /// Local multiplayer: up to four humans, each bound to its own device(s). Runs the join screen before the first serve,
    /// then gives every human a HumanBrain (restricted to their device) and a HumanView (their own camera in their share of the screen).
    /// Starts itself when the scene has a MatchManager, like FeelFx and AudioDirector.
    ///
    /// Devices: keyboard + mouse count as ONE device (PlayerSlots.KeyboardId), each gamepad is one. With exactly one human the brain
    /// listens to every device, so a lone player can still swap between keyboard and gamepad mid-match.
    /// </summary>
    public class LocalPlayers : MonoBehaviour
    {
        public PlayerSlots Slots { get; } = new PlayerSlots();

        MatchManager match;
        ViewTemplates templates;
        JoinScreen join;
        readonly List<HumanView> views = new List<HumanView>();
        GameObject spareCamera;
        readonly Dictionary<int, int> stickDir = new Dictionary<int, int>();   // per gamepad: -1 / 0 / +1, so one stick push moves one slot
        Canvas hudCanvas;                  // the shared score HUD: hidden while the join screen is up
        Shell shell;
        int[] savedSlots;                  // the join line-up, kept while Practice borrows the slots
        bool viewsBuilt, ready;

        /// <summary>True while the scene has local-player support: the MatchManager then waits on the join screen before the first serve.</summary>
        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindFirstObjectByType<LocalPlayers>() != null || FindFirstObjectByType<MatchManager>() == null) return;
            new GameObject("LocalPlayers").AddComponent<LocalPlayers>();
        }

        void Awake()
        {
            templates = ViewTemplates.Find();
            if (templates == null) { Destroy(gameObject); return; }
            Active = true;
            // The scene has one camera rig and one set of feedback objects (for a lone player). Switch them off before they start:
            // every human gets its own copy from HumanView.Create. (The camera stays on for the join screen.)
            foreach (GameObject g in templates.hideAtStart) g.SetActive(false);
            var cue = FindFirstObjectByType<HitCue>();
            if (cue != null) { cue.enabled = false; Destroy(cue); }
            foreach (var b in FindObjectsByType<HumanBrain>(FindObjectsSortMode.None)) b.enabled = false;
            templates.firstPerson.gameObject.SetActive(false);
            Slots.Join(PlayerSlots.KeyboardId);       // keyboard + mouse start in the first slot of team A
        }

        void OnDestroy() { Active = false; }      // leaving play mode must not leave the next match waiting on a join screen

        void Update()
        {
            if (match == null) match = FindFirstObjectByType<MatchManager>();
            if (match == null || match.Rules == null) return;

            if (!ready)
            {
                ready = true;
                join = JoinScreen.Create(templates.font);
                hudCanvas = FindFirstObjectByType<Hud>()?.GetComponent<Canvas>();
                shell = Shell.Create(this, match, join, templates.font);
            }

            ShellScreen now = shell.Flow.Current;
            if (hudCanvas != null) hudCanvas.enabled = match.State != MatchState.Setup && now != ShellScreen.Settings && now != ShellScreen.HowTo && now != ShellScreen.Camera && now != ShellScreen.Controls && now != ShellScreen.ConfirmReset && now != ShellScreen.Results && !match.Practice;   // these screens show the score themselves or cover it
            // Read before Tick, so the key that opened the Setup screen is not also read as a join / start press
            bool joining = shell.Flow.Current == ShellScreen.Setup, slots = shell.SlotsFocused;
            shell.Tick();
            if (match.State == MatchState.Setup)
            {
                DropUnplugged();
                if (joining && shell.Flow.Current == ShellScreen.Setup) TickJoin(slots);
                join.Refresh(Slots);
                return;
            }
            if (!viewsBuilt) Apply();           // somebody started the match without the join screen (a test, a script)
        }

        // ---------------- join screen input ----------------

        void DropUnplugged()
        {
            for (int s = 0; s < PlayerSlots.Count; s++)
            {
                int id = Slots.Owner(s);
                if (id > PlayerSlots.KeyboardId && InputSystem.GetDeviceById(id) == null) Slots.Leave(id);   // a gamepad was unplugged
            }
        }

        void TickJoin(bool slotsFocused)
        {
            bool start = false;
            Keyboard kb = Keyboard.current;
            if (kb != null && slotsFocused)
            {
                int id = PlayerSlots.KeyboardId;
                bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
                if (Slots.SlotOf(id) < 0)
                {
                    if (enter || kb.spaceKey.wasPressedThisFrame) Slots.Join(id);
                }
                else
                {
                    if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Slots.Move(id, -1);
                    if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Slots.Move(id, 1);
                    if (kb.backspaceKey.wasPressedThisFrame) Slots.Leave(id);
                    else if (enter) start = true;
                }
            }

            foreach (Gamepad pad in Gamepad.all)
            {
                int id = pad.deviceId;
                if (!slotsFocused) { if (Slots.SlotOf(id) >= 0 && pad.startButton.wasPressedThisFrame) start = true; continue; }   // options row: only Start counts
                if (Slots.SlotOf(id) < 0)
                {
                    if (pad.buttonSouth.wasPressedThisFrame) Slots.Join(id);
                    continue;
                }
                float x = pad.leftStick.x.ReadValue();
                int dir = x < -0.6f ? -1 : x > 0.6f ? 1 : 0;
                stickDir.TryGetValue(id, out int before);
                stickDir[id] = dir;
                if (pad.dpad.left.wasPressedThisFrame || (dir == -1 && before != -1)) Slots.Move(id, -1);
                if (pad.dpad.right.wasPressedThisFrame || (dir == 1 && before != 1)) Slots.Move(id, 1);
                if (pad.buttonEast.wasPressedThisFrame) Slots.Leave(id);
                else if (pad.startButton.wasPressedThisFrame) start = true;
            }

            if (start && Slots.CanStart) StartMatch();
        }

        // ---------------- starting and leaving ----------------

        /// <summary>Applies the Setup options, builds the views for the current line-up and starts the match.</summary>
        public void StartMatch()
        {
            shell?.ApplyOptions();
            for (int s = 0; s < PlayerSlots.Count; s++) match.SetSlotDifficulty(s, Slots.BotOverride(s));   // each bot's own level, if the Setup screen set one
            Apply();
            match.NewMatch();
            shell?.MatchStarted();
        }

        /// <summary>Practice: a solo court for one device (the one that pressed Practice). The join line-up is kept and comes back at ReturnToJoin.</summary>
        public void StartPractice(int deviceId)
        {
            savedSlots = Slots.Snapshot();
            Slots.Clear();
            Slots.Join(deviceId);                      // the first free slot: slot 0, team A
            Apply();
            match.StartPractice(Drill.Receive);
            shell?.MatchStarted();
        }

        // One brain and one view per human; every other slot is a bot.
        void Apply()
        {
            TearDown();
            var humanSlots = new List<int>();
            for (int s = 0; s < PlayerSlots.Count; s++) if (Slots.IsHuman(s)) humanSlots.Add(s);
            int n = humanSlots.Count;
            if (n == 0) { Slots.Join(PlayerSlots.KeyboardId); humanSlots.Add(Slots.SlotOf(PlayerSlots.KeyboardId)); n = 1; }   // a script started with nobody joined

            // the scene's camera and its Cinemachine cameras step aside: the views are clones of them
            templates.mainCamera.gameObject.SetActive(false);
            templates.thirdPerson.gameObject.SetActive(false);

            Rect[] rects = SplitScreen.Layout(n);
            for (int s = 0; s < PlayerSlots.Count; s++) match.Humans[s] = null;
            for (int i = 0; i < n; i++)
            {
                int slot = humanSlots[i];
                PlayerController p = match.Players[slot];
                HumanBrain brain = p.GetComponent<HumanBrain>();
                if (brain == null) brain = p.gameObject.AddComponent<HumanBrain>();
                int id = Slots.Owner(slot);
                DeviceScope scope = n == 1 ? DeviceScope.Any : id == PlayerSlots.KeyboardId ? DeviceScope.Keyboard : DeviceScope.Pad;
                var pad = InputSystem.GetDeviceById(id) as Gamepad;
                match.Humans[slot] = brain;
                views.Add(HumanView.Create(templates, match, slot, rects[i], i == 0, brain, scope, pad));
            }
            for (int s = 0; s < PlayerSlots.Count; s++) if (!match.IsHumanSlot(s)) match.BotFor(s);   // every other slot has a bot ready
            match.ApplyBrains();
            if (n == 3) MakeSpareView(rects[3]);
            viewsBuilt = true;
        }

        // With three humans the fourth quarter shows a neutral wide view of the court (no one's feedback objects).
        void MakeSpareView(Rect rect)
        {
            spareCamera = Instantiate(templates.mainCamera.gameObject);       // the scene camera is off, so the clone starts off
            spareCamera.name = "Spare View Camera";
            spareCamera.tag = "Untagged";
            DestroyImmediate(spareCamera.GetComponent<CinemachineBrain>());
            DestroyImmediate(spareCamera.GetComponent<AudioListener>());
            var cam = spareCamera.GetComponent<Camera>();
            cam.rect = rect;
            cam.cullingMask = ViewLayers.NeutralMask;
            spareCamera.transform.SetPositionAndRotation(new Vector3(0f, 12f, -14f), Quaternion.LookRotation(new Vector3(0f, -12f, 14f)));
            spareCamera.SetActive(true);
        }

        void TearDown()
        {
            foreach (HumanView v in views) if (v != null) Destroy(v.gameObject);
            views.Clear();
            if (spareCamera != null) Destroy(spareCamera);
            foreach (var b in FindObjectsByType<HumanBrain>(FindObjectsSortMode.None)) b.enabled = false;
            for (int s = 0; s < PlayerSlots.Count; s++) match.Humans[s] = null;
            templates.mainCamera.gameObject.SetActive(true);                  // the scene camera shows the join screen's court
            templates.thirdPerson.gameObject.SetActive(true);
            viewsBuilt = false;
        }

        /// <summary>Back to the join screen with the same line-up (Results: Change Players or Title; Pause: Back to Title). The Shell picks the screen.</summary>
        public void ReturnToJoin()
        {
            match.EndPractice();
            if (savedSlots != null) { Slots.Restore(savedSlots); savedSlots = null; }
            TearDown();
            match.ApplyBrains();
            match.EnterSetup();
        }

        /// <summary>Results: Play Again with the same line-up and options.</summary>
        public void PlayAgain() => match.NewMatch();
    }
}
