using NUnit.Framework;
using Sandspike.Art;
using Sandspike.Ball;
using Sandspike.Controls;
using Sandspike.UI;
using UnityEngine;

namespace Sandspike.Tests
{
    /// <summary>The pure rules behind the visual feedback (colours, scales, which hints are greyed ...).</summary>
    public class FeelFxTests
    {
        static void AssertColour(Color want, Color got) => Assert.That(Vector4.Distance(want, got), Is.LessThan(0.01f));

        [Test]
        public void ComboBadge_ShowsNothingOnTheFirstTouch_ThenX2AndX3()
        {
            Assert.AreEqual("", Feel.SpikeChargeFx.ComboBadge(0));
            Assert.AreEqual("×2", Feel.SpikeChargeFx.ComboBadge(1));
            Assert.AreEqual("×3", Feel.SpikeChargeFx.ComboBadge(2));
            Assert.AreEqual("×3", Feel.SpikeChargeFx.ComboBadge(5));   // never past x3
        }

        [Test]
        public void BallStretch_GrowsWithSpeedTo135At25_NeverMore_AndASquashIs08()
        {
            Assert.AreEqual(1f, Feel.BallSquash.AlongScale(0f, false), 1e-4f);
            Assert.AreEqual(1.35f, Feel.BallSquash.AlongScale(25f, false), 1e-4f);
            Assert.AreEqual(1.35f, Feel.BallSquash.AlongScale(40f, false), 1e-4f);
            Assert.That(Feel.BallSquash.AlongScale(12.5f, false), Is.EqualTo(1.175f).Within(1e-4f));
            Assert.AreEqual(0.8f, Feel.BallSquash.AlongScale(25f, true), 1e-4f);
        }

        static ControlsHint.HintState Idle() => new ControlsHint.HintState { grounded = true, canSprint = true };

        [Test]
        public void Hints_NothingIsGreyedOnTheGroundWithNothingGoingOn()
        {
            foreach (PromptAction a in System.Enum.GetValues(typeof(PromptAction)))
                if (a != PromptAction.Block) Assert.IsFalse(ControlsHint.Greyed(a, Idle()), a.ToString());
        }

        [Test]
        public void Hints_DiveIsGreyedInTheAirAndWhileServing_BumpAndSetWhileServing()
        {
            var air = Idle(); air.grounded = false;
            Assert.IsTrue(ControlsHint.Greyed(PromptAction.Dive, air));
            Assert.IsTrue(ControlsHint.Greyed(PromptAction.Jump, air));
            var serve = Idle(); serve.serving = true;
            Assert.IsTrue(ControlsHint.Greyed(PromptAction.Dive, serve));
            Assert.IsTrue(ControlsHint.Greyed(PromptAction.Bump, serve));
            Assert.IsTrue(ControlsHint.Greyed(PromptAction.Set, serve));
            Assert.IsFalse(ControlsHint.Greyed(PromptAction.Spike, serve));   // Spike tosses and serves
            Assert.IsFalse(ControlsHint.Greyed(PromptAction.Jump, serve));    // a jump serve
        }

        [Test]
        public void Hints_SprintIsGreyedWithoutStamina_BlockOnlyWhereABlockIsPossible()
        {
            var tired = Idle(); tired.canSprint = false;
            Assert.IsTrue(ControlsHint.Greyed(PromptAction.Sprint, tired));
            Assert.IsTrue(ControlsHint.Greyed(PromptAction.Block, Idle()));
            var net = Idle(); net.blockAvailable = true;
            Assert.IsFalse(ControlsHint.Greyed(PromptAction.Block, net));
        }

        [Test]
        public void LandingRing_IsWhiteUntilTheLastSecond_ThenRunsYellowOrangeRed()
        {
            AssertColour(Palette.CourtLines, LandingMarker.UrgencyColour(2.5f));
            AssertColour(Palette.CourtLines, LandingMarker.UrgencyColour(1f));
            AssertColour(Palette.Accent, LandingMarker.UrgencyColour(2f / 3f));
            AssertColour(Palette.Orange, LandingMarker.UrgencyColour(1f / 3f));
            AssertColour(Palette.TeamA, LandingMarker.UrgencyColour(0f));
        }
    }
}
