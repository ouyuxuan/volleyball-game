using NUnit.Framework;
using Sandspike.Core;

namespace Sandspike.Tests
{
    public class RulesEngineTests
    {
        // Players 0 and 1 are team A (0), players 2 and 3 are team B (1).
        static RulesEngine Started(int target = 11, int firstServe = 0)
        {
            var r = new RulesEngine(target, 2, 3, 4f, 8f, 0.11f, firstServe);
            r.Serve(firstServe);
            return r;
        }

        // ---- touches ----

        [Test]
        public void ThreeTouchesBySameTeamAreFine_FourthIsAFault()
        {
            var r = Started();
            Assert.AreEqual(PointReason.None, r.Touch(2, 1));   // B receives the serve
            Assert.AreEqual(PointReason.None, r.Touch(3, 1));
            Assert.AreEqual(PointReason.None, r.Touch(2, 1));
            Assert.AreEqual(PointReason.FourTouches, r.Touch(3, 1));
            Assert.AreEqual(1, r.Score[0], "team A gets the point");
            Assert.AreEqual(0, r.Score[1]);
        }

        [Test]
        public void SamePlayerTwiceInARow_IsADoubleTouch()
        {
            var r = Started();
            Assert.AreEqual(PointReason.None, r.Touch(2, 1));
            Assert.AreEqual(PointReason.DoubleTouch, r.Touch(2, 1));
            Assert.AreEqual(1, r.Score[0]);
        }

        [Test]
        public void FirstTouchAfterAServeCountsNormally()
        {
            var r = Started();                               // team A serves (player 0)
            Assert.AreEqual(PointReason.None, r.Touch(2, 1)); // receiver touches once: no fault
            Assert.AreEqual(1, r.Touches);
            Assert.AreEqual(2, r.LastToucherId);
        }

        [Test]
        public void SamePlayerOnTheNextPossession_IsNotADoubleTouch()
        {
            var r = Started();
            r.Touch(2, 1);
            r.Touch(0, 0);   // ball back to team A
            Assert.AreEqual(PointReason.None, r.Touch(2, 1), "player 2 touches again, but after the other team");
        }

        [Test]
        public void TouchesResetWhenTheBallChangesSides()
        {
            var r = Started();
            r.Touch(2, 1); r.Touch(3, 1); r.Touch(2, 1);     // B used 3 touches
            Assert.AreEqual(PointReason.None, r.Touch(0, 0)); // A's first touch
            Assert.AreEqual(1, r.Touches);
            Assert.AreEqual(0, r.TouchesFor(1));
        }

        // ---- landing: in / out / line ----

        [Test]
        public void LandingInTheOpponentsHalf_ScoresForTheOtherTeam()
        {
            var r = Started();
            Assert.AreEqual(PointReason.BallIn, r.BallLanded(1f, 3f));   // z > 0 is team B's half
            Assert.AreEqual(1, r.Score[0]);
            Assert.AreEqual(0, r.Score[1]);
        }

        [Test]
        public void LandingInYourOwnHalf_ScoresForTheOpponent()
        {
            var r = Started();
            r.Touch(2, 1);
            Assert.AreEqual(PointReason.BallIn, r.BallLanded(0f, 5f));   // B's own half
            Assert.AreEqual(1, r.Score[0]);
        }

        [Test]
        public void LineBallIsIn_UsingTheBallRadius()
        {
            var r = Started();
            Assert.IsTrue(r.IsInside(4.0f, 7.9f));
            Assert.IsTrue(r.IsInside(4.1f, 0f), "the ball's edge still touches the line");
            Assert.IsTrue(r.IsInside(0f, -8.1f));
            Assert.IsFalse(r.IsInside(4.2f, 0f));
            Assert.IsFalse(r.IsInside(0f, 8.2f));
        }

        [Test]
        public void LineBall_ScoresAsIn()
        {
            var r = Started();
            Assert.AreEqual(PointReason.BallIn, r.BallLanded(4.05f, 7.95f));
        }

        [Test]
        public void OutBall_IsAPointAgainstTheLastToucher()
        {
            var r = Started();
            r.Touch(2, 1);                                    // B touched it last
            Assert.AreEqual(PointReason.BallOut, r.BallLanded(6f, 2f));
            Assert.AreEqual(1, r.Score[0], "team A scores: B hit it out");
            Assert.AreEqual(0, r.Score[1]);
        }

        [Test]
        public void ServeThatLandsOut_IsTheServersFault()
        {
            var r = Started();
            Assert.AreEqual(PointReason.BallOut, r.BallLanded(0f, 9f));
            Assert.AreEqual(1, r.Score[1], "team B scores: A served out");
        }

