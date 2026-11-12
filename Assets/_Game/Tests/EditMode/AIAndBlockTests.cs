using System.Reflection;
using NUnit.Framework;
using Sandspike.Ball;
using Sandspike.Core;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Tests
{
    /// <summary>M3: the AI profiles and planner, and blocking (shield maths, IsBlocking, the rebound and the rules).</summary>
    public class AIAndBlockTests
    {
        const float Dt = 1f / 60f;
        const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static void Set(object o, string field, object v) => o.GetType().GetField(field, Any).SetValue(o, v);

        // ---------------- AI: profiles ----------------

        [Test]
        public void Profiles_MatchThePinnedTable()
        {
            AIProfile e = AIProfile.For(AIDifficulty.Easy), n = AIProfile.For(AIDifficulty.Normal), h = AIProfile.For(AIDifficulty.Hard);
            Assert.AreEqual(new[] { 0.45f, 0.195f, 0.145f }, new[] { e.reactionDelay, n.reactionDelay, h.reactionDelay });
            Assert.AreEqual(new[] { 0.85f, 1f, 1f }, new[] { e.moveSpeedMultiplier, n.moveSpeedMultiplier, h.moveSpeedMultiplier });
            Assert.AreEqual(new[] { 0.25f, 0.12f, 0.05f }, new[] { e.mistakeChance, n.mistakeChance, h.mistakeChance });
            Assert.AreEqual(1.5f, e.mistakeMetres);
            Assert.AreEqual(new[] { 1.2f, 0.7f, 0.3f }, new[] { e.aimSpread, n.aimSpread, h.aimSpread });
            Assert.AreEqual(new[] { 0.2f, 0.4f, 0.6f }, new[] { e.spikeChargeMin, n.spikeChargeMin, h.spikeChargeMin });
            Assert.AreEqual(new[] { 0.6f, 0.8f, 1.0f }, new[] { e.spikeChargeMax, n.spikeChargeMax, h.spikeChargeMax });
            Assert.AreEqual(new[] { false, true, true }, new[] { e.usesSprint, n.usesSprint, h.usesSprint });
            Assert.AreEqual(new[] { 0f, 0.4f, 1f }, new[] { e.blockChance, n.blockChance, h.blockChance });
            Assert.AreEqual(0.15f, h.tipChance);
            Assert.AreEqual(0f, n.tipChance);
        }

        [Test]
        public void Easy_NeverDivesSprintsOrBlocks_HardDoes()
        {
            AIProfile e = AIProfile.For(AIDifficulty.Easy), h = AIProfile.For(AIDifficulty.Hard);
            Assert.IsFalse(BotPlanner.DiveAllowed(e, 100f, 15f));
            Assert.IsFalse(e.usesSprint);
            Assert.AreEqual(0f, e.blockChance);
            Assert.IsTrue(BotPlanner.DiveAllowed(h, 0f, 15f), "Hard dives even if it burns out");
            Assert.IsTrue(h.usesSprint);
            Assert.AreEqual(1f, h.blockChance);
        }

        [Test]
        public void Normal_DivesOnlyIfStaminaCoversTheDive()
        {
            AIProfile n = AIProfile.For(AIDifficulty.Normal);
            Assert.IsFalse(BotPlanner.DiveAllowed(n, 10f, 15f));
            Assert.IsTrue(BotPlanner.DiveAllowed(n, 15f, 15f));
        }

        [Test]
        public void ADiveIsOnlyNeededWhenOnFootIsTooFar_AndWithinDiveRange()
        {
            Assert.IsFalse(BotPlanner.NeedsDive(2f, 0.3f, 6f, 1f, 3.5f), "on foot: 1.8 m + 1 m reach covers 2 m");
            Assert.IsTrue(BotPlanner.NeedsDive(4f, 0.3f, 6f, 1f, 3.5f));
            Assert.IsFalse(BotPlanner.NeedsDive(6f, 0.3f, 6f, 1f, 3.5f), "too far even for a dive");
        }

        // ---------------- AI: reaction delay ----------------

        [Test]
        public void ReactionGate_HoldsTheOldPlanForTheDelay()
        {
            var g = new ReactionGate();
            Assert.IsFalse(g.Ready(1, 10f, 0.25f), "a new ball: wait");
            Assert.IsFalse(g.Ready(1, 10.2f, 0.25f));
            Assert.IsTrue(g.Ready(1, 10.25f, 0.25f), "delay over: replan");
            Assert.IsTrue(g.Ready(1, 11f, 0.25f));
            Assert.IsFalse(g.Ready(2, 11.1f, 0.25f), "the ball was hit again: the timer restarts");
            Assert.IsTrue(g.Ready(2, 11.35f, 0.25f));
        }

        [Test]
        public void ReactionGate_ZeroDelayIsAlwaysReady()
        {
            var g = new ReactionGate();
            Assert.IsTrue(g.Ready(5, 3f, 0f));
        }

        // ---------------- AI: attack target ----------------

        [Test]
        public void AttackTarget_StaysInsideTheCourt_ForAllLevels()
        {
            foreach (AIDifficulty d in System.Enum.GetValues(typeof(AIDifficulty)))
                foreach (float side in new[] { -1f, 1f })
                    for (int seed = 0; seed < 300; seed++)
                    {
                        Random.InitState(seed);
                        Vector3 d1 = new Vector3(Random.Range(-4f, 4f), 0f, -side * Random.Range(1f, 8f));
                        Vector3 d2 = new Vector3(Random.Range(-4f, 4f), 0f, -side * Random.Range(1f, 8f));
                        bool tip = seed % 5 == 0;
                        Vector3 t = BotPlanner.PickAttack(AIProfile.For(d), side, d1, d2, Random.Range(-4f, 4f), 4f, 8f, 1f, tip);
                        Assert.LessOrEqual(Mathf.Abs(t.x), 3f + 1e-4f, $"{d} seed {seed} x");
                        Assert.LessOrEqual(Mathf.Abs(t.z), 7f + 1e-4f, $"{d} seed {seed} z");
                        Assert.Greater(t.z * side, 0f, $"{d} seed {seed}: on the opponents' half");
                    }
        }

        [Test]
        public void EasyAttacks_DeepMiddle_HardAvoidsDefenders()
        {
            Random.InitState(1);
            Vector3 easy = BotPlanner.PickAttack(new AIProfile { attackStyle = AIDifficulty.Easy, aimSpread = 0f }, 1f, Vector3.zero, Vector3.zero, 0f, 4f, 8f, 1f, false);
            Assert.AreEqual(0f, easy.x, 1e-4f);
            Assert.AreEqual(5.6f, easy.z, 1e-4f);

            // two defenders on the left: a Hard attack goes right
            var hard = new AIProfile { attackStyle = AIDifficulty.Hard, aimSpread = 0f };
            Vector3 t = BotPlanner.PickAttack(hard, 1f, new Vector3(-3f, 0f, 4f), new Vector3(-1.5f, 0f, 6f), 0f, 4f, 8f, 1f, false);
            Assert.Greater(t.x, 1f);
        }

        [Test]
        public void SettingUpTheHuman_DoesNotDependOnDifficulty()
        {
            foreach (AIDifficulty d in System.Enum.GetValues(typeof(AIDifficulty)))
            {
                Assert.AreEqual(BotHit.Set, BotPlanner.ChooseHit(1, true, true), d.ToString());
                BotPlanner.PlanSet(true, 2.5f, 4f, 1f, -1f, 0.54f, 1.5f, 1f, 2f, out float c, out Vector3 t);
                Assert.AreEqual(1f, c);
                Assert.AreEqual(new Vector3(2.5f, 0f, -2f), t);
            }
        }

        [Test]
        public void BlockJump_IsLaterForAHigherBall_AndNeverAfterTheCrossing()
        {
            float low = BotPlanner.BlockJumpIn(0.5f, 2.6f, 2.45f, 0.9f, 25f);
            float high = BotPlanner.BlockJumpIn(0.5f, 3.2f, 2.45f, 0.9f, 25f);
            Assert.Less(low, 0.5f);
            Assert.Less(high, low, "a higher ball needs the hands higher, which takes longer to rise to: jump earlier");
        }

        // ---------------- block: shield maths ----------------

        static readonly Vector3 Centre = new Vector3(0f, 3f, -0.5f);
        static readonly Vector3 Normal = Vector3.forward;   // the blocker is on the -z side, facing +z

        static bool Cross(float x, float y)
        {
            return BlockShield.TryCross(new Vector3(x, y, 0.5f), new Vector3(x, y, -1.5f), Centre, Normal, 0.8f, 0.6f, 0.11f, out _, out _);
        }

        [Test]
        public void Shield_HitsInsideAndAtTheEdgeOfTheRectanglePlusBallRadius()
        {
            Assert.IsTrue(Cross(0f, 3f));
            Assert.IsTrue(Cross(0.50f, 3f), "0.4 half-width + 0.11 ball radius");
            Assert.IsTrue(Cross(-0.50f, 3f));
            Assert.IsTrue(Cross(0f, 3.40f), "0.3 half-height + 0.11");
            Assert.IsTrue(Cross(0f, 2.60f));
        }

        [Test]
        public void Shield_MissesJustOutsideTheEdges()
        {
            Assert.IsFalse(Cross(0.52f, 3f));
            Assert.IsFalse(Cross(-0.52f, 3f));
            Assert.IsFalse(Cross(0f, 3.42f));
            Assert.IsFalse(Cross(0f, 2.58f));
        }

        [Test]
        public void Shield_OnlyCatchesABallTravellingTowardTheBlocker()
        {
            Assert.IsFalse(BlockShield.TryCross(new Vector3(0, 3, -1.5f), new Vector3(0, 3, 0.5f), Centre, Normal, 0.8f, 0.6f, 0.11f, out _, out _), "going away");
        }

        static void Rebound(float u, out Vector3 landing, out float flight, float inSpeed = 8f)
        {
            BlockShield.Rebound(new Vector3(0f, 2.8f, -0.5f), new Vector3(0f, -2f, -inSpeed), Normal, u, -1f, new BlockTuning(),
                BallTrajectory.DefaultGravity, 0.11f, out landing, out flight);
        }

        [Test]
        public void Rebound_GoesBackToTheAttackersSide_At55PercentSpeed()
        {
            Rebound(0f, out Vector3 landing, out float flight);
            Assert.Greater(landing.z, 0f, "attacker's side");
            Assert.That(landing.z, Is.InRange(1f, 4f));
            Vector2 horizontal = new Vector2(landing.x - 0f, landing.z - -0.5f) / flight;
            Assert.AreEqual(0.55f * 8f, horizontal.magnitude, 0.05f);
            Assert.AreEqual(0f, landing.x, 1e-3f, "square on: straight back");
        }

        [Test]
        public void Rebound_DeflectsOutwardForAnOffCentreHit()
        {
            Rebound(0.4f, out Vector3 right, out _);
            Rebound(-0.4f, out Vector3 left, out _);
            Assert.Greater(right.x, 0.3f);
            Assert.Less(left.x, -0.3f);
        }

        [Test]
        public void Rebound_NeverLandsOutTheBack_EvenFromAHardSpike()
        {
            Rebound(0f, out Vector3 landing, out float flight, 30f);
            Assert.LessOrEqual(landing.z, 4f + 1e-3f);
            Assert.Greater(flight, 0f);
        }

        [Test]
        public void Rebound_AngledHandsMayStillSendItBack_ToTheBlockersSide()
        {
            // hands turned 80 degrees: the mirrored ball still heads toward the blocker's side (a soft block): no clamping to the far side
            Vector3 angled = Quaternion.Euler(0f, 80f, 0f) * Vector3.forward;
            BlockShield.Rebound(new Vector3(0f, 2.8f, -0.5f), new Vector3(0f, -2f, -8f), angled, 0f, -1f, new BlockTuning(),
                BallTrajectory.DefaultGravity, 0.11f, out Vector3 landing, out _);
            Assert.Less(landing.z, 0f);
        }

        // ---------------- block: the player ----------------

        GameObject playerGo, ballGo;
        PlayerController player;
        BallController ball;
        FakeBrain brain;

        void MakePlayer()
        {
            ballGo = new GameObject("ball");
            ball = ballGo.AddComponent<BallController>();
            playerGo = new GameObject("player", typeof(CharacterController));
            player = playerGo.AddComponent<PlayerController>();
            Set(player, "ball", ball);
            typeof(PlayerController).GetMethod("Awake", Any).Invoke(player, null);
            brain = new FakeBrain();
            Set(player, "brain", brain);
            player.TeleportTo(new Vector3(0f, 1f, -0.8f));   // 1 m up, 0.8 m from the net, on the -z side facing +z
            player.Tick(Dt);
        }

        [TearDown]
        public void TearDown()
        {
            if (playerGo != null) Object.DestroyImmediate(playerGo);
            if (ballGo != null) Object.DestroyImmediate(ballGo);
        }

        [Test]
        public void IsBlocking_AirborneNearTheNet_NoHitHeld()
        {
            MakePlayer();
            Assert.IsFalse(player.IsGrounded);
            Assert.IsTrue(player.IsBlocking);
        }

        [Test]
        public void IsBlocking_FalseOnTheGround()
        {
            MakePlayer();
            player.TeleportTo(new Vector3(0f, 0f, -0.8f));
            player.Tick(Dt);
            Assert.IsTrue(player.IsGrounded);
            Assert.IsFalse(player.IsBlocking);
        }

        [Test]
        public void IsBlocking_FalseFartherThan1_2mFromTheNet()
        {
            MakePlayer();
            player.TeleportTo(new Vector3(0f, 1f, -1.3f));
            player.Tick(Dt);
            Assert.IsFalse(player.IsBlocking);
            player.TeleportTo(new Vector3(0f, 1f, -1.1f));
            player.Tick(Dt);
            Assert.IsTrue(player.IsBlocking);
        }

        [Test]
        public void IsBlocking_FalseWhileAHitButtonIsHeld()
        {
            MakePlayer();
            brain.spike = new ButtonState(false, true, false);
            Assert.IsFalse(player.IsBlocking);
            brain.spike = default;
            Assert.IsTrue(player.IsBlocking);
        }

        [Test]
        public void IsBlocking_FalseWhileAHitIsQueued_OrDiving()
        {
            MakePlayer();
            FieldInfo f = typeof(PlayerController).GetField("pending", Any);
            object boxed = f.GetValue(player);
            boxed.GetType().GetField("active").SetValue(boxed, true);
            f.SetValue(player, boxed);
            Assert.IsFalse(player.IsBlocking, "queued");
            boxed.GetType().GetField("active").SetValue(boxed, false);
            f.SetValue(player, boxed);
            Assert.IsTrue(player.IsBlocking);
            Set(player, "diving", true);
            Assert.IsFalse(player.IsBlocking, "diving");
        }

        [Test]
        public void Strafe_ShiftsTheShieldUpTo0_35m_EitherWay()
        {
            MakePlayer();
            Set(player, "airSpeedCap", 0f);                // keep the body still so only the hands move
            brain.move = Vector2.right;
            for (int i = 0; i < 60; i++) { Set(player, "grounded", false); player.Tick(Dt); player.TeleportTo(new Vector3(0f, 1f, -0.8f)); }
            Assert.AreEqual(0.35f, player.BlockCenter.x - playerGo.transform.position.x, 0.01f);
            brain.move = Vector2.left;
            for (int i = 0; i < 120; i++) { player.Tick(Dt); player.TeleportTo(new Vector3(0f, 1f, -0.8f)); }
            Assert.AreEqual(-0.35f, player.BlockCenter.x - playerGo.transform.position.x, 0.01f);
            brain.move = new Vector2(0.5f, 0f);
            for (int i = 0; i < 120; i++) { player.Tick(Dt); player.TeleportTo(new Vector3(0f, 1f, -0.8f)); }
            Assert.AreEqual(0.175f, player.BlockCenter.x - playerGo.transform.position.x, 0.01f);
        }

        [Test]
        public void TheShield_IsAtHandHeight_AndAQuarterMetreTowardTheNet()
        {
            MakePlayer();
            Vector3 c = player.BlockCenter;
            Assert.AreEqual(playerGo.transform.position.y + 2.45f, c.y, 0.01f);
            Assert.AreEqual(playerGo.transform.position.z + 0.25f, c.z, 0.01f);
            Assert.AreEqual(Vector3.forward, player.BlockNormal);
        }

        [Test]
        public void ABallThroughTheShield_Rebounds_ToTheAttackersSide_AndRaisesOnBlocked()
        {
            MakePlayer();
            int blocked = 0;
            player.OnBlocked += _ => blocked++;
            ball.Launch(new Vector3(0f, 3.5f, 4f), new Vector3(0f, 0f, -3f), 0.8f, false, HitType.Spike);   // a spike coming at the blocker
            int launches = ball.LaunchCount;
            Vector3 c = player.BlockCenter;
            bool hit = player.TryBlock(new Vector3(c.x + 0.2f, c.y, c.z + 0.3f), new Vector3(c.x + 0.2f, c.y, c.z - 0.3f));
            Assert.IsTrue(hit);
            Assert.AreEqual(1, blocked);
            Assert.AreEqual(launches + 1, ball.LaunchCount);
            Assert.AreEqual(HitType.Block, ball.LastHitType);
            Assert.Greater(ball.LandingPoint.z, 0f, "back on the attacker's side");
            Assert.Greater(ball.LandingPoint.x, 0f, "an off-centre hit to the right goes right");
        }

        [Test]
        public void ABallThatMissesTheShield_IsNotBlocked_AndAServeIsNeverBlocked()
        {
            MakePlayer();
            ball.Launch(new Vector3(0f, 3.5f, 4f), new Vector3(0f, 0f, -3f), 0.8f, false, HitType.Spike);
            Vector3 c = player.BlockCenter;
            Assert.IsFalse(player.TryBlock(new Vector3(c.x + 0.7f, c.y, c.z + 0.3f), new Vector3(c.x + 0.7f, c.y, c.z - 0.3f)), "too wide");
            ball.Launch(new Vector3(0f, 3.5f, 4f), new Vector3(0f, 0f, -3f), 0.8f, false, HitType.Serve);
            Assert.IsFalse(player.TryBlock(new Vector3(c.x, c.y, c.z + 0.3f), new Vector3(c.x, c.y, c.z - 0.3f)), "a serve can't be blocked");
        }

        // ---------------- block: the rules ----------------

        [Test]
        public void ABlock_CountsAsATouch_AndTheBlockerMayTouchNextWithoutADoubleTouch()
        {
            var rules = new RulesEngine();
            rules.Serve(0);
            Assert.AreEqual(PointReason.None, rules.Touch(2, 1, true));    // team B player 2 blocks
            Assert.AreEqual(1, rules.TouchesFor(1));
            Assert.AreEqual(PointReason.None, rules.Touch(2, 1), "the blocker's next touch");
            Assert.AreEqual(2, rules.TouchesFor(1));
        }

        [Test]
        public void WithoutABlock_TwoTouchesInARowAreStillADoubleTouch()
        {
            var rules = new RulesEngine();
            rules.Serve(0);
            rules.Touch(2, 1);
            Assert.AreEqual(PointReason.DoubleTouch, rules.Touch(2, 1));
        }

        [Test]
        public void ABlock_AsTheFourthContact_IsAFault()
        {
            var rules = new RulesEngine();
            rules.Serve(0);
            rules.Touch(2, 1); rules.Touch(3, 1); rules.Touch(2, 1);
            Assert.AreEqual(PointReason.FourTouches, rules.Touch(3, 1, true));
        }

        [Test]
        public void ABlockThenTwoTouches_IsStillLegal_AndAThirdIsNot()
        {
            var rules = new RulesEngine();
            rules.Serve(0);
            rules.Touch(2, 1, true);
            Assert.AreEqual(PointReason.None, rules.Touch(3, 1));
            Assert.AreEqual(PointReason.None, rules.Touch(2, 1));
            Assert.AreEqual(PointReason.FourTouches, rules.Touch(3, 1));
        }

        [Test]
        public void TheMatchManager_DefaultsToNormal()
        {
            var go = new GameObject("mm");
            var mm = go.AddComponent<MatchManager>();
            Assert.AreEqual(AIDifficulty.Normal, mm.difficulty);
            Object.DestroyImmediate(go);
        }
    }
}
