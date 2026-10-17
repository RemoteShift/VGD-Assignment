using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody), typeof(GroundCheck))]
public class LocomotionController : Singleton<LocomotionController>
{
    private PlayerLocomState currentState;
    public PlayerLocomState CurrentState => currentState;
    
    [SerializeField] private string currentStateName;
    
    public Rigidbody rb;
    public GroundCheck GroundCheck;
    public PlayerVitals Vitals { get; private set; }
    public float MovementSpeedMultiplier => Vitals ? Vitals.MovementSpeedMultiplier : 1f;
    public float MoveSpeed => moveSpeed * MovementSpeedMultiplier;
    public float DashSpeed => dashSpeed * MovementSpeedMultiplier;
    public float SlideSpeed => slideSpeed * MovementSpeedMultiplier;
    
    public InputHandler input => InputHandler.Instance;

    #region Movement Settings

    [Header("Movement Settings")]
    public float moveSpeed = 1.0f;

    public float jumpSpeed = 0.8f;
    public float dashSpeed = 2.0f;
    public float dashDuration = 0.5f;
    public int maxDashCount = 3;
    public float dashRecoveryDuration = 1f;
    public float slideSpeed = 1.5f;
    public float AirAcc = 0.1f;
    public float slamSpeed = 1.5f;

    #endregion

    public bool canMove;
    public bool IsInteracting { get; set; }
    
    private int _dashCount = 0;
    public int DashCount => _dashCount;
    [HideInInspector] public UnityEvent OnDashCountChanged;
    private float _dashIncTimer = 0;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        Vitals = GetComponent<PlayerVitals>();

