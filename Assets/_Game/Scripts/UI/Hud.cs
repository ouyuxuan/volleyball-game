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
        [SerializeField] PlayerCameraRig rig;
        [SerializeField] GameObject crosshair;

        [Header("Score (top centre)")]
        [SerializeField] RectTransform scoreRoot;       // scaled for the score pop
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text firstToText;
        [SerializeField] Image serveDotA;
        [SerializeField] Image serveDotB;
        [SerializeField] Image[] touchPips;             // three small dots under the score
        [SerializeField] float popScale = 1.35f;
        [SerializeField] float popSeconds = 0.3f;

        [Header("Point banner and match over")]
        [SerializeField] GameObject bannerRoot;
        [SerializeField] TMP_Text bannerText;
        [SerializeField] TMP_Text bannerReason;
        [SerializeField] GameObject overRoot;
        [SerializeField] TMP_Text overTitle;
        [SerializeField] TMP_Text overPrompt;

        static Sprite circle;
        int lastScoreA = -1, lastScoreB = -1;
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
            MakeServePrompt();
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

        void Update()
        {
            if (crosshair != null && rig != null && crosshair.activeSelf != rig.IsFirstPerson)
                crosshair.SetActive(rig.IsFirstPerson);
            if (match == null || match.Rules == null) return;

            RulesEngine r = match.Rules;
            UpdateScore(r);
            UpdatePips(r);
            UpdateBanner(r);
            UpdateServePrompt();
        }

        // ---- serve prompt (built in code, bottom centre): "Your serve: hold [Spike] to toss" / "Jump and hit!" ----

        TMP_Text servePrompt;
        string servePromptShown;

        void MakeServePrompt()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("Serve Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 150f);
            rt.sizeDelta = new Vector2(1000f, 60f);
            servePrompt = go.GetComponent<TextMeshProUGUI>();
            if (scoreText != null) servePrompt.font = scoreText.font;
            servePrompt.fontSize = 34f;
            servePrompt.fontStyle = FontStyles.Bold;
            servePrompt.characterSpacing = 2f;
            servePrompt.outlineWidth = 0.2f;
            servePrompt.outlineColor = (Color32)Palette.Shadow;
            servePrompt.alignment = TextAlignmentOptions.Center;
            servePrompt.color = Palette.CourtLines;
            servePrompt.raycastTarget = false;
            servePrompt.text = "";
        }

        void UpdateServePrompt()
        {
            if (servePrompt == null) return;
            string text = "";
            PlayerController server = match.State == MatchState.Serve ? match.Server : null;
            if (server != null && server.Brain is HumanBrain)
                text = server.Tossed
                    ? "Jump and hit!"
                    : $"Your serve: hold <color=#{Hex(Palette.Accent)}>{ControlPrompts.Label(PromptAction.Spike, InputDevices.Style)}</color> to toss   {Mathf.CeilToInt(match.ServeClockLeft)}";
            if (text != servePromptShown) { servePromptShown = text; servePrompt.text = text; }
        }

        void UpdateScore(RulesEngine r)
        {
            int a = r.Score[0], b = r.Score[1];
            if (a != lastScoreA || b != lastScoreB)
            {
                if (a + b > 0 && a + b > lastScoreA + lastScoreB && lastScoreA >= 0) popLeft = popSeconds;   // a point was scored: pop
                lastScoreA = a; lastScoreB = b;
                scoreText.text = $"<color=#{Hex(Palette.TeamA)}>{a}</color> {dash} <color=#{Hex(Palette.TeamB)}>{b}</color>";
                firstToText.text = $"First to {r.targetScore}";
            }
            popLeft = Mathf.Max(0f, popLeft - Time.unscaledDeltaTime);
            float k = popSeconds > 0f ? popLeft / popSeconds : 0f;
            float s = 1f + (popScale - 1f) * k * k;      // quick pop, eases back
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
                bannerText.text = $"POINT · {TeamName(w)}";
                bannerText.color = TeamColour(w);
                bannerReason.text = MatchManager.ReasonText(r.LastReason, match.LastPointViaNet);
            }

            bool over = match.State == MatchState.MatchOver;
            if (overRoot != null && overRoot.activeSelf != over) overRoot.SetActive(over);
            if (over)
            {
                int w = r.Winner;
                int hi = Mathf.Max(r.Score[0], r.Score[1]), lo = Mathf.Min(r.Score[0], r.Score[1]);
                overTitle.text = $"{TeamName(w)} wins {hi}{dash}{lo}";
                overTitle.color = TeamColour(w);
                overPrompt.text = $"Press <color=#{Hex(Palette.Accent)}>Jump</color> ({ControlPrompts.Label(PromptAction.Jump, InputDevices.Style)}) to play again";
            }
        }
    }
}
