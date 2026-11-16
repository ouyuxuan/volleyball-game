using System.Text;
using Sandspike.Art;
using Sandspike.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

namespace Sandspike.UI
{
    /// <summary>
    /// The screen shown before the first serve: two team panels with two slots each ("Bot" or a joined player's device),
    /// and a line of button hints. It only draws; LocalPlayers reads the buttons and owns the slots. Built in code, all colours from Palette.
    /// </summary>
    public class JoinScreen : MonoBehaviour
    {
        readonly TMP_Text[] slotText = new TMP_Text[PlayerSlots.Count];
        readonly Image[] slotBack = new Image[PlayerSlots.Count];
        TMP_Text status, hint;
        GameObject root;
        string shown;

        public static JoinScreen Create(TMP_FontAsset font)
        {
            var go = new GameObject("Join Screen", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;                                   // above the HUD
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var s = go.AddComponent<JoinScreen>();
            s.root = go;
            s.Group = go.AddComponent<CanvasGroup>();
            Box("Dim", go.transform, Palette.WithAlpha(Palette.Shadow, 0.6f), Vector2.zero, Vector2.zero, true);
            Text("Title", go.transform, font, "CHOOSE PLAYERS", 72f, Palette.CourtLines, new Vector2(0f, 400f), new Vector2(1400f, 100f));

            for (int team = 0; team < 2; team++)
            {
                Color c = team == 0 ? Palette.TeamA : Palette.TeamB;
                float x = team == 0 ? -450f : 450f;
                Box($"Team {(char)('A' + team)} Panel", go.transform, Palette.WithAlpha(c, 0.35f), new Vector2(x, 90f), new Vector2(780f, 420f), false);
                Text($"Team {(char)('A' + team)} Title", go.transform, font, $"TEAM {(char)('A' + team)}", 56f, Palette.CourtLines, new Vector2(x, 250f), new Vector2(700f, 80f));
                for (int k = 0; k < 2; k++)
                {
                    int slot = team * 2 + k;
                    s.slotBack[slot] = Box($"Slot {slot}", go.transform, Palette.WithAlpha(Palette.Shadow, 0.7f), new Vector2(x, 100f - k * 150f), new Vector2(700f, 120f), false);
                    s.slotText[slot] = Text($"Slot {slot} Text", go.transform, font, "Bot", 38f, Palette.UiMuted, new Vector2(x, 100f - k * 150f), new Vector2(660f, 100f));
                }
            }
            s.status = Text("Status", go.transform, font, "", 44f, Palette.Accent, new Vector2(0f, -300f), new Vector2(1600f, 70f));
            s.hint = Text("Hint", go.transform, font, "", 30f, Palette.CourtLines, new Vector2(0f, -420f), new Vector2(1800f, 120f));
            return s;
        }

        public static Image Box(string name, Transform parent, Color colour, Vector2 pos, Vector2 size, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            if (stretch) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
            else { rt.anchoredPosition = pos; rt.sizeDelta = size; }
            var img = go.GetComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
            return img;
        }

        public static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string text, float size, Color colour, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.characterSpacing = 2f;
            t.outlineWidth = 0.2f;
            t.outlineColor = (Color32)Palette.Shadow;
            t.alignment = TextAlignmentOptions.Center;
            t.color = colour;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        /// <summary>The Shell fades this screen in and out through the group.</summary>
        public GameObject Root => root;
        public CanvasGroup Group { get; private set; }

        /// <summary>The device's name for the slot: "Keyboard + Mouse" or the gamepad's own name.</summary>
        public static string DeviceName(int deviceId)
        {
            if (deviceId == PlayerSlots.KeyboardId) return "Keyboard + Mouse";
            return InputSystem.GetDeviceById(deviceId)?.displayName ?? "Gamepad";
        }

        /// <summary>Which names the gamepad hints use: PlayStation if a DualSense / DualShock is plugged in, else Xbox.</summary>
        public static PromptStyle PadStyle()
        {
            foreach (var pad in Gamepad.all) if (pad is DualShockGamepad) return PromptStyle.PlayStation;
            return PromptStyle.Xbox;
        }

        /// <summary>Redraws from the slots (cheap: only touches the texts when something changed).</summary>
        public void Refresh(PlayerSlots slots)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < PlayerSlots.Count; i++) sb.Append(slots.Owner(i)).Append(',');
            PromptStyle ps = PadStyle();
            sb.Append(ps);
            string sig = sb.ToString();
            if (sig == shown) return;
            shown = sig;

            int number = 0;
            for (int i = 0; i < PlayerSlots.Count; i++)
            {
                bool human = slots.IsHuman(i);
                slotText[i].text = human ? $"Player {++number}  -  {DeviceName(slots.Owner(i))}" : "Bot";
                slotText[i].color = human ? Palette.CourtLines : Palette.UiMuted;
                slotBack[i].color = human ? Palette.WithAlpha(i < 2 ? Palette.TeamA : Palette.TeamB, 0.85f) : Palette.WithAlpha(Palette.Shadow, 0.7f);
            }
            status.text = slots.CanStart ? "Press Start to play" : "Join at least one player to start";

            string key = ColorUtility.ToHtmlStringRGB(Palette.Accent);
            string K(string s) => $"<color=#{key}>{s}</color>";
            hint.text =
                $"Gamepad:  {K(ControlPrompts.Label(PromptAction.Jump, ps))} join   {K(ControlPrompts.Label(PromptAction.Move, ps))} / D-pad move   " +
                $"{K(ControlPrompts.Label(PromptAction.Dive, ps))} leave   {K(ControlPrompts.Label(PromptAction.Pause, ps))} start\n" +
                $"Keyboard:  {K("Enter")} / {K("Space")} join   {K("A")} {K("D")} move   {K("Backspace")} leave   {K("Enter")} start";
        }
    }
}
