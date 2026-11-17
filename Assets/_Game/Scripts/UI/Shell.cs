using System;
using System.Collections.Generic;
using Sandspike.Art;
using Sandspike.Audio;
using Sandspike.Controls;
using Sandspike.Core;
using Sandspike.Players;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

namespace Sandspike.UI
{
    /// <summary>
    /// The menus around the match: Title, Setup (the join screen plus match options), Pause, Settings, How to Play, Results.
    /// One screen shows at a time and they cross-fade in 0.25 s. ScreenFlow decides where each screen may go; this class draws them,
    /// reads MenuInput (keyboard, gamepad, mouse) and freezes the game while paused. LocalPlayers creates it and calls Tick every frame.
    /// Built in code, all colours from Palette.
    /// </summary>
    public class Shell : MonoBehaviour
    {
        // One line of a menu: a button, or a choice / slider changed with left and right. 'cell' rows are the small boxes of the Setup options.
        class Row
        {
            public Func<string> value;       // text on the right (null for a plain button)
            public Action<int> step;         // left / right (-1 / +1), or null
            public Action activate;          // confirm; when null, confirm steps +1
            public Func<float> bar;          // 0..1 slider bar under the value, or null
            public RectTransform rt;
            public Image back, fill, track;
            public TMP_Text labelT, valueT;
        }

        class Page
        {
            public GameObject go;
            public CanvasGroup group;
            public TMP_Text titleT, bodyT, hintT;
            public readonly List<Row> rows = new List<Row>();
            public int sel;
            public float nextY;
            public float pitch = 100f, height = 84f;     // row spacing and row box height (the long Settings page packs them tighter)
        }

        const int BotRow = 5;        // index of the first small "BOT 1" box in the Setup options (the five big boxes come first)
        const float FadePerSecond = 4f;      // alpha per second: a cross-fade takes 0.25 s
        static readonly string[] Levels = { "Easy", "Normal", "Hard" };
        static readonly int[] Points = { 11, 21, 25 };
        static readonly string[] SetsChoices = { "1 set", "Best of 3" };
        static readonly string[] Shoulders = { "Left", "Centre", "Right" };

        public ScreenFlow Flow { get; } = new ScreenFlow();
        public GameSettings Settings => GameSettings.Current;
        /// <summary>True when the join slots (not the options row) have the focus on the Setup screen: LocalPlayers then reads the join buttons.</summary>
        public bool SlotsFocused => zone == 0;

        readonly MenuInput input = new MenuInput();
        readonly Dictionary<ShellScreen, Page> pages = new Dictionary<ShellScreen, Page>();
        readonly ISettingsStore store = new PlayerPrefsStore();
        LocalPlayers local;
        MatchManager match;
        JoinScreen join;
        TMP_FontAsset font;
        AudioDirector audio;
        ShellScreen shown = ShellScreen.Title;
        PromptStyle hintStyle = (PromptStyle)(-1), howStyle;
        int diff = 1, pts, sets, zone, howPage;
        bool cap, frozen;
        float savedScale = 1f;
        // Controls screen: which scheme it edits, and the rebinding in progress (if any)
        bool controlsPad;
        PromptStyle controlsStyle;
        InputActionRebindingExtensions.RebindingOperation rebind;
        string listeningLabel;
        int listenEndedFrame = -1;

