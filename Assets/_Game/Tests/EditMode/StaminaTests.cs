using NUnit.Framework;
using Sandspike.Players;

namespace Sandspike.Tests
{
    public class StaminaTests
    {
        [Test]
        public void StartsFull()
        {
            var s = new Stamina();
            Assert.AreEqual(100f, s.Current, 1e-4f);
            Assert.AreEqual(1f, s.MaxChargeAllowed, 1e-4f);
        }

        [Test]
        public void JumpCost_IsProportionalToCharge_UpTo25()
        {
            var s = new Stamina();
            s.SpendJump(1f);
            Assert.AreEqual(75f, s.Current, 1e-4f);
            s.Refill();
            s.SpendJump(0.5f);
            Assert.AreEqual(87.5f, s.Current, 1e-4f);
            s.Refill();
            s.SpendJump(0f); // a tap is free
            Assert.AreEqual(100f, s.Current, 1e-4f);
        }

        [Test]
        public void SpikeCost_IsProportionalToCharge_UpTo20()
        {
            var s = new Stamina();
            s.SpendSpike(1f);
            Assert.AreEqual(80f, s.Current, 1e-4f);
            s.Refill();
            s.SpendSpike(0.25f);
            Assert.AreEqual(95f, s.Current, 1e-4f);
        }

        [Test]
        public void Regen_WaitsHalfASecondAfterSpend()
        {
            var s = new Stamina();
            s.SpendJump(1f); // 75
            s.Tick(0.4f, false);
            Assert.AreEqual(75f, s.Current, 1e-4f); // still in the delay
            s.Tick(0.2f, false);                    // 0.1 s of delay left + 0.1 s of regen at 30/s
            Assert.AreEqual(78f, s.Current, 1e-3f);
        }

        [Test]
        public void Regen_Rates_StillIsDoubleMoving()
        {
            var s = new Stamina();
            s.SpendJump(1f);
            s.Tick(0.5f, false); // delay used up
            s.Tick(1f, true);    // +15
            Assert.AreEqual(90f, s.Current, 1e-3f);
            s.Tick(0.5f, false); // +15
            Assert.AreEqual(100f, s.Current, 1e-3f);
        }

        [Test]
        public void Regen_NeverExceedsMax()
        {
            var s = new Stamina();
            s.SpendSpike(1f);
            s.Tick(10f, false);
            Assert.AreEqual(100f, s.Current, 1e-4f);
        }

        [Test]
        public void SpendingAgain_RestartsTheDelay()
        {
            var s = new Stamina();
            s.SpendJump(1f);
            s.Tick(0.6f, false);              // 0.1 s regen -> 78
            float afterFirst = s.Current;
            s.SpendJump(0.4f);                // -10, delay restarts
            s.Tick(0.4f, false);
            Assert.AreEqual(afterFirst - 10f, s.Current, 1e-3f);
        }

        [Test]
        public void Cap_IsFullAtOrAbove40()
        {
            var s = new Stamina();
            s.SpendJump(1f); s.SpendJump(1f); s.SpendJump(1f); // 100 - 75 = 25 ... then see below
            Assert.AreEqual(25f, s.Current, 1e-4f); // 100 -> 75 -> 50 -> 25
            s.Refill();
            s.SpendJump(1f); s.SpendJump(1f); // 50
            Assert.AreEqual(1f, s.MaxChargeAllowed, 1e-4f);
        }

        [Test]
        public void Cap_ScalesLinearlyBelow40_To_Half_AtZero()
        {
            var s = new Stamina();
            // 100 - 60 = 40 -> cap 1.0 ; 20 -> cap 0.75 ; 0 -> cap 0.5
            s.SpendJump(1f); s.SpendJump(1f); // 50
            s.SpendSpike(0.5f);                // 40
            Assert.AreEqual(40f, s.Current, 1e-4f);
            Assert.AreEqual(1f, s.MaxChargeAllowed, 1e-4f);
            s.SpendSpike(1f);                  // 20
            Assert.AreEqual(0.75f, s.MaxChargeAllowed, 1e-4f);
            s.SpendSpike(1f);                  // 0
            Assert.AreEqual(0f, s.Current, 1e-4f);
            Assert.AreEqual(0.5f, s.MaxChargeAllowed, 1e-4f);
        }

        [Test]
        public void ChargeMeter_RisesToFullInPointEightSeconds()
        {
            var m = new ChargeMeter();
            m.Begin();
            m.Tick(0.4f, 1f);
            Assert.AreEqual(0.5f, m.Value, 1e-4f);
            m.Tick(0.4f, 1f);
            Assert.AreEqual(1f, m.Value, 1e-4f);
            m.Tick(1f, 1f);
            Assert.AreEqual(1f, m.Value, 1e-4f);
        }

        [Test]
        public void BumpAndSetCharge_CostUpToEight_AndCapFollowsStamina()
        {
            var s = new Stamina();
            s.SpendHit(1f);
            Assert.AreEqual(92f, s.Current, 1e-4f);
            s.SpendHit(0.5f);
            Assert.AreEqual(88f, s.Current, 1e-4f);
            // charge is capped by stamina: at 20 stamina the cap is 0.75
            s.Refill(); s.SpendJump(1f); s.SpendJump(1f); s.SpendSpike(1f); s.SpendSpike(1f); // 100-25-25-20-20 = 10
            var m = new ChargeMeter();
            m.Begin(); m.Tick(5f, s.MaxChargeAllowed);
            Assert.AreEqual(s.MaxChargeAllowed, m.Value, 1e-4f);
            Assert.Less(m.Value, 1f);
        }

        [Test]
        public void ChargeMeter_IsClampedByStaminaCap()
        {
            var m = new ChargeMeter();
            m.Begin();
            m.Tick(2f, 0.6f);
            Assert.AreEqual(0.6f, m.Value, 1e-4f);
            Assert.AreEqual(0.6f, m.Release(), 1e-4f);
            Assert.IsFalse(m.IsCharging);
        }
    }
}
