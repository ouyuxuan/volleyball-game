using System.Collections.Generic;
using NUnit.Framework;
using Sandspike.Controls;
using Sandspike.Core;

namespace Sandspike.Tests
{
    public class ControlBindingsTests
    {
        class MemoryStore : ISettingsStore
        {
            public readonly Dictionary<string, string> text = new Dictionary<string, string>();
            public int Get(string key, int fallback) => fallback;
            public void Set(string key, int value) { }
            public string GetString(string key, string fallback) => text.TryGetValue(key, out string v) ? v : fallback;
            public void SetString(string key, string value) => text[key] = value;
        }

        static ControlBindings.Entry Entry(string scheme, string action, string part = null) =>
            System.Array.Find(ControlBindings.EntriesFor(scheme), e => e.action == action && e.part == part);

        [SetUp] public void Fresh() => ControlBindings.Reset();
        [TearDown] public void Clean() => ControlBindings.Reset();

        [Test]
        public void WithoutRebinding_EveryHintIsExactlyTheOldText()
        {
            foreach (PromptStyle style in new[] { PromptStyle.KeyboardMouse, PromptStyle.Xbox, PromptStyle.PlayStation })
                foreach (PromptAction a in System.Enum.GetValues(typeof(PromptAction)))
                    Assert.AreEqual(ControlPrompts.DefaultLabel(a, style), ControlPrompts.Label(a, style), $"{a} / {style}");
        }

        [Test]
        public void DefaultBindingsReadAsTheirNames()
        {
            // the names the bindings really give, for every rebindable line
            Assert.AreEqual("Space", ControlBindings.PathName(ControlBindings.PathOf(Entry(ControlBindings.Keyboard, "Jump"), ControlBindings.Keyboard), PromptStyle.KeyboardMouse));
            Assert.AreEqual("Left Click", ControlBindings.PathName(ControlBindings.PathOf(Entry(ControlBindings.Keyboard, "Spike"), ControlBindings.Keyboard), PromptStyle.KeyboardMouse));
            Assert.AreEqual("Shift", ControlBindings.PathName(ControlBindings.PathOf(Entry(ControlBindings.Keyboard, "Dive"), ControlBindings.Keyboard), PromptStyle.KeyboardMouse));
            Assert.AreEqual("W", ControlBindings.PathName(ControlBindings.PathOf(Entry(ControlBindings.Keyboard, "Move", "Up"), ControlBindings.Keyboard), PromptStyle.KeyboardMouse));
            Assert.AreEqual("Cross", ControlBindings.PathName(ControlBindings.PathOf(Entry(ControlBindings.Pad, "Jump"), ControlBindings.Pad), PromptStyle.PlayStation));
            Assert.AreEqual("RT", ControlBindings.PathName(ControlBindings.PathOf(Entry(ControlBindings.Pad, "Spike"), ControlBindings.Pad), PromptStyle.Xbox));
        }

        [Test]
        public void Rebinding_ChangesTheHint_AndAnotherActionOnThatKeySwaps()
        {
            ControlBindings.Assign(Entry(ControlBindings.Keyboard, "Spike"), ControlBindings.Keyboard, "<Keyboard>/q");
            Assert.AreEqual("Q", ControlPrompts.Label(PromptAction.Spike, PromptStyle.KeyboardMouse));

            // Bump onto Set's key (E): Set takes Bump's old button, so no key does two jobs
            ControlBindings.Assign(Entry(ControlBindings.Keyboard, "Bump"), ControlBindings.Keyboard, "<Keyboard>/e");
            Assert.AreEqual("E", ControlPrompts.Label(PromptAction.Bump, PromptStyle.KeyboardMouse));
            Assert.AreEqual("Right Click", ControlPrompts.Label(PromptAction.Set, PromptStyle.KeyboardMouse));
        }

        [Test]
        public void Rebinding_OnePadDoesNotTouchTheKeyboard_AndPadButtonsSwapToo()
        {
            ControlBindings.Assign(Entry(ControlBindings.Pad, "Jump"), ControlBindings.Pad, "<Gamepad>/buttonEast");   // Dive's button
            Assert.AreEqual("B", ControlPrompts.Label(PromptAction.Jump, PromptStyle.Xbox));
            Assert.AreEqual("A", ControlPrompts.Label(PromptAction.Dive, PromptStyle.Xbox), "Dive took Jump's old button");
            Assert.AreEqual("Circle", ControlPrompts.Label(PromptAction.Jump, PromptStyle.PlayStation));
            Assert.AreEqual("Space", ControlPrompts.Label(PromptAction.Jump, PromptStyle.KeyboardMouse), "the keyboard is a separate scheme");
            Assert.AreEqual("A", ControlPrompts.DefaultLabel(PromptAction.Jump, PromptStyle.Xbox), "the menus keep reading the fixed buttons");
        }

        [Test]
        public void MoveKeys_ReadAsWASD_UntilOneIsRebound()
        {
            Assert.AreEqual("WASD", ControlPrompts.Label(PromptAction.Move, PromptStyle.KeyboardMouse));
            ControlBindings.Assign(Entry(ControlBindings.Keyboard, "Move", "Up"), ControlBindings.Keyboard, "<Keyboard>/upArrow");
            Assert.AreEqual("Up Arrow A S D", ControlPrompts.Label(PromptAction.Move, PromptStyle.KeyboardMouse));
            Assert.AreEqual("Left Stick", ControlPrompts.Label(PromptAction.Move, PromptStyle.Xbox), "the stick is fixed");
        }

        [Test]
        public void SavedOverrides_ComeBackInANewControlsObject_AndResetGoesBackToDefaults()
        {
            var store = new MemoryStore();
            ControlBindings.Assign(Entry(ControlBindings.Keyboard, "Jump"), ControlBindings.Keyboard, "<Keyboard>/j");
            ControlBindings.Assign(Entry(ControlBindings.Pad, "Jump"), ControlBindings.Pad, "<Gamepad>/buttonNorth");
            ControlBindings.Save(store);
            Assert.IsNotEmpty(store.GetString("bindings", ""));

            ControlBindings.Reset();                       // a new run of the game
            ControlBindings.Load(store);
            var brainControls = new VolleyballControls();  // what a HumanBrain makes
            ControlBindings.Apply(brainControls);
            Assert.AreEqual("<Keyboard>/j", brainControls.Gameplay.Jump.bindings[ControlBindings.IndexOf(brainControls.Gameplay.Jump, ControlBindings.Keyboard, null)].effectivePath);
            Assert.AreEqual("J", ControlPrompts.Label(PromptAction.Jump, PromptStyle.KeyboardMouse));
            UnityEngine.Object.DestroyImmediate(brainControls.asset);      // Dispose would call Destroy, which edit mode refuses

            ControlBindings.ResetScheme(ControlBindings.Keyboard);
            Assert.AreEqual("Space", ControlPrompts.Label(PromptAction.Jump, PromptStyle.KeyboardMouse));
            Assert.AreEqual("Y", ControlPrompts.Label(PromptAction.Jump, PromptStyle.Xbox), "the gamepad rebinding stays (one scheme at a time)");
        }

        [Test]
        public void EveryRebindableLine_HasABindingInItsScheme()
        {
            foreach (string scheme in new[] { ControlBindings.Keyboard, ControlBindings.Pad })
                foreach (var e in ControlBindings.EntriesFor(scheme))
                    Assert.IsNotEmpty(ControlBindings.PathOf(e, scheme), $"{scheme}: {e.label}");
        }
    }
}