        public static Shell Create(LocalPlayers local, MatchManager match, JoinScreen join, TMP_FontAsset font)
        {
            var go = new GameObject("Shell", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;                                   // above the join screen
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var s = go.AddComponent<Shell>();
            s.local = local; s.match = match; s.join = join; s.font = font;
            s.Build();
            return s;
        }

        // ---------------- building the screens ----------------

        void Build()
        {
            Settings.Load(store);
            ControlBindings.Load(store);
            ApplyFullscreen();
            diff = Mathf.Clamp((int)match.difficulty, 0, 2);
            pts = Mathf.Max(0, Array.IndexOf(Points, match.TargetScore));

            var title = NewPage(ShellScreen.Title, "SANDSPIKE", 300f, 190f, Palette.Accent, 0.35f, 80f);
            AddRow(title, "Play", null, null, () => Flow.Go(ShellScreen.Setup));
            AddRow(title, "Practice", null, null, () => local.StartPractice(input.confirmDevice));     // a solo court for the device that pressed it
            AddRow(title, "How to Play", null, null, () => Flow.Go(ShellScreen.HowTo));
            AddRow(title, "Settings", null, null, () => Flow.Go(ShellScreen.Settings));
            AddRow(title, "Quit", null, null, Quit);

            var pause = NewPage(ShellScreen.Pause, "PAUSED", 300f, 110f, Palette.CourtLines, 0.6f, 120f);
            AddRow(pause, "Resume", null, null, () => Flow.Go(ShellScreen.Match));
            AddRow(pause, "Settings", null, null, () => Flow.Go(ShellScreen.Settings));
            AddRow(pause, "How to Play", null, null, () => Flow.Go(ShellScreen.HowTo));
            AddRow(pause, "Back to Title", null, null, () => Flow.Go(ShellScreen.Confirm));

            // Practice's pause menu: no "Are you sure?" (there is no match to lose)
            var practice = NewPage(ShellScreen.PracticePause, "PRACTICE", 300f, 110f, Palette.CourtLines, 0.6f, 120f);
            AddRow(practice, "Resume", null, null, () => Flow.Go(ShellScreen.Match));
            AddRow(practice, "Drill", () => $"< {PracticeDrills.Names[(int)match.PracticeDrill]} >", d => match.SetDrill(PracticeDrills.Next(match.PracticeDrill, d)), null);
            AddRow(practice, "Settings", null, null, () => Flow.Go(ShellScreen.Settings));
            AddRow(practice, "How to Play", null, null, () => Flow.Go(ShellScreen.HowTo));
            AddRow(practice, "Back to Title", null, null, () => { Flow.Go(ShellScreen.Title); local.ReturnToJoin(); });

            var confirm = NewPage(ShellScreen.Confirm, "BACK TO TITLE?", 280f, 100f, Palette.CourtLines, 0.7f, 20f);
            AddBody(confirm, "This ends the match. Are you sure?", 44f, 150f, 1200f, 80f, TextAlignmentOptions.Center);
            AddRow(confirm, ScreenFlow.ConfirmChoices[0], null, null, () => Flow.Back());                                 // No (the default)
            AddRow(confirm, ScreenFlow.ConfirmChoices[1], null, null, () => { Flow.Go(ShellScreen.Title); local.ReturnToJoin(); });   // Yes

            var settings = NewPage(ShellScreen.Settings, "SETTINGS", 420f, 80f, Palette.CourtLines, 0.7f, 320f);
            settings.pitch = 72f; settings.height = 64f;
            AddRow(settings, "Music volume", () => Settings.MusicPercent + "%", d => { Settings.MusicPercent += d * GameSettings.Step; Save(); }, null, () => Settings.MusicPercent / 100f);
            AddRow(settings, "Effects volume", () => Settings.EffectsPercent + "%", d => { Settings.EffectsPercent += d * GameSettings.Step; Save(); }, null, () => Settings.EffectsPercent / 100f);
            AddRow(settings, "Aim assist for gamepads", () => OnOff(Settings.AimAssist), d => { Settings.AimAssist = !Settings.AimAssist; Save(); }, null);
            AddRow(settings, "Fullscreen", () => OnOff(Settings.Fullscreen), d => { Settings.Fullscreen = !Settings.Fullscreen; Save(); }, null);
            AddRow(settings, "Landing ring", () => OnOff(Settings.LandingRing), d => { Settings.LandingRing = !Settings.LandingRing; Save(); }, null);
            AddRow(settings, "Ball trail", () => OnOff(Settings.BallTrail), d => { Settings.BallTrail = !Settings.BallTrail; Save(); }, null);
            AddRow(settings, "IN / OUT label", () => OnOff(Settings.InOutLabel), d => { Settings.InOutLabel = !Settings.InOutLabel; Save(); }, null);
            AddRow(settings, "Camera", null, null, () => Flow.Go(ShellScreen.Camera));
            AddRow(settings, "Controls", null, null, OpenControls);
            AddRow(settings, "Reset to defaults", null, null, () => { Settings.ResetToDefaults(); Save(); });
            AddRow(settings, "Back", null, null, () => Flow.Back());

            var camera = NewPage(ShellScreen.Camera, "CAMERA", 400f, 80f, Palette.CourtLines, 0.7f, 280f);
            AddRow(camera, "Height", () => $"< {Settings.CameraHeight:0.0} m >", d => { Settings.CameraHeight += d * 0.5f; Save(); }, null);
            AddRow(camera, "Distance", () => $"< {Settings.CameraDistance:0.0} m >", d => { Settings.CameraDistance += d * 0.5f; Save(); }, null);
            AddRow(camera, "Field of view", () => $"< {Settings.CameraFov:0} >", d => { Settings.CameraFov += d * 5f; Save(); }, null);
            AddRow(camera, "Shoulder", () => $"< {Shoulders[Settings.CameraShoulder + 1]} >", d => { Settings.CameraShoulder = (Settings.CameraShoulder + 1 + d + 3) % 3 - 1; Save(); }, null);
            AddRow(camera, "Back", null, null, () => Flow.Back());

            // Controls: the lines are made when it opens (they depend on the device that opened it: keyboard + mouse, or a gamepad)
            var controls = NewPage(ShellScreen.Controls, "CONTROLS", 440f, 70f, Palette.CourtLines, 0.8f, 330f);
            controls.pitch = 52f; controls.height = 46f;
            AddBody(controls, "Confirm on a line, then press the new key or button.   Esc / Start cancels.   A key already in use swaps places.", 26f, -420f, 1800f, 40f, TextAlignmentOptions.Center);

            var reset = NewPage(ShellScreen.ConfirmReset, "RESET CONTROLS?", 280f, 100f, Palette.CourtLines, 0.8f, 20f);
            AddBody(reset, "Every control of this device goes back to its default. Are you sure?", 44f, 150f, 1400f, 120f, TextAlignmentOptions.Center);
            AddRow(reset, ScreenFlow.ConfirmChoices[0], null, null, () => Flow.Back());                                 // No (the default)
            AddRow(reset, ScreenFlow.ConfirmChoices[1], null, null, () => { ControlBindings.ResetScheme(ControlScheme); ControlBindings.Save(store); Flow.Back(); });   // Yes

            var how = NewPage(ShellScreen.HowTo, HowToPlay.Titles[0], 410f, 64f, Palette.CourtLines, 0.8f, -300f);
            AddBody(how, "", 34f, 40f, 1500f, 620f, TextAlignmentOptions.TopLeft);
            how.bodyT.enableAutoSizing = true; how.bodyT.fontSizeMin = 22f; how.bodyT.fontSizeMax = 34f;
            AddRow(how, "Page", () => $"< {howPage + 1} / {HowToPlay.Titles.Length} >", d => howPage = (howPage + d + HowToPlay.Titles.Length) % HowToPlay.Titles.Length, null);
            AddRow(how, "Back", null, null, () => Flow.Back());

            var results = NewPage(ShellScreen.Results, "", 290f, 130f, Palette.CourtLines, 0.55f, 20f);
            AddBody(results, "", 90f, 170f, 1200f, 120f, TextAlignmentOptions.Center);
            AddRow(results, "Play Again", null, null, () => { local.PlayAgain(); Flow.Go(ShellScreen.Match); });
            AddRow(results, "Change Players", null, null, () => { local.ReturnToJoin(); Flow.Go(ShellScreen.Setup); });
            AddRow(results, "Title", null, null, () => { local.ReturnToJoin(); Flow.Go(ShellScreen.Title); });

            // Setup: the join screen is its own canvas; this page only adds the row of match options under the team panels.
            var setup = NewPage(ShellScreen.Setup, "", 0f, 10f, Palette.CourtLines, 0f, 0f);
            AddBody(setup, "Down: match options and bot levels    Left / Right: pick    Confirm: change    Up: back to the players    Esc: title", 26f, -312f, 1800f, 40f, TextAlignmentOptions.Center);
            AddCell(setup, -760f, "DIFFICULTY", () => $"< {Levels[diff]} >", d => diff = (diff + d + Levels.Length) % Levels.Length);
            AddCell(setup, -380f, "POINTS", () => $"< {Points[pts]} >", d => pts = (pts + d + Points.Length) % Points.Length);
            AddCell(setup, 0f, "POINT CAP", () => $"< {(cap ? "On" : "Off")} >", d => cap = !cap);
            AddCell(setup, 380f, "SETS", () => $"< {SetsChoices[sets]} >", d => sets = (sets + d + SetsChoices.Length) % SetsChoices.Length);
            AddCell(setup, 760f, "", null, null, () => Flow.Go(ShellScreen.Title)).labelT.text = "Back";
            // second row: one small box per bot slot (Match = follow the Difficulty option above)
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                int slot = i;
                AddCell(setup, -570f + i * 380f, $"BOT {i + 1}", () => local.Slots.IsHuman(slot) ? "Player" : $"< {PlayerSlots.BotLevelNames[local.Slots.BotLevel(slot)]} >",
                    d => { if (!local.Slots.IsHuman(slot)) local.Slots.CycleBotLevel(slot, d); }, null, true);
            }

            // the join screen fades like a page
            pages[ShellScreen.Setup].go.transform.SetAsLastSibling();
            join.Group.alpha = 0f; join.Root.SetActive(false);
            foreach (var p in pages.Values) { p.group.alpha = 0f; p.go.SetActive(false); }
            pages[ShellScreen.Title].group.alpha = 1f; pages[ShellScreen.Title].go.SetActive(true);
        }

