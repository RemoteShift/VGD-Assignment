using UnityEngine;

// The encounter explicitly arms each placed guard. Crowd figures never enter the target count.
[RequireComponent(typeof(CharacterController))]
public class EncounterGuard : MonoBehaviour, IDamageable
{
    public Transform model;
    public AudioSource sound;
    public AudioClip step, impact;
    public int hitPoints = 3;
    public float speed = 2.5f;
    private StoryScene encounter;
    private CharacterController motor;
    private Transform player;
    private float cooldown, windup, fallSpeed;
    private bool dead;
    private Vector3 post;
    public void Arm(StoryScene owner, Transform target)
    {
        post = transform.position;
        encounter = owner;
        player = target;
        gameObject.SetActive(true);
        motor = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!player || dead || !encounter.PlayingArena) return;
        // If an obstacle or a knockback sends a guard off the island, return it to its post.
        if (transform.position.y < post.y - 7f)
        {
            motor.enabled = false;
            transform.position = post;
            motor.enabled = true;
            fallSpeed = 0;
        }
        var offset = player.position - transform.position;
        offset.y = 0;
        float distance = offset.magnitude;
        if (distance > .1f) transform.rotation = Quaternion.LookRotation(offset);
        Vector3 walk = Vector3.zero;
        cooldown -= Time.deltaTime;
        if (windup > 0)
        {
            windup -= Time.deltaTime;
            model.localRotation = Quaternion.Euler(-24, -28, 0);
            if (windup <= 0)
            {
                model.localRotation = Quaternion.Euler(18, 20, 0);
                if (distance < 2.5f && Mathf.Abs(player.position.y - transform.position.y) < 2.6f)
                    player.GetComponent<PlayerHealth>().TakeDamage(10);
                cooldown = 1.2f;
            }
        }
        else
        {
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.identity, Time.deltaTime * 10);
            if (distance > 1.65f) walk = offset.normalized * speed;
            else if (cooldown <= 0)
            {
                windup = .72f;
                sound.PlayOneShot(step);
            }
        }
        fallSpeed = motor.isGrounded ? -2 : Mathf.Max(-14, fallSpeed - 18 * Time.deltaTime);
        motor.Move((walk + Vector3.up * fallSpeed) * Time.deltaTime);
    }

    public void TakeDamage(float damage)
    {
        if (!encounter || dead || damage <= 0) return;
        sound.PlayOneShot(impact, .55f);
        hitPoints -= Mathf.CeilToInt(damage);
        if (hitPoints <= 0) Die();
        else model.localRotation = Quaternion.Euler(-14, 0, 12);
    }
    public void Die()
    {
        if (!encounter || dead) return;
        dead = true;
        encounter.GuardFell(this);
        gameObject.SetActive(false);
    }
    public void Stun() { cooldown = .6f; windup = 0; }
    public void KnockbackFrom(Vector3 origin, float distance)
    {
        if (motor && !dead) motor.Move(Vector3.ProjectOnPlane(transform.position - origin, Vector3.up).normalized * distance);
    }
}
