using System.Reflection;
using NUnit.Framework;
using Sandspike.Aim;
using Sandspike.CameraRig;
using Sandspike.Controls;
using Sandspike.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sandspike.Tests
{
    /// <summary>Local multiplayer: aim assist, the join slots, the split-screen rectangles, and two gamepads driving two brains.</summary>
    public class LocalPlayersTests : InputTestFixture
    {
        // ---------------- aim assist ----------------

        // Team A (side -1): the opponents are at +z. Teammate at x = 2, opponents at x = -2 and x = 2, z = 4.
        static AssistCourt Court() => new AssistCourt
        {
            halfWidth = 4f, halfLength = 8f, side = -1f,
            mate = new Vector3(2f, 0f, -4f), opponentA = new Vector3(-2f, 0f, 4f), opponentB = new Vector3(2f, 0f, 4f),
        };

        static Vector3 Assist(Vector3 raw, AssistHit hit, bool pad = true, bool on = true) => AimAssist.Apply(raw, hit, Court(), pad, on);

        [Test]
        public void AimAssist_PullsSixtyPercentTowardEachSpikeCandidate()
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 cand = AimAssist.Candidate(AssistHit.Spike, Court(), i);
                Vector3 raw = cand + new Vector3(0.5f, 0f, -0.5f);                         // inside 1.5 m, inside the court
                Vector3 got = Assist(raw, AssistHit.Spike);
                Vector3 want = Vector3.Lerp(raw, cand, 0.6f);
                Assert.AreEqual(want.x, got.x, 1e-4f, "candidate " + i);
                Assert.AreEqual(want.z, got.z, 1e-4f, "candidate " + i);
            }
        }

        [Test]
        public void AimAssist_DeepCornersAreOneMetreInsideTheLines_AndMidpointIsBetweenTheOpponents()
        {
            Assert.AreEqual(new Vector3(-3f, 0f, 7f), AimAssist.Candidate(AssistHit.Spike, Court(), 0));
            Assert.AreEqual(new Vector3(3f, 0f, 7f), AimAssist.Candidate(AssistHit.Spike, Court(), 1));
            Assert.AreEqual(new Vector3(0f, 0f, 4f), AimAssist.Candidate(AssistHit.Spike, Court(), 2));
        }

        [Test]
        public void AimAssist_SetAndFirstTouchBump_PullToTheSpotNearTheNetAtTheTeammatesX()
        {
            foreach (var hit in new[] { AssistHit.Set, AssistHit.Bump })
            {
                Assert.AreEqual(new Vector3(2f, 0f, -2f), AimAssist.Candidate(hit, Court(), 0));
                Vector3 got = Assist(new Vector3(1f, 0f, -2.5f), hit);
                Vector3 want = Vector3.Lerp(new Vector3(1f, 0f, -2.5f), new Vector3(2f, 0f, -2f), 0.6f);
                Assert.AreEqual(want.x, got.x, 1e-4f);
                Assert.AreEqual(want.z, got.z, 1e-4f);
            }
        }

        [Test]
        public void AimAssist_LeavesAimAloneOutsideTheRadius()
        {
            Vector3 raw = new Vector3(0f, 0f, 1f);                                       // 3+ m from every spike candidate
            Assert.AreEqual(raw, Assist(raw, AssistHit.Spike));
            Vector3 justOut = new Vector3(-3f + 1.51f, 0f, 7f);                          // 1.51 m from the left corner; every other candidate is further
            Assert.AreEqual(justOut, Assist(justOut, AssistHit.Spike));
        }

        [Test]
        public void AimAssist_NeverReturnsAPointOutsideTheCourt()
        {
            // the raw aim is allowed beyond the lines (the aim area is bigger than the court); a pulled point is clamped back in
            Vector3 got = Assist(new Vector3(4.2f, 0f, 7.6f), AssistHit.Spike);
            Assert.LessOrEqual(Mathf.Abs(got.x), 4f);
            Assert.LessOrEqual(Mathf.Abs(got.z), 8f);
            var c = Court();
            c.mate = new Vector3(4f, 0f, -4f);                                           // teammate on the line, raw aim just outside it
            Vector3 set = AimAssist.Apply(new Vector3(5.2f, 0f, -2f), AssistHit.Set, c, true, true);
            Assert.LessOrEqual(Mathf.Abs(set.x), 4f);
        }

        [Test]
        public void AimAssist_ReturnsTheRawAimForMouseAimWhenOffAndWithNoHit()
        {
            Vector3 raw = new Vector3(3f, 0f, 7f);                                        // right on a candidate
            Assert.AreEqual(raw, Assist(raw, AssistHit.Spike, pad: false));               // mouse
            Assert.AreEqual(raw, Assist(raw, AssistHit.Spike, on: false));                // switched off
            Assert.AreEqual(raw, Assist(raw, AssistHit.None));                            // blocking, or no ball in play
        }

        [Test]
        public void AimAssist_PicksTheNearestCandidateWhenTwoAreInRange()
        {
            var c = Court();
            c.opponentA = c.opponentB = new Vector3(2f, 0f, 7f);                          // the midpoint (2, 7) is 1 m from the right corner (3, 7)
            Vector3 mid = AimAssist.Candidate(AssistHit.Spike, c, 2), right = AimAssist.Candidate(AssistHit.Spike, c, 1);
            Vector3 raw = new Vector3(2.7f, 0f, 7f);                                      // both in range; the corner is nearer (0.3 against 0.7)
            Assert.AreEqual(Mathf.Lerp(raw.x, right.x, 0.6f), AimAssist.Apply(raw, AssistHit.Spike, c, true, true).x, 1e-4f);
            raw = new Vector3(2.2f, 0f, 7f);                                              // both in range; the midpoint is nearer
            Assert.AreEqual(Mathf.Lerp(raw.x, mid.x, 0.6f), AimAssist.Apply(raw, AssistHit.Spike, c, true, true).x, 1e-4f);
        }

        // ---------------- slots ----------------

        [Test]
        public void Slots_JoinTakesTheFirstFreeSlot_AndOnlyOnce()
        {
            var s = new PlayerSlots();
            Assert.IsTrue(s.Join(PlayerSlots.KeyboardId));
            Assert.AreEqual(0, s.SlotOf(PlayerSlots.KeyboardId));
            Assert.IsFalse(s.Join(PlayerSlots.KeyboardId), "keyboard + mouse is one person at most");
            Assert.IsTrue(s.Join(5));
            Assert.AreEqual(1, s.SlotOf(5));
            Assert.IsTrue(s.Join(6));
            Assert.IsTrue(s.Join(7));
            Assert.IsFalse(s.Join(8), "four slots only");
            Assert.AreEqual(4, s.HumanCount);
        }

        [Test]
        public void Slots_MoveGoesToTheNextFreeSlot_AndNeverOntoAnotherPlayer()
        {
            var s = new PlayerSlots();
            s.Join(PlayerSlots.KeyboardId); s.Join(5);                                   // slots 0 and 1
            Assert.IsFalse(s.Move(5, -1), "slot 0 is taken, nothing free to the left");
            Assert.IsTrue(s.Move(5, 1));
            Assert.AreEqual(2, s.SlotOf(5));
            Assert.IsTrue(s.Move(5, 1));
            Assert.AreEqual(3, s.SlotOf(5));
            Assert.IsFalse(s.Move(5, 1), "no slot beyond the last");
            Assert.IsTrue(s.Move(5, -1));
            Assert.AreEqual(2, s.SlotOf(5));
            Assert.IsFalse(s.Move(99, 1), "a device that has not joined cannot move");
        }

        [Test]
        public void Slots_MoveSkipsOverAnotherPlayerToTheNextFreeSlot()
        {
            var s = new PlayerSlots();
            s.Join(1); s.Join(2);                                                         // 0 and 1
            Assert.IsTrue(s.Move(1, 1));                                                  // 0 -> over slot 1 -> slot 2
            Assert.AreEqual(2, s.SlotOf(1));
            Assert.AreEqual(1, s.SlotOf(2));
        }

        [Test]
        public void Slots_LeaveFreesTheSlot_AndAMatchNeedsAtLeastOneHuman()
        {
            var s = new PlayerSlots();
            Assert.IsFalse(s.CanStart, "0 humans cannot start");
            s.Join(PlayerSlots.KeyboardId);
            Assert.IsTrue(s.CanStart);
            Assert.IsTrue(s.Leave(PlayerSlots.KeyboardId));
            Assert.IsFalse(s.IsHuman(0));
            Assert.IsFalse(s.CanStart);
            Assert.IsFalse(s.Leave(PlayerSlots.KeyboardId), "already left");
            Assert.IsTrue(s.Join(PlayerSlots.KeyboardId), "the keyboard can join again");
        }

        // ---------------- viewports ----------------

        [Test]
        public void SplitScreen_CoversTheScreenWithoutOverlap_ForOneToFourHumans()
        {
            for (int humans = 1; humans <= 4; humans++)
            {
                Rect[] rects = SplitScreen.Layout(humans);
                Assert.GreaterOrEqual(rects.Length, humans);
                float area = 0f;
                for (int i = 0; i < rects.Length; i++)
                {
                    area += rects[i].width * rects[i].height;
                    Assert.GreaterOrEqual(rects[i].xMin, 0f); Assert.LessOrEqual(rects[i].xMax, 1f);
                    Assert.GreaterOrEqual(rects[i].yMin, 0f); Assert.LessOrEqual(rects[i].yMax, 1f);
                    for (int j = i + 1; j < rects.Length; j++) Assert.IsFalse(rects[i].Overlaps(rects[j]), $"{humans} humans: {i} overlaps {j}");
                }
                Assert.AreEqual(1f, area, 1e-5f, humans + " humans");
            }
            Assert.AreEqual(1, SplitScreen.Layout(1).Length);
            Assert.AreEqual(2, SplitScreen.Layout(2).Length);
            Assert.AreEqual(4, SplitScreen.Layout(3).Length, "3 humans: 3 views plus the spare quarter");
            Assert.AreEqual(0.5f, SplitScreen.Layout(2)[0].width);
        }

        [Test]
        public void ViewLayers_EachCameraSeesOnlyItsOwnPersonalLayer()
        {
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                    Assert.AreEqual(a == b, (ViewLayers.MaskFor(a) & (1 << ViewLayers.For(b))) != 0, $"view {a} layer {b}");
            Assert.AreEqual(0, ViewLayers.NeutralMask & (1 << ViewLayers.For(2)));
            Assert.AreNotEqual(0, ViewLayers.NeutralMask & 1, "the default layer is always visible");
        }

        // ---------------- two gamepads, two brains ----------------

        const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        HumanBrain MakeBrain(Gamepad pad, System.Collections.Generic.List<GameObject> gos)
        {
            var go = new GameObject("human");
            gos.Add(go);
            var b = go.AddComponent<HumanBrain>();
            typeof(HumanBrain).GetMethod("Awake", Any).Invoke(b, null);       // edit mode does not call Awake by itself
            b.Bind(null, null, DeviceScope.Pad, pad);
            return b;
        }

        [Test]
        public void TwoGamepads_DriveTwoBrains_AndNeitherAffectsTheOther()
        {
            var gos = new System.Collections.Generic.List<GameObject>();
            try
            {
                var padA = InputSystem.AddDevice<Gamepad>();
                var padB = InputSystem.AddDevice<Gamepad>();
                HumanBrain a = MakeBrain(padA, gos), b = MakeBrain(padB, gos);

                Set(padA.leftStick, new Vector2(1f, 0f));
                Press(padA.buttonSouth);
                Assert.Greater(a.Move.x, 0.5f, "A's stick moves A");
                Assert.IsTrue(a.Jump.Held, "A's button is A's jump");
                Assert.AreEqual(Vector2.zero, b.Move, "...and not B");
                Assert.IsFalse(b.Jump.Held);

                Release(padA.buttonSouth);
                Set(padA.leftStick, Vector2.zero);
                Set(padB.leftStick, new Vector2(0f, -1f));
                Press(padB.buttonSouth);
                Assert.Less(b.Move.y, -0.5f);
                Assert.IsTrue(b.Jump.Held);
                Assert.AreEqual(Vector2.zero, a.Move);
                Assert.IsFalse(a.Jump.Held);
            }
            finally { foreach (var g in gos) Object.DestroyImmediate(g); }
        }
    }
}
