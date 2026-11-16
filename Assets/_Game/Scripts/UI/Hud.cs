using Sandspike.Art;
using Sandspike.CameraRig;
using Sandspike.Controls;
using Sandspike.Core;
using Sandspike.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandspike.UI
{
    /// <summary>
    /// The in-game HUD (uGUI + TextMeshPro, built into the scene): score in team colours, serving dot, "First to N",
    /// touch pips, the point banner, the match-over panel and the 1st-person crosshair.
    /// Stamina and charge are not here any more: they are the ring at the player's feet (FeetRing).
    /// All colours come from Palette.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] MatchManager match;

        [Header("Score (top centre)")]
        [SerializeField] RectTransform scoreRoot;       // scaled for the score pop
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text firstToText;
        [SerializeField] Image serveDotA;
        [SerializeField] Image serveDotB;
        [SerializeField] Image[] touchPips;             // three small dots under the score
        [SerializeField] float popSeconds = 0.3f;
        const float PopScale = 1.3f;                    // the score swells 1 -> 1.3 -> 1 over popSeconds

        [Header("Point banner and match over")]
        [SerializeField] GameObject bannerRoot;
        [SerializeField] TMP_Text bannerText;
        [SerializeField] TMP_Text bannerReason;
        [SerializeField] GameObject overRoot;
        [SerializeField] TMP_Text overTitle;
        [SerializeField] TMP_Text overPrompt;

        static Sprite circle;
        int lastScoreA = -1, lastScoreB = -1, lastTarget = -1;
        float popLeft;
        string dash = "-";

        void Awake()
        {
            // Hidden panels are woken for a moment so their TMP texts exist when we style them.
            bool bannerWas = bannerRoot != null && bannerRoot.activeSelf, overWas = overRoot != null && overRoot.activeSelf;
            if (bannerRoot != null) bannerRoot.SetActive(true);
            if (overRoot != null) overRoot.SetActive(true);

            // Give every text a chunky, palette-coloured outline so it reads over sand and sky.
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            {
                t.fontStyle |= FontStyles.Bold;
                t.characterSpacing = 2f;
                t.outlineWidth = 0.2f;
                t.outlineColor = (Color32)Palette.Shadow;
            }
            if (bannerRoot != null) bannerRoot.SetActive(bannerWas);
            if (overRoot != null) overRoot.SetActive(overWas);
            if (scoreText != null && scoreText.font != null && scoreText.font.HasCharacter('–')) dash = "–";

            Sprite dot = Circle();
            foreach (var pip in touchPips) if (pip != null) { pip.sprite = dot; pip.raycastTarget = false; }
            if (serveDotA != null) { serveDotA.sprite = dot; serveDotA.color = Palette.TeamA; }
            if (serveDotB != null) { serveDotB.sprite = dot; serveDotB.color = Palette.TeamB; }
        }

        // A soft-edged white-ish disc, drawn once, for the pips and the serving dot (tinted by Image.color).
        static Sprite Circle()
        {
            if (circle != null) return circle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HudCircle", filterMode = FilterMode.Bilinear };
            Color c = Palette.CourtLines;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                    c.a = Mathf.Clamp01(size / 2f - d);      // 1 pixel of anti-aliased edge
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return circle;
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        static Color TeamColour(int team) => team == 0 ? Palette.TeamA : Palette.TeamB;
        static string TeamName(int team) => team == 0 ? "TEAM A" : "TEAM B";

        /// <summary>
        /// The team that leads and needs just one more point to win the match (first to 'target', ahead by 'winBy'), or -1.
        /// A match is one game, so this is both "set point" and "match point"; the banner says "match point".
        /// </summary>
        public static int MatchPointTeam(int scoreA, int scoreB, int target, int winBy)
        {
            if (scoreA == scoreB) return -1;
            int lead = scoreA > scoreB ? 0 : 1;
            int hi = Mathf.Max(scoreA, scoreB), lo = Mathf.Min(scoreA, scoreB);
            bool wins = hi + 1 >= target && hi + 1 - lo >= winBy;
            return wins ? lead : -1;
        }

        // Who touched the ball last, in words. With one human (player 0) it is You / Teammate / Opponent; with more, Player N or Bot.
        string ToucherName(int playerId)
        {
            if (playerId < 0) return "";
            if (match.HumanCount > 1) return match.IsHumanSlot(playerId) ? $"Player {playerId + 1}" : "Bot";
            return playerId == 0 ? "You" : playerId == 1 ? "Teammate" : "Opponent";
        }

        /// <summary>The font of the HUD's score text, for the other overlay texts built in code (join screen, serve prompts).</summary>
        public TMP_FontAsset Font => scoreText != null ? scoreText.font : null;

        void Update()
        {
            if (match == null || match.Rules == null) return;

            RulesEngine r = match.Rules;
            UpdateScore(r);
            UpdatePips(r);
            UpdateBanner(r);
        }

        void UpdateScore(RulesEngine r)
        {
            int a = r.Score[0], b = r.Score[1];
            if (a != lastScoreA || b != lastScoreB || r.targetScore != lastTarget)   // a new match may have a different target
            {
                if (a + b > 0 && a + b > lastScoreA + lastScoreB && lastScoreA >= 0) popLeft = popSeconds;   // a point was scored: pop
                lastScoreA = a; lastScoreB = b; lastTarget = r.targetScore;
                scoreText.text = $"<color=#{Hex(Palette.TeamA)}>{a}</color> {dash} <color=#{Hex(Palette.TeamB)}>{b}</color>";
                firstToText.text = $"First to {r.targetScore}";
            }
            popLeft = Mathf.Max(0f, popLeft - Time.unscaledDeltaTime);
            float k = popSeconds > 0f ? 1f - popLeft / popSeconds : 1f;     // 0 at the start of the pop, 1 at its end
            float s = popLeft > 0f ? 1f + (PopScale - 1f) * Mathf.Sin(k * Mathf.PI) : 1f;
            scoreRoot.localScale = new Vector3(s, s, 1f);

            if (serveDotA != null) serveDotA.enabled = r.ServingTeam == 0;
            if (serveDotB != null) serveDotB.enabled = r.ServingTeam == 1;
        }

        // 1 to 3 pips for the side that has the ball: filled = touches used.
        void UpdatePips(RulesEngine r)
        {
            bool show = match.State == MatchState.Rally && r.PossessionTeam >= 0;
            for (int i = 0; i < touchPips.Length; i++)
            {
                if (touchPips[i] == null) continue;
                touchPips[i].enabled = show;
                if (!show) continue;
                bool used = i < r.Touches;
                touchPips[i].color = used ? TeamColour(r.PossessionTeam) : Palette.WithAlpha(Palette.Shadow, 0.55f);
            }
        }

        void UpdateBanner(RulesEngine r)
        {
            bool point = match.State == MatchState.PointScored;
            if (bannerRoot != null && bannerRoot.activeSelf != point) bannerRoot.SetActive(point);
            if (point)
            {
                int w = r.LastPointWinner;
                bannerText.text = $"Point: {TeamName(w)}";
                bannerText.color = TeamColour(w);
                string reason = MatchManager.ReasonText(r.LastReason, match.LastPointViaNet);
                string who = ToucherName(r.LastToucherId);
                int mp = r.IsMatchOver ? -1 : MatchPointTeam(r.Score[0], r.Score[1], r.targetScore, r.winBy);
                if (who != "") reason += (reason != "" ? "  ·  " : "") + "last touch: " + who;
                if (mp >= 0) reason += (reason != "" ? "  ·  " : "") + $"{TeamName(mp)} match point";
                bannerReason.text = reason;
            }

            // the match-over panel is gone: the Shell's Results screen shows the winner now
            if (overRoot != null && overRoot.activeSelf) overRoot.SetActive(false);
        }
    }
}
