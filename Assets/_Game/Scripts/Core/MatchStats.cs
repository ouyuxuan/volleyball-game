namespace Sandspike.Core
{
    /// <summary>Matches played, won and lost, from the point of view of the human team(s). Plain C#, saved through an ISettingsStore.</summary>
    public class MatchStats
    {
        /// <summary>The stats the game is using now (the Shell loads it; the Title and Results screens read it).</summary>
        public static MatchStats Current { get; } = new MatchStats();

        public int Played { get; private set; }
        public int Won { get; private set; }
        public int Lost { get; private set; }

        /// <summary>
        /// A match reached match over. Call it once per match (never for practice or a quit to the title).
        /// Humans on both teams = played only; otherwise a win if the humans' team won, a loss if not. No humans = nothing counts.
        /// </summary>
        public void Record(bool humansOnTeamA, bool humansOnTeamB, int winningTeam)
        {
            if (!humansOnTeamA && !humansOnTeamB) return;
            Played++;
            if (humansOnTeamA && humansOnTeamB) return;
            bool humansWon = (winningTeam == 0) == humansOnTeamA;
            if (humansWon) Won++; else Lost++;
        }

        public void Reset() { Played = Won = Lost = 0; }

        /// <summary>"Played 12 · Won 7 · Lost 5", or empty while nothing was played.</summary>
        public string Line => Played == 0 ? "" : $"Played {Played} · Won {Won} · Lost {Lost}";

        public void Save(ISettingsStore s) { s.Set("statsPlayed", Played); s.Set("statsWon", Won); s.Set("statsLost", Lost); }

        public void Load(ISettingsStore s) { Played = s.Get("statsPlayed", 0); Won = s.Get("statsWon", 0); Lost = s.Get("statsLost", 0); }
    }
}
