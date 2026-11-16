using NUnit.Framework;
using Sandspike.Core;

namespace Sandspike.Tests
{
    public class PointCapTests
    {
        static int WinnerAt(int target, bool cap, int a, int b)
        {
            var r = new RulesEngine(target, 2, 3, 4f, 8f, 0.11f, 0, cap);
            r.Score[0] = a; r.Score[1] = b;
            return r.Winner;
        }

        [TestCase(11, 15)]
        [TestCase(21, 25)]
        [TestCase(25, 30)]
        public void CapOn_FirstToTheCapWinsWithoutALeadOfTwo(int target, int cap)
        {
            Assert.AreEqual(0, WinnerAt(target, true, cap, cap - 1), "cap to cap-1 ends it");
            Assert.AreEqual(1, WinnerAt(target, true, cap - 1, cap));
            Assert.AreEqual(-1, WinnerAt(target, true, cap - 1, cap - 1), "level below the cap goes on");
            Assert.AreEqual(0, WinnerAt(target, true, target, target - 2), "normal win by 2 still ends it");
            Assert.AreEqual(-1, WinnerAt(target, true, target, target - 1), "one ahead below the cap goes on");
        }

        [TestCase(11)]
        [TestCase(21)]
        [TestCase(25)]
        public void CapOff_IsTheOldBehaviour(int target)
        {
            int cap = RulesEngine.CapFor(target);
            Assert.AreEqual(-1, WinnerAt(target, false, cap, cap - 1), "no cap: 15-14 goes on");
            Assert.AreEqual(0, WinnerAt(target, false, cap + 1, cap - 1), "win by 2 ends it");
            Assert.AreEqual(int.MaxValue, new RulesEngine(target).CapScore);
        }
    }
}