        // ---- winning ----

        static void Win(RulesEngine r, int team, int points)
        {
            for (int i = 0; i < points; i++)
            {
                r.Serve(r.ServingTeam);
                r.BallLanded(0f, team == 0 ? 3f : -3f);   // lands in the loser's half
            }
        }

        [TestCase(11)]
        [TestCase(21)]
        [TestCase(25)]
        public void WinByTwo_AtEachMatchLength(int target)
        {
            var r = new RulesEngine(target, 2);
            Win(r, 0, target - 1); Win(r, 1, target - 1);     // target-1 each
            Assert.IsFalse(r.IsMatchOver);
            Win(r, 0, 1);                                      // target to target-1: not over (must win by 2)
            Assert.AreEqual(target, r.Score[0]);
            Assert.IsFalse(r.IsMatchOver, "11-10 continues");
            Win(r, 1, 1);                                      // tied at the target
            Assert.IsFalse(r.IsMatchOver);
            Win(r, 0, 1); Assert.IsFalse(r.IsMatchOver, "one point ahead is not enough");
            Win(r, 0, 1);
            Assert.IsTrue(r.IsMatchOver, "two clear: over");
            Assert.AreEqual(0, r.Winner);
            Assert.AreEqual(target + 2, r.Score[0]);
        }

        [Test]
        public void Eleven_Nine_EndsImmediately()
        {
            var r = new RulesEngine(11, 2);
            Win(r, 1, 9); Win(r, 0, 9);
            Win(r, 0, 1); Assert.IsFalse(r.IsMatchOver);       // 10-9
            Win(r, 0, 1); Assert.IsTrue(r.IsMatchOver);        // 11-9
            Assert.AreEqual(0, r.Winner);
        }

        [Test]
        public void ElevenTen_Continues_TwelveTen_Wins()
        {
            var r = new RulesEngine(11, 2);
            Win(r, 0, 10); Win(r, 1, 10);
            Win(r, 0, 1);
            Assert.IsFalse(r.IsMatchOver, "11-10 continues");
            Win(r, 0, 1);
            Assert.IsTrue(r.IsMatchOver, "12-10 wins");
            Assert.AreEqual(0, r.Winner);
        }

        [Test]
        public void TwentyFive_TwentyFour_Continues()
        {
            var r = new RulesEngine(25, 2);
            Win(r, 1, 24); Win(r, 0, 24);
            Win(r, 0, 1);
            Assert.IsFalse(r.IsMatchOver, "25-24 continues");
            Win(r, 0, 1);
            Assert.IsTrue(r.IsMatchOver, "26-24 wins");
        }

        // ---- serving ----

        [Test]
        public void ServingTeamKeepsTheServe_AndTheSameServer_WhenItWinsTheRally()
        {
            var r = Started();
            Assert.AreEqual(0, r.ServerId);
            r.BallLanded(0f, 3f);                              // A wins
            Assert.AreEqual(0, r.ServingTeam);
            Assert.AreEqual(0, r.ServerId);
        }

        [Test]
        public void SideOut_ChangesTheServingTeam()
        {
            var r = Started();
            r.BallLanded(0f, -3f);                             // A served, B wins the rally
            Assert.AreEqual(1, r.ServingTeam);
        }

        [Test]
        public void ServerRotatesEachTimeATeamWinsBackTheServe()
        {
            var r = Started();
            Assert.AreEqual(0, r.ServerId);                    // A: player 0
            r.BallLanded(0f, -3f);                             // B wins: side out
            int firstB = r.ServerId;
            Assert.AreEqual(1, r.ServingTeam);
            r.Serve(1); r.BallLanded(0f, 3f);                  // A wins it back: side out
            Assert.AreEqual(0, r.ServingTeam);
            Assert.AreEqual(1, r.ServerId, "team A rotates to its second player");
            r.Serve(0); r.BallLanded(0f, -3f);                 // B wins: side out again
            Assert.AreNotEqual(firstB, r.ServerId, "team B rotates too");
            Assert.AreEqual(1, RulesEngine.TeamOf(r.ServerId));
            r.Serve(1); r.BallLanded(0f, 3f);                  // A wins back
            Assert.AreEqual(0, r.ServerId, "A rotates back to player 0");
        }

        [Test]
        public void EventsAfterThePointAreIgnoredUntilTheNextServe()
        {
            var r = Started();
            r.BallLanded(0f, 3f);
            Assert.AreEqual(PointReason.None, r.BallLanded(0f, 3f));
            Assert.AreEqual(PointReason.None, r.Touch(2, 1));
            Assert.AreEqual(1, r.Score[0]);
        }
    }
}
