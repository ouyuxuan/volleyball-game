using NUnit.Framework;
using Sandspike.Ball;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Tests
{
    public class BotAndDiveTests
    {
        // ---- the bot's hit choice by touch count ----

        [Test]
        public void FirstTouch_IsABump() => Assert.AreEqual(BotHit.Bump, BotPlanner.ChooseHit(0, false));

        [Test]
        public void SecondTouch_IsASet() => Assert.AreEqual(BotHit.Set, BotPlanner.ChooseHit(1, false));

        [Test]
        public void ThirdTouch_SendsItOver() => Assert.AreEqual(BotHit.Over, BotPlanner.ChooseHit(2, false));

        [Test]
        public void SecondTouch_IsStillASet_WhenAskedTo_EvenOnAHighBallNearTheNet()
        {
            Assert.AreEqual(BotHit.Set, BotPlanner.ChooseHit(1, true, true));
            Assert.AreEqual(BotHit.Over, BotPlanner.ChooseHit(2, true, true));
            Assert.AreEqual(BotHit.Over, BotPlanner.ChooseHit(0, true, true));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void AHighBallNearTheNet_IsSentOverAtOnce(int touches) =>
            Assert.AreEqual(BotHit.Over, BotPlanner.ChooseHit(touches, true));

        [Test]
        public void HighNearNet_NeedsBothHeightAndDistance()
        {
            Assert.IsTrue(BotPlanner.IsHighNearNet(-2f, 3.2f, 3.5f, 3.0f));
            Assert.IsFalse(BotPlanner.IsHighNearNet(-5f, 3.2f, 3.5f, 3.0f), "too far from the net");
            Assert.IsFalse(BotPlanner.IsHighNearNet(-2f, 2.0f, 3.5f, 3.0f), "too low");
        }

        [Test]
        public void SetForHuman_IsFullChargeAtTheHumansX()
        {
            BotPlanner.PlanSet(true, 2.5f, 4f, 1f, -1f, 0.54f, 1.5f, 1f, 2f, out float c, out Vector3 t);
            Assert.AreEqual(1f, c);
            Assert.AreEqual(new Vector3(2.5f, 0f, -2f), t);
            BotPlanner.PlanSet(true, 9f, 4f, 1f, 1f, 0.54f, 1.5f, 1f, 2f, out c, out t);
            Assert.AreEqual(3f, t.x, "clamped 1 m inside the court");
        }

        [Test]
        public void SetForBot_KeepsTheOldChargeAndTarget()
        {
            BotPlanner.PlanSet(false, 2.5f, 4f, 1f, -1f, 0.54f, 1.5f, 1f, 2f, out float c, out Vector3 t);
            Assert.AreEqual(0.54f, c);
            Assert.AreEqual(new Vector3(2.5f, 0f, -1.5f), t);
        }

        [Test]
        public void AimFor_UndoesTheBumpDistanceScale()
        {
            var resolver = new HitResolver(new HitTuning());
            Vector3 from = new Vector3(0f, 0f, -4f), target = new Vector3(1f, 0f, -2f);
            foreach (float charge in new[] { 0f, 0.5f, 1f })
            {
                Vector3 aim = resolver.AimFor(HitType.Bump, charge, from, target);
                var result = resolver.Resolve(new HitInput
                {
                    type = HitType.Bump, charge = charge, timingError = 0f, closestDistance = 0f, reachRadius = 1f,
                    contactHeight = 0.9f, aimPoint = aim, hitterPosition = from, ballPosition = from,
                });
                Assert.AreEqual(target.x, result.target.x, 1e-3f);
                Assert.AreEqual(target.z, result.target.z, 1e-3f);
            }
        }

        // ---- the dive and its dig ----

        [Test]
        public void Dig_ReachesFartherThanANormalHit()
        {
            Vector3 dig = new Vector3(0f, 0.35f, 0.7f);
            Vector3 ball = dig + new Vector3(1.2f, 0f, 0f);          // 1.2 m away
            Assert.IsFalse(ContactTiming.InReach(ball, dig, 1.0f), "outside a normal 1.0 m reach");
            Assert.IsTrue(ContactTiming.DigInReach(ball, dig, 1.0f, 0.3f), "inside the dig's 1.3 m reach");
            Assert.IsFalse(ContactTiming.DigInReach(dig + new Vector3(1.4f, 0f, 0f), dig, 1.0f, 0.3f));
        }

        [Test]
        public void Dive_BelowTheCostDrainsToZero()
        {
            var s = new Stamina();
            s.SpendJump(1f); s.SpendJump(1f); s.SpendJump(1f); s.SpendJump(1f);   // 100 - 100 = 0 
            s.SpendJump(1f);
            s.SpendDive();                                            // never negative
            Assert.AreEqual(0f, s.Current, 1e-4f);
        }

        [Test]
        public void Dive_CostsFifteen()
        {
            var s = new Stamina();
            s.SpendDive();
            Assert.AreEqual(85f, s.Current, 1e-4f);
        }
    }
}
