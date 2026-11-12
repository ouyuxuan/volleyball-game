namespace Sandspike.Controls
{
    /// <summary>The game's actions, for looking up button names.</summary>
    public enum PromptAction { Move, Aim, Jump, Spike, Bump, Set, ToggleView, Pause, FeedBall, Dive, ToggleHints, Sprint, Block }

    /// <summary>Button names for the on-screen hints, per device style (keys / Xbox / PlayStation).</summary>
    public static class ControlPrompts
    {
        public static string Label(PromptAction action, PromptStyle style)
        {
            if (action == PromptAction.Block) action = PromptAction.Jump;   // a block is a jump at the net
            switch (style)
            {
                case PromptStyle.PlayStation:
                    switch (action)
                    {
                        case PromptAction.Move: return "Left Stick";
                        case PromptAction.Aim: return "Right Stick";
                        case PromptAction.Jump: return "Cross";
                        case PromptAction.Spike: return "R2";
                        case PromptAction.Bump: return "Square";
                        case PromptAction.Set: return "Triangle";
                        case PromptAction.ToggleView: return "R3";
                        case PromptAction.Pause: return "Options";
                        case PromptAction.Dive: return "Circle";
                        case PromptAction.Sprint: return "L3";
                        case PromptAction.ToggleHints: return "Create";
                        default: return "Create";
                    }
                case PromptStyle.Xbox:
                    switch (action)
                    {
                        case PromptAction.Move: return "Left Stick";
                        case PromptAction.Aim: return "Right Stick";
                        case PromptAction.Jump: return "A";
                        case PromptAction.Spike: return "RT";
                        case PromptAction.Bump: return "X";
                        case PromptAction.Set: return "Y";
                        case PromptAction.ToggleView: return "R3";
                        case PromptAction.Pause: return "Menu";
                        case PromptAction.Dive: return "B";
                        case PromptAction.Sprint: return "L3";
                        case PromptAction.ToggleHints: return "View";
                        default: return "View";
                    }
                default:
                    switch (action)
                    {
                        case PromptAction.Move: return "WASD";
                        case PromptAction.Aim: return "Mouse";
                        case PromptAction.Jump: return "Space";
                        case PromptAction.Spike: return "Left Click";
                        case PromptAction.Bump: return "Right Click";
                        case PromptAction.Set: return "E";
                        case PromptAction.ToggleView: return "V";
                        case PromptAction.Pause: return "Esc";
                        case PromptAction.Dive: return "Shift";
                        case PromptAction.Sprint: return "Left Ctrl";
                        case PromptAction.ToggleHints: return "H";
                        default: return "R";
                    }
            }
        }

        public static string ActionName(PromptAction action)
        {
            switch (action)
            {
                case PromptAction.Move: return "Move";
                case PromptAction.Aim: return "Aim";
                case PromptAction.Jump: return "Jump (hold = higher)";
                case PromptAction.Spike: return "Spike (hold = harder)";
                case PromptAction.Bump: return "Bump (hold = harder)";
                case PromptAction.Set: return "Set (hold = harder)";
                case PromptAction.ToggleView: return "1st / 3rd person";
                case PromptAction.Pause: return "Pause";
                case PromptAction.Dive: return "Dive (low dig)";
                case PromptAction.Sprint: return "Sprint (hold)";
                case PromptAction.Block: return "at the net: Block";
                case PromptAction.ToggleHints: return "Show / hide controls";
                default: return "Feed ball (training)";
            }
        }
    }
}
