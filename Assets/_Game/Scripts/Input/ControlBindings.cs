using System;
using Sandspike.Core;
using UnityEngine.InputSystem;

namespace Sandspike.Controls
{
    /// <summary>
    /// The player's rebound controls. One set of binding overrides (saved as one JSON text) covers both schemes, because every keyboard
    /// binding and every gamepad binding has its own id; so the overrides are global per scheme, not per player. Every HumanBrain applies
    /// them to its own VolleyballControls, and the Controls screen edits a shared copy (Shared) that is never enabled.
    /// "Keyboard&amp;Mouse" and "Gamepad" are the two binding groups in VolleyballControls.inputactions.
    /// </summary>
    public static class ControlBindings
    {
        public const string Keyboard = "Keyboard&Mouse", Pad = "Gamepad";
        const string StoreKey = "bindings";

        /// <summary>One line of the Controls screen: its name, the input action, and (for Move) which part of the composite.</summary>
        public struct Entry
        {
            public string label, action, part;
            public Entry(string label, string action, string part = null) { this.label = label; this.action = action; this.part = part; }
        }

        // What can be rebound. Aim (mouse / right stick), the Move stick and Pause are fixed, and so is the hidden training FeedBall.
        public static readonly Entry[] KeyboardEntries =
        {
            new Entry("Move up", "Move", "Up"), new Entry("Move down", "Move", "Down"), new Entry("Move left", "Move", "Left"), new Entry("Move right", "Move", "Right"),
            new Entry("Jump", "Jump"), new Entry("Spike / toss serve", "Spike"), new Entry("Bump", "Bump"), new Entry("Set", "Set"),
            new Entry("Dive", "Dive"), new Entry("Sprint", "Sprint"), new Entry("1st / 3rd person", "ToggleView"), new Entry("Show / hide controls", "ToggleHints"),
        };

        public static readonly Entry[] PadEntries =
        {
            new Entry("Jump", "Jump"), new Entry("Spike / toss serve", "Spike"), new Entry("Bump", "Bump"), new Entry("Set", "Set"),
            new Entry("Dive", "Dive"), new Entry("Sprint", "Sprint"), new Entry("1st / 3rd person", "ToggleView"), new Entry("Show / hide controls", "ToggleHints"),
        };

        public static Entry[] EntriesFor(string scheme) => scheme == Pad ? PadEntries : KeyboardEntries;

        static VolleyballControls shared;
        static string json = "";

        /// <summary>Raised when the saved overrides change (the brains re-apply them, the hints redraw).</summary>
        public static event Action Changed;

        /// <summary>A controls object just for the Controls screen and for looking up button names. It is never enabled.</summary>
        public static VolleyballControls Shared
        {
            get
            {
                if (shared == null) { shared = new VolleyballControls(); Apply(shared); }
                return shared;
            }
        }

        /// <summary>Puts the saved overrides on a controls object (every HumanBrain does this when it is made, and again at every change).</summary>
        public static void Apply(VolleyballControls c)
        {
            if (string.IsNullOrEmpty(json)) c.asset.RemoveAllBindingOverrides();
            else c.asset.LoadBindingOverridesFromJson(json);
        }

        /// <summary>Reads the saved overrides (at start-up).</summary>
        public static void Load(ISettingsStore s)
        {
            json = s.GetString(StoreKey, "");
            if (shared != null) Apply(shared);
            Changed?.Invoke();
        }

        /// <summary>Saves what Shared holds now and tells everyone.</summary>
        public static void Save(ISettingsStore s)
        {
            json = Shared.asset.SaveBindingOverridesAsJson();
            s.SetString(StoreKey, json);
            Changed?.Invoke();
        }

        /// <summary>Forgets everything (tests).</summary>
        public static void Reset() { json = ""; shared = null; }

        // ---------------- finding bindings ----------------

        static bool HasGroup(InputBinding b, string scheme) => (";" + b.groups + ";").Contains(";" + scheme + ";");

        /// <summary>The index in action.bindings of this entry's binding for the scheme, or -1.</summary>
        public static int IndexOf(InputAction a, string scheme, string part)
        {
            var bs = a.bindings;
            for (int i = 0; i < bs.Count; i++)
            {
                InputBinding b = bs[i];
                if (!HasGroup(b, scheme)) continue;
                if (part == null ? (!b.isComposite && !b.isPartOfComposite) : (b.isPartOfComposite && b.name == part)) return i;
            }
            return -1;
        }

        /// <summary>The path the entry is bound to now ("&lt;Keyboard&gt;/space"), or "" if it has none in this scheme.</summary>
        public static string PathOf(Entry e, string scheme)
        {
            InputAction a = Shared.asset.FindAction(e.action);
            int i = a == null ? -1 : IndexOf(a, scheme, e.part);
            return i < 0 ? "" : a.bindings[i].effectivePath ?? "";
        }