        static string OnOff(bool on) => on ? "On" : "Off";

        Page NewPage(ShellScreen screen, string title, float titleY, float titleSize, Color titleColour, float dim, float firstRowY)
        {
            var go = new GameObject("Page " + screen, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var p = new Page { go = go, group = go.GetComponent<CanvasGroup>(), nextY = firstRowY };
            if (dim > 0f) JoinScreen.Box("Dim", go.transform, Palette.WithAlpha(Palette.Shadow, dim), Vector2.zero, Vector2.zero, true);
            p.titleT = JoinScreen.Text("Title", go.transform, font, title, titleSize, titleColour, new Vector2(0f, titleY), new Vector2(1700f, titleSize * 1.3f));
            if (screen != ShellScreen.Setup) p.hintT = JoinScreen.Text("Hint", go.transform, font, "", 28f, Palette.CourtLines, new Vector2(0f, -490f), new Vector2(1800f, 50f));
            pages[screen] = p;
            return p;
        }

        void AddBody(Page p, string text, float size, float y, float w, float h, TextAlignmentOptions align)
        {
            p.bodyT = JoinScreen.Text("Body", p.go.transform, font, text, size, Palette.CourtLines, new Vector2(0f, y), new Vector2(w, h));
            p.bodyT.alignment = align;
            p.bodyT.raycastTarget = false;
        }

        // A vertical menu line: label on the left, value (and slider bar) on the right.
        void AddRow(Page p, string label, Func<string> value, Action<int> step, Action activate, Func<float> bar = null)
        {
            var r = new Row { value = value, step = step, activate = activate, bar = bar };
            var pos = new Vector2(0f, p.nextY);
            p.nextY -= p.pitch;
            r.back = JoinScreen.Box("Row " + label, p.go.transform, Palette.Shadow, pos, new Vector2(900f, p.height), false);
            r.rt = r.back.rectTransform;
            bool plain = value == null;
            r.labelT = JoinScreen.Text("Label", r.back.transform, font, label, 38f, Palette.CourtLines, plain ? Vector2.zero : new Vector2(-150f, bar != null ? p.height * 0.1f : 0f), new Vector2(plain ? 860f : 540f, p.height - 14f));
            r.labelT.alignment = plain ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            if (!plain)
            {
                r.valueT = JoinScreen.Text("Value", r.back.transform, font, "", 38f, Palette.Accent, new Vector2(290f, bar != null ? p.height * 0.1f : 0f), new Vector2(260f, p.height - 14f));
                r.valueT.alignment = TextAlignmentOptions.MidlineRight;
            }
            if (bar != null)
            {
                r.track = JoinScreen.Box("Track", r.back.transform, Palette.UiBarEmpty, new Vector2(0f, -(p.height / 2f - 10f)), new Vector2(820f, 8f), false);
                r.fill = JoinScreen.Box("Fill", r.track.transform, Palette.Accent, new Vector2(-410f, 0f), new Vector2(0f, 8f), false);
                r.fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                r.fill.rectTransform.anchoredPosition = new Vector2(-410f, 0f);
            }
            p.rows.Add(r);
        }

        // A Setup option: a small box with a title above its value.
        Row AddCell(Page p, float x, string label, Func<string> value, Action<int> step, Action activate = null, bool small = false)
        {
            var r = new Row { value = value, step = step, activate = activate };
            r.back = JoinScreen.Box("Cell " + label, p.go.transform, Palette.Shadow, new Vector2(x, small ? -250f : -175f), new Vector2(360f, small ? 56f : 70f), false);
            r.rt = r.back.rectTransform;
            if (small)      // a one-line box: the bot's name on the left, its level on the right
            {
                r.labelT = JoinScreen.Text("Label", r.back.transform, font, label, 24f, Palette.CourtLines, new Vector2(-100f, 0f), new Vector2(130f, 40f));
                r.valueT = JoinScreen.Text("Value", r.back.transform, font, "", 30f, Palette.Accent, new Vector2(60f, 0f), new Vector2(210f, 44f));
                p.rows.Add(r);
                return r;
            }
            bool plain = value == null;
            r.labelT = JoinScreen.Text("Label", r.back.transform, font, label, plain ? 38f : 24f, Palette.CourtLines, new Vector2(0f, plain ? 0f : 19f), new Vector2(340f, plain ? 70f : 34f));
            if (!plain) r.valueT = JoinScreen.Text("Value", r.back.transform, font, "", 36f, Palette.Accent, new Vector2(0f, -11f), new Vector2(340f, 44f));
            p.rows.Add(r);
            return r;
        }

        // ---------------- every frame ----------------

        public void Tick()
        {
            input.Poll();
            ApplySettings();
            ShellScreen cur = Flow.Current;

            // something outside the menus started a match (a test, a script): leave the menus
            if (match.State != MatchState.Setup && (cur == ShellScreen.Title || cur == ShellScreen.Setup)) { Flow.ForceMatch(); cur = ShellScreen.Match; }

            switch (cur)
            {
                case ShellScreen.Match:
                    if (match.State == MatchState.MatchOver) Flow.Go(ShellScreen.Results);
                    else if (input.pause && match.State != MatchState.Setup) Flow.Go(match.Practice ? ShellScreen.PracticePause : ShellScreen.Pause);
                    break;
                case ShellScreen.Setup: TickSetup(); break;
                default: TickMenu(pages[cur]); break;
            }

            if (Flow.Current != shown) Entered(Flow.Current);
            if (hintStyle != InputDevices.Style) RefreshHints();
            Draw();
        }

        void TickMenu(Page p)
        {
            if (rebind != null || Time.frameCount == listenEndedFrame) return;     // listening for a new key: the menu waits (and the key that ends it is not also a menu press)
            ShellScreen cur = Flow.Current;
            if (input.back || ((cur == ShellScreen.Pause || cur == ShellScreen.PracticePause) && input.pause)) { Flow.Back(); return; }
            if (cur == ShellScreen.HowTo) input.confirm &= p.sel == 1;     // only the Back line confirms; the page line turns with left / right
            int n = p.rows.Count;
            if (input.dy != 0) p.sel = (p.sel - input.dy + n) % n;
            if (input.dx != 0) p.rows[p.sel].step?.Invoke(input.dx);
            if (input.confirm) Activate(p.rows[p.sel]);
            PointerTick(p);
        }

        // Setup: the join slots first; Down moves to the options row, where Left / Right pick a box and Confirm changes it.
        void TickSetup()
        {
            Page p = pages[ShellScreen.Setup];
            if (input.esc || (zone == 1 && input.back)) { Flow.Go(ShellScreen.Title); return; }
            if (zone == 0)
            {
                if (input.dy == -1) { zone = 1; p.sel = 0; }
                return;
            }
            if (input.dy == 1) { if (p.sel >= BotRow) p.sel = Mathf.Min(p.sel - BotRow, BotRow - 1); else zone = 0; return; }
            if (input.dy == -1 && p.sel < BotRow) { p.sel = BotRow + Mathf.Min(p.sel, PlayerSlots.Count - 1); return; }
            if (input.dx != 0) p.sel = p.sel < BotRow ? Mathf.Clamp(p.sel + input.dx, 0, BotRow - 1) : Mathf.Clamp(p.sel + input.dx, BotRow, p.rows.Count - 1);
            if (input.confirm) Activate(p.rows[p.sel]);
            if (PointerTick(p)) zone = 1;
        }

        static void Activate(Row r) { if (r.activate != null) r.activate(); else r.step?.Invoke(1); }

        // Mouse: hovering picks a line, a click on it works it (left half of a choice steps back, right half forward). True if the pointer is on a line.
        bool PointerTick(Page p)
        {
            Mouse m = Mouse.current;
            if (m == null) return false;
            bool click = m.leftButton.wasPressedThisFrame, moved = m.delta.ReadValue().sqrMagnitude > 0f;
            if (!click && !moved) return false;
            Vector2 pos = m.position.ReadValue();
            for (int i = 0; i < p.rows.Count; i++)
            {
                Row r = p.rows[i];
                if (!RectTransformUtility.RectangleContainsScreenPoint(r.rt, pos, null)) continue;
                p.sel = i;
                if (click)
                {
                    input.confirmDevice = PlayerSlots.KeyboardId;      // a mouse click counts as the keyboard + mouse
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(r.rt, pos, null, out Vector2 lp);
                    float frac = (lp.x - r.rt.rect.xMin) / r.rt.rect.width;
                    if (r.activate != null) r.activate(); else r.step?.Invoke(frac < 0.5f ? -1 : 1);
                }
                return true;
            }
            return false;
        }

        // ---------------- reacting to a screen change ----------------

        void Entered(ShellScreen s)
        {
            ShellScreen prev = shown;
            shown = s;
            bool fromSub = prev == ShellScreen.Settings || prev == ShellScreen.HowTo || prev == ShellScreen.Confirm || prev == ShellScreen.Camera || prev == ShellScreen.Controls || prev == ShellScreen.ConfirmReset;   // coming back: keep the highlighted line
            if (pages.TryGetValue(s, out Page p) && !fromSub) p.sel = 0;
            if (s == ShellScreen.Confirm || s == ShellScreen.ConfirmReset) p.sel = ScreenFlow.ConfirmDefault;
            if (s == ShellScreen.Controls && prev != ShellScreen.ConfirmReset) RebuildControlRows();      // fresh lines each time it opens (not when coming back from the reset question)
            if (s == ShellScreen.Setup) { zone = 0; p.sel = 0; }
            if ((s == ShellScreen.Pause || s == ShellScreen.PracticePause) && prev == ShellScreen.Match) Freeze(true);
            if (s == ShellScreen.Match || s == ShellScreen.Title || s == ShellScreen.Setup || s == ShellScreen.Results) Freeze(false);
            if (s == ShellScreen.HowTo) { howPage = 0; howStyle = InputDevices.Style; }
            if (s == ShellScreen.Results) ShowResults(p);
            Debug.Log("[Shell] screen " + s);
        }

        // Pause: the game stops, sounds pause, and the humans' gameplay input is switched off so only the menu reads buttons.
        void Freeze(bool on)
        {
            if (on == frozen) return;
            frozen = on;
            AudioListener.pause = on;
            if (on)
            {
                savedScale = Time.timeScale;
                Time.timeScale = 0f;
                foreach (HumanBrain h in match.Humans) if (h != null) h.enabled = false;
            }
            else
            {
                Time.timeScale = savedScale;      // exactly what it was before the pause
                match.ApplyBrains();
            }
        }

        void ShowResults(Page p)
        {
            RulesEngine r = match.Rules;
            MatchSets ms = match.Sets;
            int w = ms.IsBestOfThree ? ms.Winner : r.Winner >= 0 ? r.Winner : r.LastPointWinner;
            p.titleT.text = $"TEAM {(char)('A' + w)} WINS";
            p.titleT.color = w == 0 ? Palette.TeamA : Palette.TeamB;
            string a = ColorUtility.ToHtmlStringRGB(Palette.TeamA), b = ColorUtility.ToHtmlStringRGB(Palette.TeamB);
            p.bodyT.text = $"<color=#{a}>{r.Score[0]}</color>  -  <color=#{b}>{r.Score[1]}</color>";
            if (ms.IsBestOfThree)      // sets won, then every set's score
            {
                var all = new System.Text.StringBuilder();
                foreach (int[] s in ms.Scores) all.Append(all.Length > 0 ? "    " : "").Append($"{s[0]} - {s[1]}");
                p.bodyT.text = $"<color=#{a}>{ms.SetsWon[0]}</color>  -  <color=#{b}>{ms.SetsWon[1]}</color>\n<size=45%>Sets: {all}</size>";
            }
        }

        // Options reach the match when it starts (LocalPlayers.StartMatch).
        public void ApplyOptions()
        {
            match.difficulty = (AIDifficulty)diff;
            match.SetOptions(Points[pts], cap, sets == 1 ? 2 : 1);
        }

        /// <summary>A match began (from the Setup screen, or from a script): the menus step aside.</summary>
        public void MatchStarted() { if (!Flow.Go(ShellScreen.Match)) Flow.ForceMatch(); }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;     // in the Editor, quitting just stops play mode
#else
            Application.Quit();
#endif
        }

