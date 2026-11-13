using System.Text;
using Sandspike.Art;
using Sandspike.Controls;
using Sandspike.Players;
using TMPro;
using UnityEngine;

namespace Sandspike.UI
{
    /// <summary>
    /// A small controls panel whose button names follow the device you used last:
    /// keys + mouse, Xbox names (A / X / Y / RT) or PlayStation names (Cross / Square / Triangle / R2).
    /// It shows for the first few seconds, then hides; H / gamepad Select brings it back (and hides it again).
    /// </summary>
    public class ControlsHint : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] HumanBrain brain;               // for the toggle button
        [SerializeField] float visibleSeconds = 10f;     // shown this long after the game starts

        static readonly PromptAction[] Lines =
        {
            PromptAction.Move, PromptAction.Aim, PromptAction.Jump, PromptAction.Spike,
            PromptAction.Bump, PromptAction.Set, PromptAction.Dive, PromptAction.Sprint, PromptAction.Block, PromptAction.ToggleView, PromptAction.ToggleHints,
        };

        /// <summary>What the greying needs to know about the player (a plain struct, so the rules can be tested).</summary>
        public struct HintState
        {
            public bool grounded, busy, serving, canSprint, blockAvailable;   // busy = diving or getting up
        }

        /// <summary>True if this action cannot be used right now (its line is drawn greyed out).</summary>
        public static bool Greyed(PromptAction action, HintState s)
        {
            switch (action)
            {
                case PromptAction.Move: return s.busy;
                case PromptAction.Jump: return !s.grounded || s.busy;
                case PromptAction.Spike: return s.busy;
                case PromptAction.Bump:
                case PromptAction.Set: return s.serving || s.busy;
                case PromptAction.Dive: return !s.grounded || s.serving || s.busy;
                case PromptAction.Sprint: return !s.canSprint;
                case PromptAction.Block: return !s.blockAvailable;
                default: return false;
            }
        }

        CanvasGroup group;
        PlayerController player;
        HintState state;
        bool stateKnown;
        bool shown = true;
        float timer;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            InputDevices.Changed += Refresh;
            Refresh();
        }

        void OnDisable() { InputDevices.Changed -= Refresh; }

        void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (shown && timer >= visibleSeconds) shown = false;
            if (brain != null && brain.isActiveAndEnabled && brain.Controls.Gameplay.ToggleHints.WasPressedThisFrame()) shown = !shown;
            group.alpha = Mathf.MoveTowards(group.alpha, shown ? 1f : 0f, Time.unscaledDeltaTime * 4f);

            // greying follows the player: rebuild the text only when something changed
            if (player == null && brain != null) player = brain.GetComponent<PlayerController>();
            if (player == null || player.Brain == null) return;
            HintState now = new HintState
            {
                grounded = player.IsGrounded,
                busy = player.IsDiving || player.IsRecovering,
                serving = player.ServeMode,
                canSprint = !player.IsBurnedOut && !player.IsDiving && !player.IsRecovering
                            && (player.IsSprinting ? player.Stamina.Current > 0f : player.Stamina.Current >= player.SprintMinStamina),
                blockAvailable = player.BlockAvailable,
            };
            if (!stateKnown || !now.Equals(state)) { state = now; stateKnown = true; Refresh(); }
        }

        void Refresh()
        {
            if (label == null) return;
            string keyColour = ColorUtility.ToHtmlStringRGB(Palette.Accent);
            string muted = ColorUtility.ToHtmlStringRGBA(Palette.WithAlpha(Palette.UiMuted, 0.5f));
            var sb = new StringBuilder();
            foreach (var action in Lines)
            {
                bool grey = stateKnown && Greyed(action, state);
                // the Spike line reads "Block" while a jump at the net would block
                string name = action == PromptAction.Spike && stateKnown && state.blockAvailable ? "Block" : ControlPrompts.ActionName(action);
                if (grey) sb.Append("<color=#").Append(muted).Append('>');
                else sb.Append("<color=#").Append(keyColour).Append('>');
                sb.Append("<b>").Append(ControlPrompts.Label(action, InputDevices.Style)).Append("</b></color>  ");
                if (grey) sb.Append("<color=#").Append(muted).Append('>').Append(name).Append("</color>");
                else sb.Append(name);
                sb.AppendLine();
            }
            label.text = sb.ToString();
        }
    }
}
