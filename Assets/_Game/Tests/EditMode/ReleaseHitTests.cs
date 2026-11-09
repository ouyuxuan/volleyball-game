using System.Reflection;
using NUnit.Framework;
using Sandspike.Ball;
using Sandspike.Players;
using UnityEngine;

namespace Sandspike.Tests
{
    /// <summary>Hits fire on RELEASE: holding through the contact never hits, letting go does.</summary>
    public class ReleaseHitTests
    {
        GameObject playerGo, ballGo;
        PlayerController player;
        BallController ball;
        FakeBrain brain;
        float contactTime;   // ball.elapsed at which the ball is exactly at the bump contact point

        const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static void Set(object o, string field, object v) => o.GetType().GetField(field, Any).SetValue(o, v);
        static object Get(object o, string field) => o.GetType().GetField(field, Any).GetValue(o);
        static void Call(object o, string method) => o.GetType().GetMethod(method, Any).Invoke(o, null);

        [SetUp]
        public void SetUp()
        {
            ballGo = new GameObject("ball");
            ball = ballGo.AddComponent<BallController>();
            ball.Launch(new Vector3(0f, 3f, -9f), new Vector3(0f, 0f, -1f), 1.6f, false);   // a fast, steep arc: once past, it is soon out of reach

            // The moment the descending ball is 0.9 m high (the bump contact height).
            contactTime = 0.6f;
            for (float t = 0.6f; t < 1.6f; t += 0.0005f)
                if (ball.PositionAtOffset(t).y <= 0.9f) { contactTime = t; break; }
            Vector3 at = ball.PositionAtOffset(contactTime);

            playerGo = new GameObject("player", typeof(CharacterController));
            brain = new FakeBrain();
            player = playerGo.AddComponent<PlayerController>();
            Set(player, "ball", ball);
            Call(player, "Awake");
            player.TeleportTo(new Vector3(at.x, 0f, at.z - 0.35f));   // bump contact is 0.35 m in front, 0.9 m up
            Set(player, "brain", brain);   // after Awake, which would look for a brain component
            Set(player, "grounded", true);
            Set(player, "reachRadius", 0.5f);   // tight, so a ball that has moved on is clearly out of reach
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(ballGo);
        }

        void BallAt(float t) => Set(ball, "elapsed", t);
        void Frame(bool down, bool held, bool up) { brain.bump = new ButtonState(down, held, up); Call(player, "Update"); }
        bool Queued => (bool)Get(Get(player, "pending"), "active");

        [Test]
        public void HoldingThroughTheContact_NeverHits()
        {
            BallAt(contactTime - 0.2f);
            Frame(true, true, false);
            foreach (float t in new[] { -0.1f, 0f, 0.05f, 0.3f })
            {
                BallAt(contactTime + t);
                Frame(false, true, false);
            }
            Assert.AreEqual(1, ball.LaunchCount);     // only the original launch
            Assert.IsFalse(Queued);
            Assert.IsTrue(player.SpikeCharge.IsCharging);
        }

        [Test]
        public void ReleaseWhileInReach_HitsAtOnce()
        {
            BallAt(contactTime);
            Frame(true, true, false);
            Frame(false, false, true);
            Assert.AreEqual(2, ball.LaunchCount);
            Assert.IsFalse(player.SpikeCharge.IsCharging);
        }

        [Test]
        public void EarlyRelease_QueuesTheHitForContact()
        {
            BallAt(contactTime - 0.3f);
            Frame(true, true, false);
            Frame(false, false, true);
            Assert.AreEqual(1, ball.LaunchCount);     // not hit yet
            Assert.IsTrue(Queued);                    // waiting for the ball to arrive
        }

        [Test]
        public void ReleaseAfterTheBallPassed_DoesNothing()
        {
            BallAt(contactTime + 0.3f);
            Frame(true, true, false);
            Frame(false, false, true);
            Assert.AreEqual(1, ball.LaunchCount);
            Assert.IsFalse(Queued);
            Assert.IsFalse(player.SpikeCharge.IsCharging);
            Assert.AreEqual(100f, player.Stamina.Current, 1e-4f);   // a swing at nothing is free
        }
    }
}
