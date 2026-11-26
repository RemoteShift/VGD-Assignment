using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Guards navigate the baked arena floor. The visible knight and the sword target both
// travel on the same root, including around scenery and while recovering from a slam.
[RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider))]
public class EncounterGuard : MonoBehaviour, IDamageable
{
    public Animator animator;
    public AudioSource sound;
    public AudioClip step, impact;
    public int hitPoints = 8;
    public bool captain;
    public GameObject captainPlume;
    public float speed = 4.6f;
    private StoryScene encounter;
    private NavMeshAgent agent;
    private PlayerHealth target;
    private Collider targetBody;
    private int scenery;
    private float cooldown, windup, recoil, stride, nextPath;
    private Vector3 goal;
    private string pose;
    private bool dead;

    public void Arm(StoryScene owner, Transform player)
    {
        encounter = owner;
        target = player.GetComponent<PlayerHealth>();
        targetBody = player.GetComponent<LocomotionController>().PlayerCollider;
        scenery = LayerMask.GetMask("Default", "Ignore Light");
        captainPlume.SetActive(captain);
        if (captain) hitPoints += 3;
        gameObject.SetActive(true);
        agent = GetComponent<NavMeshAgent>();
        agent.speed = speed;
        agent.updateRotation = false;
        // Moving with the agent and applying animation displacement would move the model twice.
        animator.applyRootMotion = false;
        Pose("Ready");
    }

    private void Pose(string state, bool restart = false)
    {
        if (pose == state && !restart) return;
        pose = state;
        animator.speed = state == "Cut" ? (captain ? 1.3f : 1.7f) : 1f;
        animator.CrossFadeInFixedTime(state, .08f, 0, 0);
    }

    private void Update()
    {
        if (dead || !target || !agent.isOnNavMesh) return;
        if (!encounter.PlayingArena)
        {
            agent.isStopped = true;
            return;
        }

        Vector3 offset = target.transform.position - transform.position;
        Vector3 flat = Vector3.ProjectOnPlane(offset, Vector3.up);
        bool inReach = flat.sqrMagnitude <= 2.1f * 2.1f && Mathf.Abs(offset.y) < 2.3f &&
            !Physics.Linecast(transform.position + Vector3.up * 1.2f, targetBody.bounds.center,
                scenery, QueryTriggerInteraction.Ignore);
        cooldown -= Time.deltaTime;
        if (windup > 0)
        {
            // Heading locks on commitment. Sidestepping or getting outside the arc beats the cut.
            windup -= Time.deltaTime;
            if (windup <= 0 && inReach && Vector3.Dot(transform.forward, flat.normalized) > .48f)
                target.TakeDamage(captain ? 32 : 24);
            return;
        }
        recoil -= Time.deltaTime;
        if (recoil > 0) return;

        if (inReach)
        {
            agent.isStopped = true;
            Face(flat);
            if (cooldown <= 0 && Vector3.Dot(transform.forward, flat.normalized) > .75f)
            {
                Pose("Cut", true);
                windup = captain ? .46f : .33f;
                cooldown = captain ? 1.32f : 1.12f;
                sound.PlayOneShot(step, .8f);
            }
            else if (cooldown < .50f) Pose("Ready");
            return;
        }

        agent.isStopped = false;
        // Reuse the route until the target moves. Sample the floor so jump and dash
        // inputs do not send the search to an unreachable point in the air.
        if (Time.time >= nextPath)
        {
            nextPath = Time.time + .14f;
            var bounds = targetBody.bounds;
            Vector3 foot = new(bounds.center.x, bounds.min.y, bounds.center.z);
            if (NavMesh.SamplePosition(foot, out var hit, 4f, agent.areaMask) &&
                (!agent.hasPath || (goal - hit.position).sqrMagnitude > .09f))
            {
                goal = hit.position;
                agent.SetDestination(goal);
            }
        }
        if (agent.velocity.sqrMagnitude > .06f)
        {
            Face(agent.velocity);
            Pose("Advance");
            stride -= Time.deltaTime;
            if (stride <= 0) { sound.PlayOneShot(step, .28f); stride = .32f; }
        }
        else Pose("Ready");
    }

    private void Face(Vector3 direction)
    {
        direction.y = 0;
        if (direction.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 720 * Time.deltaTime);
    }

    public void TakeDamage(float damage)
    {
        if (!encounter || dead || damage <= 0) return;
        sound.PlayOneShot(impact, .66f);
        encounter.RegisterHit();
        hitPoints -= Mathf.CeilToInt(damage);
        if (hitPoints <= 0) Die();
        // Light swings hurt without resetting the attack clock. A heavy blow buys space.
        else if (damage >= (captain ? 5f : 3f)) Stun();
    }

    public void Die()
    {
        if (!encounter || dead) return;
        dead = true;
        agent.enabled = false;
        GetComponent<CapsuleCollider>().enabled = false;
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
        if (!agent || !agent.isOnNavMesh || dead) return;
        agent.isStopped = true;
        windup = 0;
        recoil = .22f;
        cooldown = Mathf.Max(cooldown, .48f);
        Pose("Recoil", true);
    }

    public void KnockbackFrom(Vector3 origin, float distance)
    {
        if (!agent || !agent.isOnNavMesh || dead) return;
        var delta = Vector3.ProjectOnPlane(transform.position - origin, Vector3.up).normalized * distance;
        agent.Move(delta);
    }
}