        // ---------------- controls screen ----------------

        string ControlScheme => controlsPad ? ControlBindings.Pad : ControlBindings.Keyboard;

        // Opens the Controls screen for the device that pressed Confirm on the Settings line.
        void OpenControls()
        {
            controlsPad = input.confirmDevice != PlayerSlots.KeyboardId;
            controlsStyle = !controlsPad ? PromptStyle.KeyboardMouse : InputSystem.GetDeviceById(input.confirmDevice) is DualShockGamepad ? PromptStyle.PlayStation : PromptStyle.Xbox;
            Flow.Go(ShellScreen.Controls);
        }

        // One line per rebindable action of the scheme, then Reset and Back.
        void RebuildControlRows()
        {
            Page p = pages[ShellScreen.Controls];
            foreach (Row r in p.rows) Destroy(r.back.gameObject);
            p.rows.Clear();
            p.nextY = 330f; p.sel = 0;
            string scheme = ControlScheme;
            p.titleT.text = controlsPad ? "CONTROLS: GAMEPAD" : "CONTROLS: KEYBOARD + MOUSE";
            foreach (ControlBindings.Entry e in ControlBindings.EntriesFor(scheme))
            {
                ControlBindings.Entry entry = e;
                AddRow(p, entry.label, () => listeningLabel == entry.label ? (controlsPad ? "Press button..." : "Press key...") : ControlBindings.PathName(ControlBindings.PathOf(entry, scheme), controlsStyle),
                    null, () => StartListening(entry, scheme));
            }
            AddRow(p, "Reset to defaults", null, null, () => Flow.Go(ShellScreen.ConfirmReset));
            AddRow(p, "Back", null, null, () => Flow.Back());
        }

