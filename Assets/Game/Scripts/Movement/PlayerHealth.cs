using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerHealth : Singleton<PlayerHealth>
{
    [Min(1f)] public float maxHealth = 100f;
    [Min(0f)] public float startingHealth = 100f;
    public bool restartArenaOnDeath = true;
    public UnityEvent onDeath = new UnityEvent();
    [SerializeField] private float health;
    public float Health => health;
    public float HealthFraction => Mathf.Clamp01(health / Mathf.Max(1f, maxHealth));
    public bool IsDead => health <= 0f;

    private void Awake()
    {
        base.Awake();
        ResetHealth();
    }

    // Called by future enemy attacks. Oxygen and exertion never call this.
    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f) return;
        health = Mathf.Clamp(health - damage, 0f, maxHealth);
        if (!IsDead) return;
        onDeath.Invoke();
        if (restartArenaOnDeath && TryGetComponent<PlayerVitals>(out var vitals))
            vitals.RestartAfterDefeat();
    }

    public void RestoreHealth(float amount)
    {
        if (!IsDead) health = Mathf.Clamp(health + Mathf.Max(0f, amount), 0f, maxHealth);
    }

    public void ResetHealth() => health = Mathf.Clamp(startingHealth, 0f, maxHealth);
}
