using System.Text;
using Sandspike.Art;
using Sandspike.Controls;
using TMPro;
using UnityEngine;

namespace Sandspike.UI
{
    /// <summary>
    /// A small controls panel whose button names follow the device you used last:
    /// keys + mouse, Xbox names (A / X / Y / RT) or PlayStation names (Cross / Square / Triangle / R2).
    /// </summary>
    public class ControlsHint : MonoBehaviour
    {
        [SerializeField] TMP_Text label;

        static readonly PromptAction[] Lines =
        {
            PromptAction.Move, PromptAction.Aim, PromptAction.Jump, PromptAction.Spike,
            PromptAction.Bump, PromptAction.Set, PromptAction.ToggleView, PromptAction.FeedBall,
        };

        void OnEnable()
        {
            InputDevices.Changed += Refresh;
            Refresh();
        }

        void OnDisable() { InputDevices.Changed -= Refresh; }

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