        // The next key or button pressed becomes the binding (the Input System listens). Esc (keyboard) or Start (gamepad) cancels.
        void StartListening(ControlBindings.Entry e, string scheme)
        {
            if (rebind != null) return;
            InputAction action = ControlBindings.Shared.asset.FindAction(e.action);
            int index = ControlBindings.IndexOf(action, scheme, e.part);
            if (index < 0) return;
            listeningLabel = e.label;
            bool pad = scheme == ControlBindings.Pad;
            var op = action.PerformInteractiveRebinding(index)
                .WithControlsHavingToMatchPath(pad ? "<Gamepad>" : "<Keyboard>")
                .WithControlsExcluding("<Keyboard>/anyKey").WithControlsExcluding("<Keyboard>/imeSelected")
                .WithControlsExcluding("<Pointer>/press").WithControlsExcluding("<Mouse>/scroll/*")
                .WithControlsExcluding("<Gamepad>/leftStick/*").WithControlsExcluding("<Gamepad>/rightStick/*")
                .WithCancelingThrough(pad ? "<Gamepad>/start" : "<Keyboard>/escape")
                .WithMagnitudeHavingToBeGreaterThan(0.5f)
                .OnApplyBinding((o, path) => ControlBindings.Assign(e, scheme, path))      // sets it, and swaps with whoever had that key
                .OnComplete(o => { StopListening(); ControlBindings.Save(store); })
                .OnCancel(o => StopListening());
            if (!pad) op.WithControlsHavingToMatchPath("<Mouse>");           // a mouse button can be a keyboard-scheme binding too
            rebind = op.Start();
        }