        /// <summary>
        /// Binds the entry to newPath. If another entry of the same scheme already used newPath, it takes this entry's old path (the two swap).
        /// Works on Shared; call Save afterwards.
        /// </summary>
        public static void Assign(Entry e, string scheme, string newPath)
        {
            InputAction a = Shared.asset.FindAction(e.action);
            int idx = IndexOf(a, scheme, e.part);
            string old = a.bindings[idx].effectivePath;
            a.ApplyBindingOverride(idx, newPath);
            foreach (Entry other in EntriesFor(scheme))
            {
                InputAction oa = Shared.asset.FindAction(other.action);
                int oi = IndexOf(oa, scheme, other.part);
                bool same = oa == a && oi == idx;
                if (!same && oa.bindings[oi].effectivePath == newPath) oa.ApplyBindingOverride(oi, old);
            }
        }

        /// <summary>Puts every binding of one scheme back to its default. Works on Shared; call Save afterwards.</summary>
        public static void ResetScheme(string scheme)
        {
            foreach (InputAction a in Shared.asset)
                for (int i = 0; i < a.bindings.Count; i++)
                    if (HasGroup(a.bindings[i], scheme)) a.RemoveBindingOverride(i);
        }

        // ---------------- names ----------------

        /// <summary>The name to show for a gameplay action: the default text unless the player rebound it.</summary>
        public static string Name(PromptAction action, PromptStyle style, string defaultName)
        {
            bool pad = style != PromptStyle.KeyboardMouse;
            string scheme = pad ? Pad : Keyboard;
            if (action == PromptAction.Aim || action == PromptAction.Pause || (pad && action == PromptAction.Move)) return defaultName;   // fixed
            InputAction a = Shared.asset.FindAction(action.ToString());
            if (a == null) return defaultName;
            if (action == PromptAction.Move)       // the four keys: "WASD" when they are letters
            {
                string[] parts = { "Up", "Left", "Down", "Right" };
                bool changed = false;
                var names = new string[4];
                for (int p = 0; p < 4; p++)
                {
                    int i = IndexOf(a, scheme, parts[p]);
                    if (i < 0) return defaultName;
                    changed |= a.bindings[i].overridePath != null;
                    names[p] = PathName(a.bindings[i].effectivePath, style);
                }
                if (!changed) return defaultName;
                bool letters = Array.TrueForAll(names, n => n.Length == 1);
                return string.Join(letters ? "" : " ", names);
            }
            int idx = IndexOf(a, scheme, null);
            if (idx < 0 || a.bindings[idx].overridePath == null) return defaultName;
            return PathName(a.bindings[idx].effectivePath, style);
        }

        /// <summary>A control path as a button name for the style: "Space", "Left Click", "A" / "Cross", "R2" ...</summary>
        public static string PathName(string path, PromptStyle style)
        {
            if (string.IsNullOrEmpty(path)) return "None";
            int slash = path.IndexOf('/');
            string device = slash < 0 ? path : path.Substring(0, slash), leaf = slash < 0 ? "" : path.Substring(slash + 1);
            bool ps = style == PromptStyle.PlayStation;
            if (device.Contains("Mouse"))
            {
                switch (leaf)
                {
                    case "leftButton": return "Left Click";
                    case "rightButton": return "Right Click";
                    case "middleButton": return "Middle Click";
                    case "forwardButton": return "Mouse Forward";
                    case "backButton": return "Mouse Back";
                }
            }
            else if (device.Contains("Keyboard"))
            {
                if (leaf == "leftShift") return "Shift";        // the default Dive key has always read "Shift"
                if (leaf == "leftCtrl") return "Left Ctrl";     // and Sprint "Left Ctrl"
                if (leaf == "rightCtrl") return "Right Ctrl";
                if (leaf == "escape") return "Esc";
            }
            else
            {
                switch (leaf)
                {
                    case "buttonSouth": return ps ? "Cross" : "A";
                    case "buttonEast": return ps ? "Circle" : "B";
                    case "buttonWest": return ps ? "Square" : "X";
                    case "buttonNorth": return ps ? "Triangle" : "Y";
                    case "leftShoulder": return ps ? "L1" : "LB";
                    case "rightShoulder": return ps ? "R1" : "RB";
                    case "leftTrigger": return ps ? "L2" : "LT";
                    case "rightTrigger": return ps ? "R2" : "RT";
                    case "leftStickPress": return "L3";
                    case "rightStickPress": return "R3";
                    case "start": return ps ? "Options" : "Menu";
                    case "select": return ps ? "Create" : "View";
                    case "dpad/up": return "D-pad Up";
                    case "dpad/down": return "D-pad Down";
                    case "dpad/left": return "D-pad Left";
                    case "dpad/right": return "D-pad Right";
                }
            }
            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
    }
}
