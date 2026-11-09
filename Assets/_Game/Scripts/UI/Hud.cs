using Sandspike.Art;
using Sandspike.CameraRig;
using Sandspike.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandspike.UI
{
    /// <summary>
    /// The in-game HUD (uGUI + TextMeshPro, built into the scene): stamina bar, jump and spike charge meters,
    /// the PERFECT / GOOD / OK / MISS pop after each hit, and the 1st-person crosshair.
    /// All colours come from Palette. M2 adds the score.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerController player;
        [SerializeField] PlayerCameraRig rig;

        [Header("Meters (Image type = Filled)")]
        [SerializeField] Image staminaFill;
        [SerializeField] Image jumpFill;
        [SerializeField] Image spikeFill;
        [SerializeField] Image[] capMarkers;         // thin lines on the charge meters showing the current max charge

        [Header("Hit pop")]
        [SerializeField] TMP_Text popText;
        [SerializeField] float popSeconds = 0.9f;
        [SerializeField] float popStartScale = 1.5f;

        [Header("Crosshair (1st person only)")]
        [SerializeField] GameObject crosshair;

        [SerializeField] float lowStaminaFraction = 0.4f;

        float popTimer;

        void Awake()
        {
            // Give every text a chunky, palette-coloured outline so it reads over sand and sky.
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            {
                t.fontStyle |= FontStyles.Bold;
                t.characterSpacing = 2f;
                t.outlineWidth = 0.2f;
                t.outlineColor = (Color32)Palette.Shadow;
            }
        }

        void Start()
        {
            if (player != null) player.OnHitAttempt += OnHit;
            if (popText != null) popText.alpha = 0f;
        }

        void OnDestroy()
        {
            if (player != null) player.OnHitAttempt -= OnHit;
        }

        void Update()
        {
            if (player == null) return;

            float s = player.Stamina.Normalized;
            staminaFill.fillAmount = s;
            staminaFill.color = s < lowStaminaFraction ? Palette.TeamA : Palette.Accent;

            // Charge meters show the charge itself; the cap marker shows how far stamina currently lets you go.
            jumpFill.fillAmount = player.JumpCharge.Value;
            spikeFill.fillAmount = player.SpikeCharge.Value;
            if (capMarkers != null)
                foreach (var marker in capMarkers)
                {
                    if (marker == null) continue;
                    var rt = marker.rectTransform;
                    rt.anchorMin = new Vector2(player.Stamina.MaxChargeAllowed, 0f);
                    rt.anchorMax = new Vector2(player.Stamina.MaxChargeAllowed, 1f);
                }

            if (crosshair != null && rig != null && crosshair.activeSelf != rig.IsFirstPerson)
                crosshair.SetActive(rig.IsFirstPerson);

            UpdatePop();
        }

        void OnHit(HitReport report)
        {
            if (popText == null) return;
            switch (report.grade)
            {
                case HitGrade.Perfect: popText.text = "PERFECT"; popText.color = Palette.Accent; break;
                case HitGrade.Good:    popText.text = "GOOD";    popText.color = Palette.CourtLines; break;
                case HitGrade.Miss:    popText.text = "MISS";    popText.color = Palette.TeamA; break;
                default:               popText.text = "OK";      popText.color = Palette.Sky; break;
            }
            popTimer = popSeconds;
        }

        void UpdatePop()
        {
            if (popText == null) return;
            if (popTimer <= 0f) { popText.alpha = 0f; return; }
            popTimer -= Time.deltaTime;
            float k = Mathf.Clamp01(popTimer / popSeconds);             // 1 -> 0
            float pop = Mathf.Lerp(1f, popStartScale, Mathf.Pow(k, 4f)); // quick pop at the start, then settle
            popText.rectTransform.localScale = Vector3.one * pop;
            popText.alpha = Mathf.Clamp01(k * 3f);                       // hold, then fade at the end
        }
    }
}
