using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatController : Singleton<CombatController>
{
    /// <summary>
    /// Tuning for one kind of melee attack. The attack scans a fan of rays from the camera,
    /// sweeping from the left edge of the arc to the right edge over <see cref="sweepDuration"/>.
    /// </summary>
    [System.Serializable]
    public class AttackSettings
    {
        [Min(0f)] public float damage = 1f;

        [Tooltip("How far each scan ray reaches (world units).")]
        [Min(0.1f)] public float range = 3f;

        [Tooltip("Total width of the horizontal sweep in degrees, centred on where the camera is looking.")]
        [Range(1f, 360f)] public float arc = 120f;

        [Tooltip("Seconds between the input and the first scan ray.")]
        [Min(0f)] public float windup = 0.08f;

        [Tooltip("Seconds the scan takes to travel from the left edge of the arc to the right edge.")]
        [Min(0f)] public float sweepDuration = 0.14f;

        [Tooltip("Seconds after the sweep before another attack can start.")]
        [Min(0f)] public float recovery = 0.1f;

        [Tooltip("Animator playback speed while this attack plays (1 = original clip speed).")]
        [Min(0.1f)] public float animSpeed = 2.6f;
    }

    private const string IDLE = "Idle";
    private const string WALK = "Walk";
    private const string ATTACK1 = "Attack 1";
    private const string ATTACK2 = "Attack 2";
    private const string HEAVY_ATTACK = "Heavy Attack";

    [Header("References")]
    [SerializeField] private Animator anim;

    [Header("Targeting")]
    public LayerMask attackLayer;

    [Tooltip("Degrees between neighbouring scan rays. Lower = denser scan, more rays per swing.")]
    [Range(0.5f, 15f)] public float scanAngleStep = 3f;

    [Tooltip("Draw every scan ray in the Scene view while swinging (editor only).")]
    public bool debugDraw = true;

    [Header("Light Attack (Left Click)")]
    public AttackSettings lightAttack = new AttackSettings();

    [Header("Heavy Attack (Hold Right Click)")]
    [Tooltip("How long right click has to be held before the heavy attack fires.")]
    [Min(0f)] public float heavyHoldTime = 0.2f;
    public AttackSettings heavyAttack = new AttackSettings
    {
        damage = 3f,
        range = 4f,
        arc = 150f,
        windup = 0.12f,
        sweepDuration = 0.2f,
        recovery = 0.2f,
        animSpeed = 1.6f
    };

    [Header("Dash Strike")]
    [Tooltip("Damage dealt to each enemy the player dashes through (once per dash).")]
    [Min(0f)] public float dashDamage = 2f;

    [Tooltip("Radius around the player that damages enemies while dashing.")]
    [Min(0.1f)] public float dashHitRadius = 1.25f;

    [Header("Ground Slam")]
    [Min(0.1f)] public float slamRadius = 5f;
    [Min(0f)] public float slamDamage = 5f;
    [Tooltip("How far enemies are pushed away from the impact, in world units.")]
    [Min(0f)] public float slamKnockbackDistance = 3f;
    [Tooltip("Minimum vertical drop after entering Ground Slam before the landing deals damage and knockback.")]
    [Min(0f)] public float minimumSlamDistance = 2f;

    [Header("Animation")]
    [Tooltip("Blend time into an attack animation. Keep this short so attacks feel snappy.")]
    [Min(0f)] public float attackCrossFade = 0.05f;

    [Tooltip("Blend time between idle / walk.")]
    [Min(0f)] public float locomotionCrossFade = 0.2f;

    public bool IsAttacking => _attacking;

    private string _currentAnimStateName = IDLE;

    private bool _attacking;
    private Coroutine _attackRoutine;
    private int _lightComboIndex;

    private bool _heavyHeld;
    private bool _heavyFired;
    private float _heavyHeldTime;

    private InputHandler _input;
    private Camera _cam;

    private readonly RaycastHit[] _rayHits = new RaycastHit[16];
    private readonly Collider[] _overlapHits = new Collider[32];
    private readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();

    private Vector3 PlayerVelocity => LocomotionController.Instance.rb.linearVelocity;

    private Camera Cam
    {
        get
        {
            if (_cam == null) _cam = CameraLook.Instance.GetComponentInChildren<Camera>();
            return _cam;
        }
    }

    #region Unity Messages

    private void OnEnable()
    {
        _input = InputHandler.Instance;
        _input.OnAttackInput += LightAttackPressed;
        _input.OnHeavyAttackStarted += HeavyAttackStarted;
        _input.OnHeavyAttackCanceled += HeavyAttackCanceled;
    }

    private void OnDisable()
    {
        if (_input != null)
        {
            _input.OnAttackInput -= LightAttackPressed;
            _input.OnHeavyAttackStarted -= HeavyAttackStarted;
            _input.OnHeavyAttackCanceled -= HeavyAttackCanceled;
        }

        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        _attacking = false;
        _attackRoutine = null;
        _heavyHeld = false;
        if (anim) anim.speed = 1f;
    }

    private void Update()
    {
        TickHeavyHold();
        SetAnimations();
    }

    #endregion

    #region Input

    private void LightAttackPressed()
    {
        if (_attacking || !LocomotionController.Instance.canMove || LocomotionController.Instance.IsInteracting || Time.timeScale <= 0f) return;

        var animState = _lightComboIndex == 0 ? ATTACK1 : ATTACK2;
        _lightComboIndex = 1 - _lightComboIndex;

        StartAttack(lightAttack, animState);
    }

    private void HeavyAttackStarted()
    {
        _heavyHeld = true;
        _heavyFired = false;
        _heavyHeldTime = 0f;
    }

    private void HeavyAttackCanceled()
    {
        // Released early: the heavy attack never fires. Released after it fired: nothing to do.
        _heavyHeld = false;
    }

    // Fires the heavy attack as soon as right click has been held long enough. If another attack is
    // still playing at that moment it fires the instant that attack ends (as long as it's still held).
    private void TickHeavyHold()
    {
        if (!_heavyHeld || _heavyFired) return;
        if (!LocomotionController.Instance.canMove || LocomotionController.Instance.IsInteracting || Time.timeScale <= 0f) return;

        _heavyHeldTime += Time.deltaTime;
        if (_heavyHeldTime < heavyHoldTime || _attacking) return;

        _heavyFired = true; // one heavy attack per press
        StartAttack(heavyAttack, HEAVY_ATTACK);
    }

    #endregion

    #region Attack Flow

    private void StartAttack(AttackSettings settings, string animState)
    {
        var vitals = LocomotionController.Instance.Vitals;
        if (vitals) vitals.AddExertion(animState == HEAVY_ATTACK ? vitals.heavyAttackExertion : vitals.lightAttackExertion);
        _attacking = true;
        _attackRoutine = StartCoroutine(AttackRoutine(settings, animState));
    }

    private IEnumerator AttackRoutine(AttackSettings settings, string animState)
    {
        anim.speed = settings.animSpeed;
        ChangeAnimationState(animState, attackCrossFade, force: true);

        if (settings.windup > 0f)
            yield return new WaitForSeconds(settings.windup);

        yield return Sweep(settings);

        if (settings.recovery > 0f)
            yield return new WaitForSeconds(settings.recovery);

        _attacking = false;
        _attackRoutine = null;
        anim.speed = 1f;
    }

    /// <summary>
    /// Scans from the left edge of the arc to the right edge over the attack's sweep duration.
    /// Rays are cast along the camera's horizontal plane, so the strike spans the screen left-to-right
    /// instead of poking a single ray through the centre. Every enemy is damaged at most once per swing.
    /// </summary>
    private IEnumerator Sweep(AttackSettings settings)
    {
        var cam = Cam;
        _hitThisSwing.Clear();

        var halfArc = settings.arc * 0.5f;
        var previousAngle = -halfArc;
        CastScanRay(cam, settings, previousAngle); // left edge

        var elapsed = 0f;
        do
        {
            if (settings.sweepDuration > 0f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            var t = settings.sweepDuration > 0f ? Mathf.Clamp01(elapsed / settings.sweepDuration) : 1f;
            var angle = Mathf.Lerp(-halfArc, halfArc, t);

            // Fill in every ray between last frame's angle and this frame's, so a slow frame can't skip enemies.
            ScanBetween(cam, settings, previousAngle, angle);
            previousAngle = angle;
        }
        while (elapsed < settings.sweepDuration);
    }

    private void ScanBetween(Camera cam, AttackSettings settings, float fromAngle, float toAngle)
    {
        for (var a = fromAngle + scanAngleStep; a < toAngle; a += scanAngleStep)
            CastScanRay(cam, settings, a);

        if (toAngle > fromAngle)
            CastScanRay(cam, settings, toAngle);
    }

    private void CastScanRay(Camera cam, AttackSettings settings, float angle)
    {
        var camTransform = cam.transform;
        var origin = camTransform.position;
        var direction = Quaternion.AngleAxis(angle, camTransform.up) * camTransform.forward;

        if (debugDraw)
            Debug.DrawRay(origin, direction * settings.range, Color.yellow, 0.25f);

        var count = Physics.RaycastNonAlloc(origin, direction, _rayHits, settings.range, attackLayer, QueryTriggerInteraction.Collide);
        for (var i = 0; i < count; i++)
            DamageOnce(_rayHits[i].collider, settings.damage, _hitThisSwing);
    }

    #endregion

    #region Dash Strike

    /// <summary>
    /// Called every physics step while the player is dashing. Damages every enemy around
    /// <paramref name="center"/> that isn't already in <paramref name="alreadyHit"/>.
    /// </summary>
    public void DashStrike(Vector3 center, HashSet<IDamageable> alreadyHit)
    {
        // Never let the radius get smaller than the distance travelled per physics step.
        var radius = Mathf.Max(dashHitRadius, LocomotionController.Instance.DashSpeed * Time.fixedDeltaTime);

        var count = Physics.OverlapSphereNonAlloc(center, radius, _overlapHits, attackLayer);
        for (var i = 0; i < count; i++)
            DamageOnce(_overlapHits[i], dashDamage, alreadyHit);
    }

    #endregion

    private static bool DamageOnce(Collider col, float damage, HashSet<IDamageable> alreadyHit)
    {
        var damageable = col.GetComponentInParent<IDamageable>();
        if (damageable == null || !alreadyHit.Add(damageable)) return false;

        damageable.TakeDamage(damage);
        return true;
    }

    public void GroundSlam(Vector3 origin)
    {
        // A slam is infrequent: an allocating query avoids silently dropping crowded targets.
        var hits = Physics.OverlapSphere(origin, slamRadius, attackLayer, QueryTriggerInteraction.Collide);
        var hitEnemies = new HashSet<IDamageable>();
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponentInParent<IDamageable>();
            if (enemy == null || !hitEnemies.Add(enemy)) continue;
            enemy.KnockbackFrom(origin, slamKnockbackDistance);
            enemy.TakeDamage(slamDamage);
        }
    }

    #region Animation

    private void ChangeAnimationState(string newState, float fadeTime, bool force = false)
    {
        if (!force && _currentAnimStateName.Equals(newState)) return;

        _currentAnimStateName = newState;
        anim.CrossFadeInFixedTime(_currentAnimStateName, fadeTime);
    }

    private void SetAnimations()
    {
        if (!_attacking)
        {
            if (LocomotionController.Instance.CurrentState is SlideState or DashState or not GroundedState)
            {
                ChangeAnimationState(IDLE, locomotionCrossFade);
                return;
            }

            var velocity = PlayerVelocity;
            var horizontalSpeedSquared = velocity.x * velocity.x + velocity.z * velocity.z;
            ChangeAnimationState(horizontalSpeedSquared < 0.01f ? IDLE : WALK, locomotionCrossFade);
        }
    }

    #endregion

    #region Gizmos

    // Select the player in the editor to see the light (green) and heavy (red) sweep arcs.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.55f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
        var cam = Camera.main;
        if (!cam) return;

        DrawArcGizmo(cam.transform, lightAttack, Color.green);
        DrawArcGizmo(cam.transform, heavyAttack, Color.red);
    }

    private static void DrawArcGizmo(Transform view, AttackSettings settings, Color color)
    {
        const int segments = 24;
        var halfArc = settings.arc * 0.5f;
        var origin = view.position;

        Vector3 PointAt(float angle) =>
            origin + Quaternion.AngleAxis(angle, view.up) * view.forward * settings.range;

        Gizmos.color = color;
        var previous = PointAt(-halfArc);
        Gizmos.DrawLine(origin, previous);

        for (var i = 1; i <= segments; i++)
        {
            var point = PointAt(Mathf.Lerp(-halfArc, halfArc, i / (float)segments));
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        Gizmos.DrawLine(origin, previous);
    }

    #endregion
}
