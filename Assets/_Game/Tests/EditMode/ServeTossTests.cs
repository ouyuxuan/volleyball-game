using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sandspike.Ball;
using Sandspike.Core;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Tests
{
    /// <summary>The toss serve: toss meter, dropped toss and serve clock faults, server-only touches, and the serve maths from the real contact point.</summary>
    public class ServeTossTests
    {
        const float Dt = 1f / 60f;
        const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static void Set(object o, string field, object v) => o.GetType().GetField(field, Any).SetValue(o, v);

        GameObject ballGo;
        BallController ball;
        readonly List<GameObject> gos = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            ballGo = new GameObject("ball");
            ball = ballGo.AddComponent<BallController>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var g in gos) if (g != null) Object.DestroyImmediate(g);
            Object.DestroyImmediate(ballGo);
        }

        PlayerController MakePlayer(out FakeBrain brain, float z = -9f, bool server = true, float side = -1f)
        {
            var go = new GameObject("player", typeof(CharacterController));
            gos.Add(go);
            var p = go.AddComponent<PlayerController>();
            Set(p, "ball", ball);
            Set(p, "courtSide", side);
            typeof(PlayerController).GetMethod("Awake", Any).Invoke(p, null);
            brain = new FakeBrain();
            Set(p, "brain", brain);
            p.TeleportTo(new Vector3(0f, 0f, z));
            go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -side), Vector3.up);
            Set(p, "grounded", true);
            p.ServeMode = server;
            return p;
        }

        // Press Spike, hold for 'charge' of the toss meter, release.
        void Toss(PlayerController p, FakeBrain b, float charge)
        {
            ball.PlaceAt(p.ContactPointAt(HitType.Serve) + Vector3.up * 0.15f);
            b.spike = new ButtonState(true, true, false); p.Tick(Dt);
            int frames = Mathf.RoundToInt(charge * 0.8f / Dt);
            b.spike = new ButtonState(false, true, false);
            for (int i = 0; i < frames; i++) p.Tick(Dt);
            b.spike = new ButtonState(false, false, true); p.Tick(Dt);
            b.spike = default;
        }

        // Puts 'p' at z with its feet at feetY and launches a toss-type ball so it is exactly at p's spike contact point right now.
        void ToContact(PlayerController p, float z, float feetY)
        {
            p.TeleportTo(new Vector3(0f, feetY, z));
            p.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -p.CourtSide), Vector3.up);
            Set(p, "grounded", feetY <= 0.001f);
            Set(p, "verticalVelocity", 0f);
            Set(p, "cooldownLeft", 0f);
            Vector3 c = p.ContactPointAt(HitType.Spike);
            ball.Launch(c, new Vector3(c.x, 0f, c.z - p.CourtSide * 1.5f), 1.6f, false, HitType.Toss);
        }

        // ---------------- the toss ----------------

        [TestCase(0f, 3.6f, 0.8f)]
        [TestCase(0.5f, 4.8f, 1.65f)]
        [TestCase(1f, 6.0f, 2.5f)]
        public void Toss_ApexAndDistanceFollowTheCharge_TowardTheNet(float charge, float apex, float distance)
        {
            var p = MakePlayer(out var b);
            Toss(p, b, charge);
            Assert.IsTrue(ball.IsInFlight);
            Assert.AreEqual(HitType.Toss, ball.LastHitType);
            Assert.AreEqual(apex, BallTrajectory.ApexHeight(ball.Position, ball.Velocity, ball.Gravity), 0.1f);
            Assert.AreEqual(-9f + distance, ball.LandingPoint.z, 0.1f, "lands in front of the server, toward the net");
            Assert.AreEqual(ball.Position.x, ball.LandingPoint.x, 0.01f, "straight, not toward the aim point");
            Assert.AreEqual(100f, p.Stamina.Current, 1e-3f, "a toss costs no stamina");
        }

        [Test]
        public void Toss_IsNotATouch_AndTheServerStaysInServeMode()
        {
            var p = MakePlayer(out var b);
            int touches = 0;
            p.OnBallTouched += _ => touches++;
            Toss(p, b, 0.6f);
            Assert.AreEqual(0, touches);
            Assert.IsTrue(p.Tossed);
            Assert.IsTrue(p.ServeMode);
            var rules = new RulesEngine();
            rules.Serve(0);                       // the MatchManager feeds the rules nothing at the toss
            Assert.IsTrue(rules.RallyActive);
            Assert.AreEqual(0, rules.Touches);
            Assert.AreEqual(0, rules.TouchesFor(1));
        }

        [Test]
        public void Serve_IsNotAReceivingTeamTouch()
        {
            // The MatchManager never feeds the serve to RulesEngine.Touch, so the receivers start from zero.
            var rules = new RulesEngine();
            rules.Serve(0);
            Assert.AreEqual(0, rules.TouchesFor(1));
            Assert.AreEqual(PointReason.None, rules.Touch(2, 1));
            Assert.AreEqual(PointReason.None, rules.Touch(3, 1));
            Assert.AreEqual(PointReason.None, rules.Touch(2, 1));
            Assert.AreEqual(3, rules.TouchesFor(1));
        }

        [Test]
        public void BeforeTheToss_TheServerCannotWalkInsideTheBaseline_AfterIt_TheNormalClampApplies()
        {
            var p = MakePlayer(out var b);
            p.TeleportTo(new Vector3(0f, 0f, -3f));
            Assert.AreEqual(-8.1f, p.transform.position.z, 1e-3f);
            Toss(p, b, 0f);
            p.TeleportTo(new Vector3(0f, 0f, -3f));
            Assert.AreEqual(-3f, p.transform.position.z, 1e-3f);
        }

        // ---------------- faults ----------------

        [Test]
        public void ADroppedToss_IsAServeFault_ForTheReceivers_AndTheServeChangesSide()
        {
            var rules = new RulesEngine();
            rules.Serve(0);
            Assert.AreEqual(PointReason.ServeFault, rules.ServeFault());
            Assert.AreEqual(1, rules.LastPointWinner);
            Assert.AreEqual(1, rules.Score[1]);
            Assert.AreEqual(1, rules.ServingTeam);
            Assert.AreEqual("fault", MatchManager.ReasonText(PointReason.ServeFault, false));
        }

        MatchManager MakeMatch(out FakeBrain[] brains)
        {
            var mgo = new GameObject("match");
            gos.Add(mgo);
            var m = mgo.AddComponent<MatchManager>();
            var players = new PlayerController[4];
            brains = new FakeBrain[4];
            for (int i = 0; i < 4; i++) players[i] = MakePlayer(out brains[i], -4f, false, i < 2 ? -1f : 1f);
            Set(m, "ball", ball);
            Set(m, "players", players);
            typeof(MatchManager).GetMethod("Start", Any).Invoke(m, null);
            return m;
        }

        [Test]
        public void MatchManager_DroppedToss_IsAServeFault_AndTheTossDoesNotStartTheRally()
        {
            var m = MakeMatch(out var brains);
            PlayerController server = m.Server;
            Toss(server, brains[server.PlayerId], 0.5f);
            Assert.AreEqual(MatchState.Serve, m.State);
            Assert.IsTrue(server.Tossed);
            typeof(MatchManager).GetMethod("OnLanded", Any).Invoke(m, new object[] { new Vector3(0f, 0f, -7f) });
            Assert.AreEqual(MatchState.PointScored, m.State);
            Assert.AreEqual(PointReason.ServeFault, m.Rules.LastReason);
            Assert.AreEqual(1, m.Rules.LastPointWinner);
            Assert.AreEqual(1, m.Rules.ServingTeam);
        }

        [Test]
        public void MatchManager_ServeClockRunningOut_IsAServeFault()
        {
            var m = MakeMatch(out _);
            Assert.AreEqual(15f, m.ServeClockLeft, 1e-3f);
            typeof(MatchManager).GetProperty("ServeClockLeft").SetValue(m, 0f);
            typeof(MatchManager).GetMethod("Update", Any).Invoke(m, null);
            Assert.AreEqual(MatchState.PointScored, m.State);
            Assert.AreEqual(PointReason.ServeFault, m.Rules.LastReason);
            Assert.AreEqual(1, m.Rules.ServingTeam);
        }

        [Test]
        public void MatchManager_HittingTheToss_StartsTheRally()
        {
            var m = MakeMatch(out var brains);
            PlayerController server = m.Server;
            Toss(server, brains[server.PlayerId], 0.5f);
            ToContact(server, server.CourtSide * 9f, 0f);
            brains[server.PlayerId].aim = new Vector3(0f, 0f, -server.CourtSide * 5f);
            Assert.IsTrue(server.AttemptHit(HitType.Spike, 0.5f));
            Assert.AreEqual(MatchState.Rally, m.State);
            Assert.AreEqual(HitType.Serve, ball.LastHitType);
        }

        // ---------------- only the server ----------------

        [Test]
        public void OnlyTheServerCanHitTheToss_AndItIsNeverBlocked()
        {
            var server = MakePlayer(out var sb);
            Toss(server, sb, 0.5f);
            var other = MakePlayer(out _, -9f, false);
            ToContact(other, -9f, 0f);                           // the toss is right at a non-server's hand
            int launches = ball.LaunchCount;
            Assert.IsFalse(other.AttemptHit(HitType.Spike, 1f));
            Assert.IsFalse(other.AttemptHit(HitType.Bump, 1f));
            Assert.AreEqual(launches, ball.LaunchCount);

            var blocker = MakePlayer(out _, -0.8f, false);
            Set(blocker, "grounded", false);
            blocker.TeleportTo(new Vector3(0f, 1.5f, -0.8f));
            ball.Launch(new Vector3(0f, 3.5f, 4f), new Vector3(0f, 0f, -3f), 0.8f, false, HitType.Toss);
            Vector3 c = blocker.BlockCenter;
            Assert.IsFalse(blocker.TryBlock(new Vector3(c.x, c.y, c.z + 0.3f), new Vector3(c.x, c.y, c.z - 0.3f)));
        }

        [Test]
        public void ServerBumpAndSet_DoNothing()
        {
            var p = MakePlayer(out var b);
            Toss(p, b, 0.5f);
            b.bump = new ButtonState(true, true, false); p.Tick(Dt);
            Assert.IsFalse(p.SpikeCharge.IsCharging);
            b.bump = default;
        }

        // ---------------- the hit ----------------

        [Test]
        public void ServeGrid_ClearsTheNet_AndLandsIn_StandingAndAirborne()
        {
            int count = 0;
            foreach (float serverZ in new[] { -9f, -6f, -3f })
            foreach (float feetY in new[] { 0f, 0.9f, 1.6f })
            foreach (float charge in new[] { 0f, 0.5f, 1f })
            for (float x = -4f; x <= 4.001f; x += 2f)
            for (float z = 0.5f; z <= 7.9f; z += 1.2f)
            {
                var p = MakePlayer(out var b, -9f);
                Toss(p, b, 0.5f);                                // sets Tossed (the real toss), then the ball is moved to the contact point
                ToContact(p, serverZ, feetY);
                b.aim = new Vector3(x, 0f, z);
                string what = $"serverZ={serverZ} feetY={feetY} charge={charge} aim=({x},{z})";
                Assert.IsTrue(p.AttemptHit(HitType.Spike, charge), "no contact: " + what);
                Assert.AreEqual(HitType.Serve, ball.LastHitType, what);
                Assert.IsFalse(ball.WillHitNet, "serve hits the net: " + what);
                var rules = new RulesEngine();
                rules.Serve(0);
                Assert.AreEqual(PointReason.BallIn, rules.BallLanded(ball.LandingPoint.x, ball.LandingPoint.z), "serve lands out: " + what + " " + ball.LandingPoint);
                count++;
                Object.DestroyImmediate(p.gameObject);
            }
            Debug.Log($"[ServeTest] {count} serves checked");
        }

        [Test]
        public void JumpServe_IsFlatter_AtFullCharge_EqualAtZero_AndUnderTheSpeedCap()
        {
            var p = MakePlayer(out var b);
            ball.PlaceAt(p.ContactPointAt(HitType.Serve));
            int flatter = 0, total = 0;
            foreach (float y in new[] { 2.3f, 3.2f, 3.9f })
            for (float x = -3f; x <= 3.001f; x += 3f)
            for (float z = 3f; z <= 7f; z += 2f)
            {
                b.aim = new Vector3(x, 0f, z);
                Vector3 start = new Vector3(0f, y, -8.7f);
                HitResult stand = p.PredictServe(1f, start, false);
                HitResult stand0 = p.PredictServe(0f, start, false);
                HitResult air1 = p.PredictServe(1f, start, true);
                HitResult air0 = p.PredictServe(0f, start, true);
                Assert.AreEqual(stand0.flightTime, air0.flightTime, 1e-4f);
                Assert.LessOrEqual(air1.flightTime, stand.flightTime + 1e-4f);
                if (air1.flightTime < stand.flightTime - 0.04f) flatter++;
                total++;
                float flat = new Vector2(air1.target.x - start.x, air1.target.z - start.z).magnitude;
                float standSpeed = flat / stand.flightTime;
                Assert.LessOrEqual(flat / air1.flightTime, Mathf.Max(22f, standSpeed) + 0.01f);
            }
            Assert.Greater(flatter, total / 2, "most jump serves should be noticeably flatter");
        }
    }
}
