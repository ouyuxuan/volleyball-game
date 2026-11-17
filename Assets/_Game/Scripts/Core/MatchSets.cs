using System.Collections.Generic;

namespace Sandspike.Core
{
    /// <summary>
    /// The sets of a match, as plain C# (no scene objects). One RulesEngine plays one set (win by 2, optional cap);
    /// when it ends, EndSet records the score here. "1 set" = setsToWin 1, "Best of 3" = setsToWin 2.
    /// </summary>
    public class MatchSets
    {
        public readonly int setsToWin;
        public int[] SetsWon { get; } = new int[2];
        /// <summary>The final score {team A, team B} of each finished set, in order.</summary>
        public List<int[]> Scores { get; } = new List<int[]>();
        /// <summary>Who serves first in the set about to be played: the team that lost the last set (the opening team for set 1).</summary>
        public int NextServingTeam { get; private set; }

        public MatchSets(int setsToWin, int firstServingTeam)
        {
            this.setsToWin = setsToWin;
            NextServingTeam = firstServingTeam;
        }

        public bool IsBestOfThree => setsToWin > 1;

        /// <summary>The team with enough sets, or -1.</summary>
        public int Winner => SetsWon[0] >= setsToWin ? 0 : SetsWon[1] >= setsToWin ? 1 : -1;
        public bool IsOver => Winner >= 0;

        /// <summary>A set ended: the winner's score goes in the list and the loser serves next.</summary>
        public void EndSet(int winner, int scoreA, int scoreB)
        {
            Scores.Add(new[] { scoreA, scoreB });
            SetsWon[winner]++;
            NextServingTeam = 1 - winner;
        }
    }
}
