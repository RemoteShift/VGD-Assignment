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

    public void KnockbackFrom(Vector3 origin, float distance)
    {
        // Move the animated model's root, not just its skinned mesh.
        var model = GetComponentInParent<Animator>();
        var body = model ? model.transform : transform;
        var direction = body.position - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = body.forward;
        direction.Normalize();

        var collider = GetComponent<Collider>();
        var mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
        var playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) mask &= ~(1 << playerLayer);
        if (collider && Physics.BoxCast(collider.bounds.center, collider.bounds.extents * 0.9f,
            direction, out var hit, Quaternion.identity, distance, mask, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0f, hit.distance - 0.05f);

        body.position += direction * Mathf.Max(0f, distance);
    }

    public void Die()
    {
        var model = GetComponentInParent<Animator>();
        Destroy(model ? model.gameObject : gameObject);
    }
}
