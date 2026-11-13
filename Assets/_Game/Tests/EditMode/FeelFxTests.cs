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
