using System.Collections.Generic;
using NUnit.Framework;
using Sandspike.Core;

namespace Sandspike.Tests
{
    public class MatchStatsTests
    {
        class MemoryStore : ISettingsStore
        {
            readonly Dictionary<string, int> data = new Dictionary<string, int>();
            public string GetString(string key, string fallback) => fallback;
            public void SetString(string key, string value) { }
            public int Get(string key, int fallback) => data.TryGetValue(key, out int v) ? v : fallback;
            public void Set(string key, int value) => data[key] = value;
        }

        [Test]
        public void HumanTeamWinAndLossCount()
        {
            var s = new MatchStats();
            s.Record(true, false, 0);     // humans on A, A won
            s.Record(true, false, 1);     // humans on A, B won
            s.Record(false, true, 1);     // humans on B, B won
            Assert.AreEqual((3, 2, 1), (s.Played, s.Won, s.Lost));
        }

        [Test]
        public void HumansOnBothTeamsIsPlayedOnly()
        {
            var s = new MatchStats();
            s.Record(true, true, 0);
            Assert.AreEqual((1, 0, 0), (s.Played, s.Won, s.Lost));
        }

        [Test]
        public void NoHumansCountsNothing()
        {
            var s = new MatchStats();
            s.Record(false, false, 0);
            Assert.AreEqual(0, s.Played);
        }

        [Test]
        public void SaveLoadRoundTripAndReset()
        {
            var store = new MemoryStore();
            var a = new MatchStats();
            a.Record(true, false, 0); a.Record(true, false, 1);
            a.Save(store);
            var b = new MatchStats();
            b.Load(store);
            Assert.AreEqual((2, 1, 1), (b.Played, b.Won, b.Lost));
            Assert.AreEqual("Played 2 · Won 1 · Lost 1", b.Line);
            b.Reset();
            Assert.AreEqual("", b.Line);
        }
    }
}
