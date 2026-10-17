using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PlayerVitalsHUD : MonoBehaviour
{
    public PlayerVitals vitals;
    public PlayerHealth health;
    public PlayerInteraction interaction;
    public Image healthFill;
    public TMP_Text healthText;
    public TMP_Text interactionText;
    public Color healthColor = new Color(0.35f, 1f, 0.5f);
    public Image oxygenFill;
    [FormerlySerializedAs("enduranceFill")]
    public Image exertionFill;
    public TMP_Text oxygenText;
    [FormerlySerializedAs("enduranceText")]
    public TMP_Text exertionText;
    public TMP_Text statusText;
    [Range(0f, 1f)] public float lowOxygenThreshold = 0.25f;
    [FormerlySerializedAs("highEnduranceThreshold")]
    [Range(0f, 1f)] public float highExertionThreshold = 0.75f;
    public Color oxygenColor = new Color(0.2f, 0.85f, 1f);
    [FormerlySerializedAs("enduranceColor")]
    public Color exertionColor = new Color(1f, 0.7f, 0.2f);
    public Color warningColor = new Color(1f, 0.25f, 0.25f);

    private void LateUpdate()
    {
        if (!vitals) return;
        if (health && healthFill && healthText)
        {
            SetBar(healthFill, health.HealthFraction, health.HealthFraction <= 0.25f ? warningColor : healthColor);
            healthText.SetText("HEALTH   {0:0} / {1:0}", health.Health, health.maxHealth);
        }
        if (interactionText) interactionText.text = interaction ? interaction.Prompt : "";
        bool lowOxygen = vitals.OxygenFraction <= lowOxygenThreshold;
        bool highExertion = vitals.ExertionFraction >= highExertionThreshold;
        SetBar(oxygenFill, vitals.OxygenFraction, lowOxygen ? warningColor : oxygenColor);
        SetBar(exertionFill, vitals.ExertionFraction, highExertion ? warningColor : exertionColor);
        oxygenText.SetText("OXYGEN   {0:0} / {1:0}", vitals.Oxygen, vitals.maxOxygen);
        exertionText.SetText("EXERTION   {0:0} / {1:0}", vitals.Exertion, vitals.maxExertion);
        statusText.text = health && health.IsDead ? "DEFEATED" : vitals.IsDepleted ? "OXYGEN EMPTY" : lowOxygen ? "LOW OXYGEN" : highExertion ? "HIGH EXERTION" : "";
    }

    private static void SetBar(Image bar, float fraction, Color color)
    {
        bar.rectTransform.anchorMax = new Vector2(fraction, 1f);
        bar.color = color;
    }
}
