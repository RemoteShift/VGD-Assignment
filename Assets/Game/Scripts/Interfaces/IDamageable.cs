public interface IDamageable
{
    void TakeDamage(float damage);
    void KnockbackFrom(UnityEngine.Vector3 origin, float distance);
    void Stun();
    void Die();
}
