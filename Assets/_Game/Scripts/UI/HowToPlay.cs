using Sandspike.Art;
using Sandspike.Controls;
using UnityEngine;

namespace Sandspike.UI
{
    /// <summary>The text of the How to Play screen: three short pages, with button names for the device that opened it.</summary>
    public static class HowToPlay
    {
        public static readonly string[] Titles = { "HOW TO PLAY: HITTING", "HOW TO PLAY: MOVING", "HOW TO PLAY: SCORING" };

        public static string[] Pages(PromptStyle style)
        {
            string key = ColorUtility.ToHtmlStringRGB(Palette.Accent);
            string K(PromptAction a) => $"<color=#{key}>{ControlPrompts.Label(a, style)}</color>";
            return new[]
            {
                $"{K(PromptAction.Bump)} bump: your first touch. Tap it to pass the ball up. Hold it to hit harder.\n\n" +
                $"{K(PromptAction.Set)} set: tap it to lift the ball up for a teammate. Hold it to hit harder.\n\n" +
                $"{K(PromptAction.Spike)} spike: hold it to wind up, then release to hit. Jump with {K(PromptAction.Jump)} first and hit in the air for the strongest shot. " +
                $"Hold {K(PromptAction.Jump)} to jump higher. The ball goes where you aim with {K(PromptAction.Aim)}.\n\n" +
                "Each team gets three touches. A bump, then a set, then a spike is the best play: a spike as your second touch is 10% faster, and as your third touch 20% faster.",

                $"Move with {K(PromptAction.Move)}. Hold {K(PromptAction.Sprint)} to sprint. Sprinting uses stamina.\n\n" +
                $"Dive with {K(PromptAction.Dive)} to reach a ball you cannot get to on foot. If you dive with too little stamina you burn out and move slowly for two seconds.\n\n" +
                $"Block: jump at the net and hold {K(PromptAction.Bump)} while the other team has the ball. The ball bounces back toward where you aim. A block counts as one of your three touches.\n\n" +
                $"Serve: hold {K(PromptAction.Spike)} to toss the ball up, then jump with {K(PromptAction.Jump)} and hit it. Toss within 15 seconds, and do not let the ball hit the sand.",

                "Every rally scores a point, whoever served. The team that wins a rally from the other side serves next.\n\n" +
                "A ball that lands inside the lines scores for the team on the other side of the net. A ball that lands outside scores for the team that did not hit it last.\n\n" +
                "A fourth touch, hitting the ball twice in a row yourself, or a serve that fails also give the point to the other team.\n\n" +
                "The first team to the target score (11, 21 or 25) wins, if it is 2 points ahead. With the point cap on, the first team to the cap (15, 25 or 30) wins even without a 2 point lead.",
            };
        }
    }
}