        void StopListening()
        {
            rebind?.Dispose();
            rebind = null;
            listeningLabel = null;
            listenEndedFrame = Time.frameCount;
        }

        // ---------------- settings ----------------

        void Save() { Settings.Save(store); ApplyFullscreen(); }

        void ApplyFullscreen() { if (!Application.isEditor) Screen.fullScreen = Settings.Fullscreen; }   // no effect in the Editor

        // The sliders drive AudioDirector's two volume fields; aim assist goes to every human's brain.
        void ApplySettings()
        {
            if (audio == null) audio = FindFirstObjectByType<AudioDirector>();
            if (audio != null) { audio.musicVolume = Settings.MusicPercent / 100f; audio.sfxVolume = Settings.EffectsPercent / 100f; }
            foreach (HumanBrain h in match.Humans) if (h != null) h.aimAssist = Settings.AimAssist;
        }

        // ---------------- drawing ----------------

        void RefreshHints()
        {
            hintStyle = InputDevices.Style;
            string key = ColorUtility.ToHtmlStringRGB(Palette.Accent);
            string K(string s) => $"<color=#{key}>{s}</color>";
            string text = hintStyle == PromptStyle.KeyboardMouse
                ? $"{K("Arrows")} / {K("WASD")} choose      {K("Enter")} / {K("Space")} select      {K("Esc")} back"
                : $"{K("D-pad")} / {K(ControlPrompts.DefaultLabel(PromptAction.Move, hintStyle))} choose      {K(ControlPrompts.DefaultLabel(PromptAction.Jump, hintStyle))} select      {K(ControlPrompts.DefaultLabel(PromptAction.Dive, hintStyle))} back";
            // the Title has nothing to go back to, so its line leaves out "back"
            string titleText = hintStyle == PromptStyle.KeyboardMouse
                ? $"{K("Arrows")} / {K("WASD")} choose      {K("Enter")} / {K("Space")} select"
                : $"{K("D-pad")} / {K(ControlPrompts.DefaultLabel(PromptAction.Move, hintStyle))} choose      {K(ControlPrompts.DefaultLabel(PromptAction.Jump, hintStyle))} select";
            foreach (var kv in pages) if (kv.Value.hintT != null) kv.Value.hintT.text = kv.Key == ShellScreen.Title ? titleText : text;
        }

