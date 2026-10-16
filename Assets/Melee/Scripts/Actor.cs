using UnityEngine;

public class Actor : MonoBehaviour, IDamageable
{
    private float _currentHealth;
    public int maxHealth;

    void Awake()
    {
        _currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;

        if(_currentHealth <= 0)
        { Die(); }
    }

    public void Stun()
    {
        throw new System.NotImplementedException();
    }

    public void Die()
    {
        Destroy(transform.parent.gameObject);
    }
}
