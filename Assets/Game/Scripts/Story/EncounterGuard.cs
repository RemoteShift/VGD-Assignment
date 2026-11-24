using System.Collections;
using UnityEngine;

// Only the current watch is armed. Decorative knights, defeated guards and future watches
// never inflict damage or report a defeat to the story.
[RequireComponent(typeof(CharacterController))]
public class EncounterGuard : MonoBehaviour, IDamageable
{
    public Animator animator;
    public AudioSource sound;
    public AudioClip step, impact;
    public int hitPoints = 3;
    public bool captain;
    public GameObject captainPlume;
    public float speed = 2.8f;
    [Range(-1, 1)] public float approachSide;
    private StoryScene encounter;
    private CharacterController motor;
    private PlayerHealth target;
    private Vector3 post;
    private float cooldown, windup, fallSpeed, recoil, stride;
    private string pose;
    private bool dead;

    public void Arm(StoryScene owner, Transform player)
    {
        encounter = owner;
        target = player.GetComponent<PlayerHealth>();
        post = transform.position;
        captainPlume.SetActive(captain);
        gameObject.SetActive(true);
        motor = GetComponent<CharacterController>();
        Pose("Ready");
    }

    private void Pose(string state)
    {
        if (pose == state) return;
        pose = state;
        animator.CrossFadeInFixedTime(state, .10f, 0, 0f);
    }

    private void Update()
    {
        if (dead || !target || !encounter.PlayingArena) return;
        if (transform.position.y < post.y - 7f)
        {
            motor.enabled = false;
            transform.position = post;
            motor.enabled = true;
            fallSpeed = 0;
        }
        var offset = target.transform.position - transform.position;
        var flat = Vector3.ProjectOnPlane(offset, Vector3.up);
        float distance = flat.magnitude;
        Vector3 walk = Vector3.zero;
        cooldown -= Time.deltaTime;
        recoil -= Time.deltaTime;
        if (windup > 0)
        {
            // The blade commits to its heading at windup, giving the player room to sidestep.
            windup -= Time.deltaTime;
            if (windup <= 0 && distance < 2.65f && Mathf.Abs(offset.y) < 2.6f &&
                Vector3.Dot(transform.forward, flat.normalized) > .26f)
                target.TakeDamage(captain ? 16 : 10);
        }
        else if (recoil <= 0)
        {
            if (distance > .1f) transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(flat), 380 * Time.deltaTime);
            if (distance > 1.8f)
            {
                var direction = flat.normalized;
                var flank = Vector3.Cross(Vector3.up, direction) * approachSide * Mathf.Clamp01((distance - 2f) / 4);
                walk = (direction + flank).normalized * speed;
                Pose("Advance");
                stride -= Time.deltaTime;
                if (stride <= 0) { sound.PlayOneShot(step, .28f); stride = .4f; }
            }
            else if (cooldown <= 0)
            {
                Pose("Cut");
                windup = captain ? .88f : .60f;
                cooldown = captain ? 2.3f : 1.82f;
                sound.PlayOneShot(step, .8f);
            }
            else if (cooldown < .8f) Pose("Ready");
        }
        fallSpeed = motor.isGrounded ? -2 : Mathf.Max(-14, fallSpeed - 18 * Time.deltaTime);
        motor.Move((walk + Vector3.up * fallSpeed) * Time.deltaTime);
    }

    public void TakeDamage(float damage)
    {
        if (!encounter || dead || damage <= 0) return;
        sound.PlayOneShot(impact, .66f);
        encounter.RegisterHit();
        hitPoints -= Mathf.CeilToInt(damage);
        if (hitPoints <= 0) Die();
        else Stun();
    }

    public void Die()
    {
        if (!encounter || dead) return;
        dead = true;
        motor.enabled = false;
        Pose("Fall");
        encounter.GuardFell(this);
        StartCoroutine(RemoveBody());
    }

    private IEnumerator RemoveBody()
    {
        yield return new WaitForSeconds(2);
        gameObject.SetActive(false);
    }

    public void Stun()
    {
        windup = 0;
        recoil = .28f;
        cooldown = Mathf.Max(cooldown, .65f);
        Pose("Recoil");
    }

    public void KnockbackFrom(Vector3 origin, float distance)
    {
        if (motor && !dead) motor.Move(Vector3.ProjectOnPlane(transform.position - origin, Vector3.up).normalized * distance);
    }
}