        void Draw()
        {
            foreach (var kv in pages)
            {
                Page p = kv.Value;
                bool current = kv.Key == Flow.Current;
                p.group.alpha = Mathf.MoveTowards(p.group.alpha, current ? 1f : 0f, FadePerSecond * Time.unscaledDeltaTime);
                bool on = p.group.alpha > 0f || current;
                if (p.go.activeSelf != on) p.go.SetActive(on);
                if (on) DrawPage(kv.Key, p, current);
            }
            // the join screen is the Setup screen's background
            float a = pages[ShellScreen.Setup].group.alpha;
            join.Group.alpha = a;
            if (join.Root.activeSelf != (a > 0f)) join.Root.SetActive(a > 0f);
        }

        void DrawPage(ShellScreen screen, Page p, bool current)
        {
            if (screen == ShellScreen.HowTo && current)
            {
                p.titleT.text = HowToPlay.Titles[howPage];
                string body = HowToPlay.Pages(howStyle)[howPage];
                if (p.bodyT.text != body) p.bodyT.text = body;
            }
            for (int i = 0; i < p.rows.Count; i++)
            {
                Row r = p.rows[i];
                bool sel = current && i == p.sel && (screen != ShellScreen.Setup || zone == 1);
                r.back.color = sel ? Palette.Accent : Palette.WithAlpha(Palette.Shadow, 0.75f);
                Color text = sel ? Palette.BallOutline : Palette.CourtLines;
                r.labelT.color = text;
                if (r.valueT != null)
                {
                    string v = r.value();
                    if (r.valueT.text != v) r.valueT.text = v;
                    r.valueT.color = sel ? Palette.BallOutline : Palette.Accent;
                }
                if (r.bar != null)
                {
                    r.fill.rectTransform.sizeDelta = new Vector2(820f * r.bar(), 8f);
                    r.fill.color = sel ? Palette.BallOutline : Palette.Accent;
                    r.track.color = sel ? Palette.WithAlpha(Palette.BallOutline, 0.3f) : Palette.UiBarEmpty;
                }
            }
        }
    }
}
