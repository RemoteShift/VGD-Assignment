using UnityEngine;
using UnityEngine.Rendering;

public class GardenDayNightCycle : MonoBehaviour
{
    public Light sun;
    public Light fillLight;
    [Min(10f)] public float fullCycleSeconds = 180f;
    [Tooltip("Fraction of the full cycle spent in daytime. 0.75 gives 135 seconds of day and 45 seconds of night in a 180-second cycle.")]
    [Range(0.51f, 0.95f)] public float daytimeFraction = 0.75f;
    [Range(0f, 24f)] public float startingHour = 12f;
    public bool advanceTime = true;
    [Header("Lighting")]
    [Min(0f)] public float daySunIntensity = 1.24f;
    [Tooltip("A small nonzero intensity keeps the procedural sky's sun reference stable at night.")]
    [Min(0.01f)] public float nightSunIntensity = 0.01f;
    [Min(0f)] public float dayFillIntensity = 0.65f;
    [Min(0f)] public float nightFillIntensity = 0.02f;
    public Color dayAmbient = new Color(0.65f, 0.7f, 0.8f);
    public Color nightAmbient = new Color(0.025f, 0.035f, 0.065f);
    public Color daySunColor = new Color(1f, 0.96f, 0.86f);
    public Color sunsetColor = new Color(1f, 0.4f, 0.18f);
    [Min(0f)] public float daySkyExposure = 1f;
    [Min(0f)] public float nightSkyExposure = 0.04f;
    [Header("Runtime")]
    [SerializeField, Range(0f, 24f)] private float hour;
    public float Hour => hour;
    public bool IsNight => hour < 6f || hour >= 18f;

    private Material originalSky;
    private Material runtimeSky;
    private AmbientMode originalAmbientMode;
    private Color originalAmbient;
    private float originalReflection;
    private float sunHeading;

    private void Awake()
    {
        originalSky = RenderSettings.skybox;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbient = RenderSettings.ambientLight;
        originalReflection = RenderSettings.reflectionIntensity;
        if (originalSky)
        {
            runtimeSky = new Material(originalSky);
            RenderSettings.skybox = runtimeSky;
        }
        if (sun) sunHeading = sun.transform.eulerAngles.y;
        SetHour(startingHour);
    }

    private void Update()
    {
        if (advanceTime) AdvanceTime(Time.deltaTime);
        ApplyLighting();
    }

    public void AdvanceTime(float seconds)
    {
        float day = Mathf.Clamp(daytimeFraction, 0.51f, 0.95f);
        float phase = IsNight
            ? day + Mathf.Repeat(hour - 18f, 24f) / 12f * (1f - day)
            : (hour - 6f) / 12f * day;
        phase = Mathf.Repeat(phase + Mathf.Max(0f, seconds) / Mathf.Max(10f, fullCycleSeconds), 1f);
        hour = phase < day
            ? 6f + phase / day * 12f
            : Mathf.Repeat(18f + (phase - day) / (1f - day) * 12f, 24f);
    }

    public void SetHour(float value)
    {
        hour = Mathf.Repeat(value, 24f);
        ApplyLighting();
    }

    private void ApplyLighting()
    {
        float elevation = Mathf.Cos((hour - 12f) * Mathf.PI / 12f);
        float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.25f, elevation));
        if (sun)
        {
            sun.transform.rotation = Quaternion.Euler(hour * 15f - 90f, sunHeading, 0f);
            sun.intensity = Mathf.Lerp(Mathf.Max(0.01f, nightSunIntensity), daySunIntensity, daylight);
            sun.color = Color.Lerp(sunsetColor, daySunColor, Mathf.Clamp01(elevation * 2f));
        }
        if (fillLight) fillLight.intensity = Mathf.Lerp(nightFillIntensity, dayFillIntensity, daylight);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, daylight);
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.08f, originalReflection, daylight);
        if (runtimeSky && runtimeSky.HasProperty("_Exposure"))
            runtimeSky.SetFloat("_Exposure", Mathf.Lerp(nightSkyExposure, daySkyExposure, daylight));
    }

    private void OnDestroy()
    {
        if (RenderSettings.skybox == runtimeSky) RenderSettings.skybox = originalSky;
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbient;
        RenderSettings.reflectionIntensity = originalReflection;
        if (runtimeSky) Destroy(runtimeSky);
    }
}
