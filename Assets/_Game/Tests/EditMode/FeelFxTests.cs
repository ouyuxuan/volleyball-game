using NUnit.Framework;
using Sandspike.Art;
using Sandspike.Ball;
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
