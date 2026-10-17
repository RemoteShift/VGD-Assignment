using UnityEngine;

public class SporeBulb : MonoBehaviour, IDamageable
{
    [Min(0f)] public float oxygenAmount = 15f;
    [Min(0.1f)] public float releaseRadius = 4f;
    public bool HasBurst { get; private set; }

    public void TakeDamage(float damage)
    {
        if (HasBurst || damage <= 0f) return;
        HasBurst = true;
        var player = FindFirstObjectByType<PlayerVitals>();
        if (player && Vector3.Distance(transform.position, player.transform.position) <= releaseRadius)
            player.RestoreOxygen(oxygenAmount);
        gameObject.SetActive(false);
    }

    public void KnockbackFrom(Vector3 origin, float distance) { }
    public void Stun() { }
    public void Die() => TakeDamage(1f);
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, releaseRadius);
    }
}
