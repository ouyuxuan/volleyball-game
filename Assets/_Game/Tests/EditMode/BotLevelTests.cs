using NUnit.Framework;
using Sandspike.Controls;
using Sandspike.Players;

namespace Sandspike.Tests
{
    public class BotLevelTests
    {
        [Test]
        public void EveryBotFollowsTheSetupOption_UntilItHasItsOwnLevel()
        {
            var s = new PlayerSlots();
            for (int slot = 0; slot < PlayerSlots.Count; slot++)
            {
                Assert.AreEqual(0, s.BotLevel(slot), "Match is the default");
                Assert.AreEqual(AIDifficulty.Hard, s.DifficultyFor(slot, AIDifficulty.Hard));
            }
            s.CycleBotLevel(2);                      // Match -> Easy
            Assert.AreEqual(AIDifficulty.Easy, s.DifficultyFor(2, AIDifficulty.Hard));
            Assert.AreEqual(AIDifficulty.Hard, s.DifficultyFor(1, AIDifficulty.Hard), "the other bots still follow the option");
            s.CycleBotLevel(2); Assert.AreEqual(AIDifficulty.Normal, s.DifficultyFor(2, AIDifficulty.Easy));
            s.CycleBotLevel(2); Assert.AreEqual(AIDifficulty.Hard, s.DifficultyFor(2, AIDifficulty.Easy));
            s.CycleBotLevel(2); Assert.IsNull(s.BotOverride(2), "after Hard it wraps back to Match");
            Assert.AreEqual(AIDifficulty.Easy, s.DifficultyFor(2, AIDifficulty.Easy));
        }

        [Test]
        public void CyclingBackwards_WrapsAndNamesMatchTheOrder()
        {
            var s = new PlayerSlots();
            s.CycleBotLevel(0, -1);
            Assert.AreEqual("Hard", PlayerSlots.BotLevelNames[s.BotLevel(0)]);
            Assert.AreEqual(AIDifficulty.Hard, s.BotOverride(0));
        }
    }
}
