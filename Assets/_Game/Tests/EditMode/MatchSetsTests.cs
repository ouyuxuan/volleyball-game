using NUnit.Framework;
using Sandspike.Core;

namespace Sandspike.Tests
{
    public class MatchSetsTests
    {
        [Test]
        public void OneSet_EndsAtTheFirstSet_AndTheLoserServesNext()
        {
            var m = new MatchSets(1, 0);
            Assert.IsFalse(m.IsOver);
            Assert.AreEqual(0, m.NextServingTeam, "set 1 opens with the first serving team");
            m.EndSet(1, 9, 11);
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(1, m.Winner);
            Assert.IsFalse(m.IsBestOfThree);
        }

        [Test]
        public void BestOfThree_NeedsTwoSets_AndKeepsEverySetScore()
        {
            var m = new MatchSets(2, 0);
            m.EndSet(0, 11, 5);
            Assert.IsFalse(m.IsOver, "one set each way is not enough");
            Assert.AreEqual(1, m.NextServingTeam, "team B lost set 1, so it serves first in set 2");
            m.EndSet(1, 8, 11);
            Assert.IsFalse(m.IsOver, "1-1 goes to a third set");
            Assert.AreEqual(0, m.NextServingTeam, "team A lost set 2");
            m.EndSet(1, 7, 11);
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(1, m.Winner);
            Assert.AreEqual(1, m.SetsWon[0]); Assert.AreEqual(2, m.SetsWon[1]);
            Assert.AreEqual(3, m.Scores.Count);
            Assert.AreEqual(new[] { 8, 11 }, m.Scores[1]);
        }

        [Test]
        public void BestOfThree_TwoStraightSetsEndsIt()
        {
            var m = new MatchSets(2, 1);
            m.EndSet(0, 11, 0);
            m.EndSet(0, 12, 10);
            Assert.AreEqual(0, m.Winner);
            Assert.AreEqual(2, m.Scores.Count);
        }

        [Test]
        public void ASetEndsByTheRulesEngineWinRule_AndTheNextSetStartsAtZeroWithTheLoserServing()
        {
            var set1 = new RulesEngine(11, 2, 3, 4f, 8f, 0.11f, 0, false);
            set1.Score[0] = 11; set1.Score[1] = 10;
            Assert.IsFalse(set1.IsMatchOver, "11-10 is not a win by 2");
            set1.Score[1] = 9;
            Assert.IsTrue(set1.IsMatchOver);
            var m = new MatchSets(2, 0);
            m.EndSet(set1.Winner, set1.Score[0], set1.Score[1]);
            var set2 = new RulesEngine(11, 2, 3, 4f, 8f, 0.11f, m.NextServingTeam, false);
            Assert.AreEqual(0, set2.Score[0] + set2.Score[1], "scores reset");
            Assert.AreEqual(1, set2.ServingTeam, "the team that lost set 1 serves first");
        }
    }
}
