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
            PromptAction.Bump, PromptAction.Set, PromptAction.Dive, PromptAction.ToggleView, PromptAction.ToggleHints,
        };

        CanvasGroup group;
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
        }

        void Refresh()
        {
            if (label == null) return;
            string keyColour = ColorUtility.ToHtmlStringRGB(Palette.Accent);
            var sb = new StringBuilder();
            foreach (var action in Lines)
            {
                sb.Append("<color=#").Append(keyColour).Append("><b>")
                  .Append(ControlPrompts.Label(action, InputDevices.Style))
                  .Append("</b></color>  ")
                  .AppendLine(ControlPrompts.ActionName(action));
            }
            label.text = sb.ToString();
        }
    }
}
