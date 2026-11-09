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
    /// and the 1st-person crosshair.
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


        [Header("Crosshair (1st person only)")]
        [SerializeField] GameObject crosshair;

        [SerializeField] float lowStaminaFraction = 0.4f;


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

        }

    }
}
