using System.Collections.Generic;
using NUnit.Framework;
using Sandspike.Core;

namespace Sandspike.Tests
{
    public class ShellTests
    {
        class MemoryStore : ISettingsStore
        {
            public readonly Dictionary<string, int> data = new Dictionary<string, int>();
            public int Get(string key, int fallback) => data.TryGetValue(key, out int v) ? v : fallback;
            public void Set(string key, int value) => data[key] = value;
        }

        // ---- screen flow ----

        static ScreenFlow InMatch()
        {
            var f = new ScreenFlow();
            Assert.IsTrue(f.Go(ShellScreen.Setup));
            Assert.IsTrue(f.Go(ShellScreen.Match));
            return f;
        }

        [Test]
        public void StartsOnTheTitle_AndWalksTheMainFlow()
        {
            var f = new ScreenFlow();
            Assert.AreEqual(ShellScreen.Title, f.Current);
            Assert.IsTrue(f.Go(ShellScreen.Setup));
            Assert.IsTrue(f.Go(ShellScreen.Match));
            Assert.IsTrue(f.Go(ShellScreen.Results));
            Assert.IsTrue(f.Go(ShellScreen.Match), "play again");
            Assert.IsTrue(f.Go(ShellScreen.Results));
            Assert.IsTrue(f.Go(ShellScreen.Setup), "change players");
            Assert.IsTrue(f.Go(ShellScreen.Title));
        }

        [Test]
        public void PauseIsOnlyReachableDuringAMatch()
        {
            foreach (var from in new[] { ShellScreen.Title, ShellScreen.Setup, ShellScreen.Results })
            {
                var f = new ScreenFlow();
                if (from == ShellScreen.Setup) f.Go(ShellScreen.Setup);
                if (from == ShellScreen.Results) { f.Go(ShellScreen.Setup); f.Go(ShellScreen.Match); f.Go(ShellScreen.Results); }
                Assert.IsFalse(f.Go(ShellScreen.Pause), from + " must not reach Pause");
                Assert.AreEqual(from, f.Current);
            }
            var m = InMatch();
            Assert.IsTrue(m.Go(ShellScreen.Pause));
        }

        [Test]
        public void SettingsAndHowTo_OnlyFromTitleOrPause_AndBackReturnsToTheOpener()
        {
            var f = new ScreenFlow();
            Assert.IsTrue(f.Go(ShellScreen.Settings));
            Assert.IsTrue(f.Back()); Assert.AreEqual(ShellScreen.Title, f.Current);
            Assert.IsTrue(f.Go(ShellScreen.HowTo));
            Assert.IsFalse(f.Go(ShellScreen.Settings), "no jumping between the two");
            Assert.IsTrue(f.Back()); Assert.AreEqual(ShellScreen.Title, f.Current);

            f = InMatch();
            f.Go(ShellScreen.Pause);
            f.Go(ShellScreen.Settings);
            Assert.IsTrue(f.Back()); Assert.AreEqual(ShellScreen.Pause, f.Current);
            f.Go(ShellScreen.HowTo);
            Assert.IsTrue(f.Back()); Assert.AreEqual(ShellScreen.Pause, f.Current);

            f = InMatch();
            Assert.IsFalse(f.Go(ShellScreen.Settings), "not from inside the match");
            f.Go(ShellScreen.Results);
            Assert.IsFalse(f.Go(ShellScreen.HowTo), "not from Results");
        }

        [Test]
        public void BackFromEachScreen()
        {
            var f = new ScreenFlow();
            Assert.IsFalse(f.Back(), "Title has nowhere to go back to");
            f.Go(ShellScreen.Setup);
            Assert.IsTrue(f.Back()); Assert.AreEqual(ShellScreen.Title, f.Current);

            f = InMatch();
            Assert.IsFalse(f.Back(), "Back does nothing in the match itself (Esc pauses instead)");
            f.Go(ShellScreen.Pause);
            Assert.IsTrue(f.Back()); Assert.AreEqual(ShellScreen.Match, f.Current, "Back from Pause resumes");
            f.Go(ShellScreen.Pause); f.Go(ShellScreen.Confirm);
            Assert.IsTrue(f.Back()); Assert.AreEqual(ShellScreen.Pause, f.Current, "Back from the confirm answers No");
            f.Go(ShellScreen.Confirm);
            Assert.IsTrue(f.Go(ShellScreen.Title), "Yes");
        }

        [Test]
        public void QuitIsOnlyOnTheTitle_AndTheConfirmStartsOnNo()
        {
            var f = new ScreenFlow();
            Assert.IsTrue(f.CanQuit);
            f.Go(ShellScreen.Setup);
            Assert.IsFalse(f.CanQuit);
            Assert.AreEqual("No", ScreenFlow.ConfirmChoices[ScreenFlow.ConfirmDefault]);
            Assert.IsFalse(new ScreenFlow().Go(ShellScreen.Confirm), "the confirm is only reached from Pause");
        }

        // ---- settings ----

        [Test]
        public void Defaults_AreMusic25_Effects80_AimAssistOn()
        {
            var s = new GameSettings();
            Assert.AreEqual(25, s.MusicPercent);
            Assert.AreEqual(80, s.EffectsPercent);
            Assert.IsTrue(s.AimAssist);
        }

        [Test]
        public void Volumes_ClampToZeroToHundred_AndSnapToSteps()
        {
            var s = new GameSettings { MusicPercent = 140, EffectsPercent = -30 };
            Assert.AreEqual(100, s.MusicPercent);
            Assert.AreEqual(0, s.EffectsPercent);
            s.MusicPercent = 42;
            Assert.AreEqual(40, s.MusicPercent);
        }

        [Test]
        public void SaveThenLoad_ReturnsTheSameValues_AndAnEmptyStoreGivesDefaults()
        {
            var store = new MemoryStore();
            var a = new GameSettings { MusicPercent = 55, EffectsPercent = 10, AimAssist = false, Fullscreen = false };
            a.Save(store);
            var b = new GameSettings();
            b.Load(store);
            Assert.AreEqual(55, b.MusicPercent);
            Assert.AreEqual(10, b.EffectsPercent);
            Assert.IsFalse(b.AimAssist);
            Assert.IsFalse(b.Fullscreen);

            var c = new GameSettings { MusicPercent = 90 };
            c.Load(new MemoryStore());
            Assert.AreEqual(25, c.MusicPercent);
            a.ResetToDefaults();
            Assert.AreEqual(80, a.EffectsPercent);
            Assert.IsTrue(a.AimAssist);
        }

        [Test]
        public void FeedbackToggles_DefaultOn_SaveLoadAndReset()
        {
            var s = new GameSettings();
            Assert.IsTrue(s.LandingRing && s.BallTrail && s.InOutLabel);
            s.LandingRing = false; s.InOutLabel = false;
            var store = new MemoryStore();
            s.Save(store);
            var b = new GameSettings();
            b.Load(store);
            Assert.IsFalse(b.LandingRing); Assert.IsTrue(b.BallTrail); Assert.IsFalse(b.InOutLabel);
            b.ResetToDefaults();
            Assert.IsTrue(b.LandingRing && b.BallTrail && b.InOutLabel);
        }
    }
}