        _dashCount = maxDashCount;
        _dashIncTimer = dashRecoveryDuration;
    }

    private void Start()
    {
        ReturnToDefaultState();
    }

    private void OnDisable()
    {
        RestoreAllPassThrough();
    }

    private void Update()
    {
        if (!canMove || IsInteracting) return;
        
        currentState?.Update();
        if(_dashCount < maxDashCount)
        {
            _dashIncTimer -= Time.deltaTime;
            if (_dashIncTimer <= 0)
            {
                _dashCount++;
                OnDashCountChanged.Invoke();
                _dashIncTimer = dashRecoveryDuration;
            }
        }
    }

    private void FixedUpdate()
    {
        if(canMove && !IsInteracting)
            currentState?.FixedUpdate();
    }

    public void ResetPositionRotation(Vector3 startPosition, Quaternion startRotation)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        rb.position = startPosition;
        rb.rotation = startRotation;
        transform.SetPositionAndRotation(startPosition, startRotation);
        
        Physics.SyncTransforms();
        rb.Sleep();
        
        var euler = startRotation.eulerAngles;
            
        CameraLook.Instance.ResetRotation(targetYaw: euler.y, targetPitch: euler.x);

        GroundCheck.SetGroundedState(true);
        ReturnToDefaultState();
    }
    
    public void ResetPositionRotation(Vector3 startPosition, Vector3 startEulerAngles)
    {
        ResetPositionRotation(startPosition, Quaternion.Euler(startEulerAngles));
    }

    public void Jump()
    {
        if (Vitals) Vitals.AddExertion(Vitals.jumpExertion);
        GroundCheck.SetGroundedState(false);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpSpeed, rb.linearVelocity.z);
    }
    
    public void BulletJump(Vector3 aimDirection)
    {
        if (Vitals) Vitals.AddExertion(Vitals.jumpExertion);
        Vector3 launchDir = aimDirection.normalized;
        
        if (launchDir.y < 0.2f)
        {
            launchDir.y = 0.2f;
            launchDir = launchDir.normalized;
        }

        var bulletJumpForce = jumpSpeed * 3f;
        
        transform.position += Vector3.up * 0.15f;
        
        rb.linearVelocity = new Vector3(launchDir.x * bulletJumpForce,
            Mathf.Clamp(launchDir.y * bulletJumpForce, jumpSpeed, jumpSpeed * 2f), launchDir.z * bulletJumpForce);
        GroundCheck.SetGroundedState(false);
        SwitchState(new AirborneState(this, rb));
    }

    public void Dash()
    {
        if (_dashCount > 0)
        {
            SwitchState(new DashState(this, rb));
            _dashCount--;
            if (Vitals) Vitals.AddExertion(Vitals.dashExertion);
            OnDashCountChanged.Invoke();
        }
    }
    
    public void SwitchState(PlayerLocomState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
        
        currentStateName = currentState != null ? currentState.GetType().Name : "None";
    }
    
    public void ReturnToDefaultState()
    {
        if (GroundCheck.IsGrounded)
        {
            SwitchState(new GroundedState(this, rb));
        }
        else
        {
            SwitchState(new AirborneState(this, rb));
        }
    }

    #region Dash Pass-Through

    // Colliders the player is currently ignoring so a dash can carry it straight through enemies.
    private readonly HashSet<Collider> _passThroughColliders = new HashSet<Collider>();
    private readonly List<Collider> _passThroughScratch = new List<Collider>();
    private Coroutine _passThroughRestoreRoutine;

    // Safety net: collisions always come back after this long, even if the player stays overlapped.
    private const float PassThroughRestoreTimeout = 2f;

    public Collider PlayerCollider => playerCollider;

    /// <summary>
    /// Makes the player ignore every collider on <paramref name="layers"/> within <paramref name="radius"/>
    /// until <see cref="EndPassThrough"/> is called.
    /// </summary>
    public void BeginPassThrough(LayerMask layers, float radius)
    {
        // Back-to-back dashes: keep what's already ignored and just add to it.
        if (_passThroughRestoreRoutine != null)
        {
            StopCoroutine(_passThroughRestoreRoutine);
            _passThroughRestoreRoutine = null;
        }

        var found = Physics.OverlapSphere(playerCollider.bounds.center, radius, layers, QueryTriggerInteraction.Ignore);
        foreach (var other in found)
        {
            if (other == playerCollider) continue;

            Physics.IgnoreCollision(playerCollider, other, true);
            _passThroughColliders.Add(other);
        }
    }

    /// <summary>
    /// Ends the pass-through. Collisions are restored per enemy once the player is no longer overlapping it,
    /// so finishing a dash inside an enemy doesn't violently shove the player out.
    /// </summary>
    public void EndPassThrough()
    {
        if (_passThroughColliders.Count == 0) return;

        if (!isActiveAndEnabled)
        {
            RestoreAllPassThrough();
            return;
        }

        if (_passThroughRestoreRoutine == null)
            _passThroughRestoreRoutine = StartCoroutine(RestorePassThroughWhenClear());
    }

    private IEnumerator RestorePassThroughWhenClear()
    {
        var wait = new WaitForFixedUpdate();
        var timeout = PassThroughRestoreTimeout;

        while (_passThroughColliders.Count > 0 && timeout > 0f)
        {
            yield return wait;
            timeout -= Time.fixedDeltaTime;

            _passThroughScratch.Clear();
            _passThroughScratch.AddRange(_passThroughColliders);

            foreach (var other in _passThroughScratch)
            {
                if (other == null)
                {
                    _passThroughColliders.Remove(other);
                    continue;
                }

                if (IsOverlappingPlayer(other)) continue;

                Physics.IgnoreCollision(playerCollider, other, false);
                _passThroughColliders.Remove(other);
            }
        }

        RestoreAllPassThrough();
    }

    private bool IsOverlappingPlayer(Collider other)
    {
        if (!playerCollider.bounds.Intersects(other.bounds)) return false;

        // ComputePenetration can't handle non-convex mesh colliders, so stay cautious for those.
        if (other is MeshCollider { convex: false }) return true;

        return Physics.ComputePenetration(
            playerCollider, playerCollider.transform.position, playerCollider.transform.rotation,
            other, other.transform.position, other.transform.rotation,
            out _, out _);
    }

    private void RestoreAllPassThrough()
    {
        if (playerCollider != null)
        {
            foreach (var other in _passThroughColliders)
            {
                if (other != null)
                    Physics.IgnoreCollision(playerCollider, other, false);
            }
        }

        _passThroughColliders.Clear();
        _passThroughRestoreRoutine = null;
    }

    #endregion

    #region Height Shrinking (for sliding)

    [Header("Height Shrinking (for sliding)")]
    [Header("References")]
    [SerializeField] private CapsuleCollider playerCollider;
    [SerializeField] private Transform cameraHolderTransform;
    
    [Header("Height settings")]
    [SerializeField] private float standingHeight = 2.0f;
    [SerializeField] private float slidingHeight = 1.0f;
    [SerializeField] private float standingCamY = 0.6f;
    [SerializeField]  private float slideCamY = -0.2f;

    public void SetSlideHeight()
    {
        playerCollider.height = slidingHeight;
        
        playerCollider.center = new Vector3(0f, -(standingHeight - slidingHeight) / 2f, 0f);

        var camPos = cameraHolderTransform.localPosition;
        cameraHolderTransform.localPosition = new Vector3(camPos.x, slideCamY, camPos.z);
    }
    
    public void SetStandingHeight()
    {
        playerCollider.height = standingHeight;
        playerCollider.center = Vector3.zero;
        
        var camPos = cameraHolderTransform.localPosition;
        cameraHolderTransform.localPosition = new Vector3(camPos.x, standingCamY, camPos.z);
    }

    #endregion
}
