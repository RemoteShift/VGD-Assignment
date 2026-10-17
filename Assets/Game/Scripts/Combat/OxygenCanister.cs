using UnityEngine;

public class OxygenCanister : MonoBehaviour
{
    [Min(0f)] public float oxygenAmount = 40f;
    [Min(0f)] public float useDuration = 1f;
    public bool IsConsumed { get; private set; }
    private Vector3 originalScale;
    private void Awake() => originalScale = transform.localScale;

    public void ShowUseProgress(float progress)
    {
        transform.localScale = originalScale * (1f + Mathf.Sin(progress * Mathf.PI * 4f) * 0.04f);
    }

    public bool Collect(PlayerVitals player)
    {
        if (IsConsumed || !player || player.IsDepleted) return false;
        IsConsumed = true;
        player.RestoreOxygen(oxygenAmount);
        gameObject.SetActive(false);
        return true;
    }
}
