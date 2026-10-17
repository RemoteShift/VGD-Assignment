using UnityEngine;

public class HealthCell : MonoBehaviour
{
    [Min(0f)] public float healthAmount = 25f;
    [Tooltip("Seconds before disappearing. Set to zero for a permanent placed pickup.")]
    [Min(0f)] public float lifetime = 15f;
    public bool IsConsumed { get; private set; }
    private float age;

    private void Update()
    {
        if (lifetime <= 0f) return;
        age += Time.deltaTime;
        if (age >= lifetime) gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<PlayerHealth>();
        if (player) Collect(player);
    }

    public bool Collect(PlayerHealth player)
    {
        if (IsConsumed || !isActiveAndEnabled || !player || player.IsDead) return false;
        IsConsumed = true;
        player.RestoreHealth(healthAmount);
        gameObject.SetActive(false);
        return true;
    }
}
