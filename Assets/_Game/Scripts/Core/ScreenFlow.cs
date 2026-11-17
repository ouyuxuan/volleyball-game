namespace Sandspike.Core
{
    /// <summary>The screens of the shell. Match = no menu is up (the match is on, or being set up behind a menu).</summary>
    public enum ShellScreen { Title, Setup, Match, Pause, Confirm, Settings, HowTo, Results, Camera, PracticePause, Controls, ConfirmReset }

    /// <summary>
    /// Which menu screen is showing and where each one can go. Plain C# (no Unity objects), so it is easy to test.
    /// Title -> Setup -> Match -> Results, Pause during a match, Settings and How to Play from Title or Pause (Camera from Settings).
    /// </summary>
    public class ScreenFlow
    {
        public ShellScreen Current { get; private set; } = ShellScreen.Title;
        ShellScreen opener = ShellScreen.Title;      // where Settings / How to Play were opened from: Back returns there

        /// <summary>"Back to Title" asks first, and the answer on screen starts on No.</summary>
        public static readonly string[] ConfirmChoices = { "No", "Yes" };
        public const int ConfirmDefault = 0;

        /// <summary>Quit is only offered on the title screen.</summary>
        public bool CanQuit => Current == ShellScreen.Title;

        static bool Allowed(ShellScreen from, ShellScreen to)
        {
            switch (from)
            {
                case ShellScreen.Title: return to == ShellScreen.Setup || to == ShellScreen.HowTo || to == ShellScreen.Settings || to == ShellScreen.Match;   // Match: Practice starts straight from the Title
                case ShellScreen.Setup: return to == ShellScreen.Title || to == ShellScreen.Match;
                case ShellScreen.Match: return to == ShellScreen.Pause || to == ShellScreen.PracticePause || to == ShellScreen.Results;   // Pause only during a match
                case ShellScreen.Pause: return to == ShellScreen.Match || to == ShellScreen.Settings || to == ShellScreen.HowTo || to == ShellScreen.Confirm;
                case ShellScreen.Confirm: return to == ShellScreen.Pause || to == ShellScreen.Title;
                case ShellScreen.PracticePause: return to == ShellScreen.Match || to == ShellScreen.Settings || to == ShellScreen.HowTo || to == ShellScreen.Title;   // nothing to lose in practice: no "Are you sure?"
                case ShellScreen.Results: return to == ShellScreen.Match || to == ShellScreen.Setup || to == ShellScreen.Title;
                case ShellScreen.Settings: return to == ShellScreen.Camera || to == ShellScreen.Controls;       // the Camera and Controls pages open from Settings
                case ShellScreen.Controls: return to == ShellScreen.ConfirmReset;       // "Reset to defaults" asks first
                case ShellScreen.ConfirmReset: return to == ShellScreen.Controls;
                default: return false;      // Settings and How to Play only go Back
            }
        }

        /// <summary>Moves to another screen if the flow allows it; false (and no change) if not.</summary>
        public bool Go(ShellScreen to)
        {
            if (!Allowed(Current, to)) return false;
            if (to == ShellScreen.Settings || to == ShellScreen.HowTo) opener = Current;
            Current = to;
            return true;
        }

        /// <summary>Esc / East: Setup -> Title, Pause / PracticePause -> Match (resume), Confirm -> Pause, Settings / How to Play -> where they were opened. False elsewhere.</summary>
        public bool Back()
        {
            switch (Current)
            {
                case ShellScreen.Settings:
                case ShellScreen.HowTo: Current = opener; return true;
                case ShellScreen.Camera:
                case ShellScreen.Controls: Current = ShellScreen.Settings; return true;
                case ShellScreen.ConfirmReset: Current = ShellScreen.Controls; return true;     // Back answers No
                case ShellScreen.Setup: return Go(ShellScreen.Title);
                case ShellScreen.Pause: return Go(ShellScreen.Match);
                case ShellScreen.PracticePause: return Go(ShellScreen.Match);
                case ShellScreen.Confirm: return Go(ShellScreen.Pause);
                default: return false;
            }
        }

        /// <summary>Something outside the menus started a match (a test, a script): drop to the match from wherever we are.</summary>
        public void ForceMatch() { Current = ShellScreen.Match; }
    }
}
