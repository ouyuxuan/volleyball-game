using UnityEngine;

namespace Sandspike.Players
{
    public enum AIDifficulty { Easy, Normal, Hard }

    /// <summary>How a bot goes for balls it can't reach on foot.</summary>
    public enum AIDiveMode { Never, IfStaminaAllows, Always }

    /// <summary>
    /// Every number that makes a difficulty level, in one place. Edit the Table below to tune the AI.
    /// </summary>
    public class AIProfile
    {
        public float reactionDelay;        // seconds the bot keeps its old plan after the ball's arc changes
        public float moveSpeedMultiplier;  // scales how fast it runs
        public float mistakeChance;        // per hit: send it mistakeMetres off target
        public float mistakeMetres = 1.5f;
        public float aimSpread;            // attacks land within this radius of the chosen spot (m)
        public float spikeChargeMin, spikeChargeMax;
        public bool usesSprint;
        public AIDiveMode diveMode;
        public float blockChance;          // chance it blocks when it could (0 never, 1 always)
        public float tipChance;            // chance to dink a spike chance short instead of hitting it
        public AIDifficulty attackStyle;   // how it picks where to attack (see BotPlanner.PickAttack)

        /// <summary>The pinned table (M3). Easy / Normal / Hard.</summary>
        public static readonly AIProfile[] Table =
        {
            new AIProfile { reactionDelay = 0.45f, moveSpeedMultiplier = 0.85f, mistakeChance = 0.25f, aimSpread = 1.2f, spikeChargeMin = 0.2f, spikeChargeMax = 0.6f,
                            usesSprint = false, diveMode = AIDiveMode.Never, blockChance = 0f, tipChance = 0f, attackStyle = AIDifficulty.Easy },
            new AIProfile { reactionDelay = 0.23f, moveSpeedMultiplier = 1f, mistakeChance = 0.12f, aimSpread = 0.7f, spikeChargeMin = 0.4f, spikeChargeMax = 0.8f,
                            usesSprint = true, diveMode = AIDiveMode.IfStaminaAllows, blockChance = 0.4f, tipChance = 0f, attackStyle = AIDifficulty.Normal },
            new AIProfile { reactionDelay = 0.19f, moveSpeedMultiplier = 1f, mistakeChance = 0.05f, aimSpread = 0.3f, spikeChargeMin = 0.6f, spikeChargeMax = 1.0f,
                            usesSprint = true, diveMode = AIDiveMode.Always, blockChance = 1f, tipChance = 0.15f, attackStyle = AIDifficulty.Hard },
        };

        public static AIProfile For(AIDifficulty d) => Table[(int)d];
    }
}
