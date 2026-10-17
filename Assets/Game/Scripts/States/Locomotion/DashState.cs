using System.Collections.Generic;
using UnityEngine;

public class DashState : PlayerLocomState
{
    // How far past the dash's own length we look for enemies to pass through, to cover enemies that move into the path.
    private const float PassThroughMargin = 3f;

    private float _dashTimer;
    private Vector3 _dashDir;
    
    private Vector3 _initialVelocity;

    // Everything already damaged during this dash, so each enemy is hit once per dash.
    private readonly HashSet<IDamageable> _hitThisDash = new HashSet<IDamageable>();

    public DashState(LocomotionController playerLocomotion, Rigidbody rb) 
        : base(playerLocomotion, rb) { }

    public override void Enter()
    {
        base.Enter();
        
        _dashDir = GetMoveDirection();
        _dashTimer = player.dashDuration;
        _initialVelocity = Rb.linearVelocity;
        _hitThisDash.Clear();

        // Dash straight through enemies instead of bouncing off them.
        var dashDistance = player.DashSpeed * player.dashDuration;
        player.BeginPassThrough(CombatController.Instance.attackLayer, dashDistance + PassThroughMargin);
    }

    public override void Update()
    {
        if(player.input.SlidePressed && !player.GroundCheck.IsGrounded)
        {
            player.SwitchState(new GroundSlamState(player, Rb));
            return;
        }
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // 1. Check the actual velocity resulting from LAST frame's physics/collision resolution
        // (Skip this check on the first frame of the dash using a timer buffer)
        if (_dashTimer < player.dashDuration - Time.fixedDeltaTime)
        {
            Vector3 actualHorizontalVel = new Vector3(Rb.linearVelocity.x, 0f, Rb.linearVelocity.z);

            // If physics reduced our velocity significantly (e.g. hitting a wall), cancel dash!
            if (actualHorizontalVel.magnitude < player.DashSpeed * 0.5f)
            {
                player.ReturnToDefaultState();
                return;
            }
        }

        _dashTimer -= Time.fixedDeltaTime;

        // 2. NOW apply the dash velocity for THIS frame
        Rb.linearVelocity = new Vector3(_dashDir.x * player.DashSpeed, 0f, _dashDir.z * player.DashSpeed);

        // 3. Damage anything we're passing through
        CombatController.Instance.DashStrike(player.PlayerCollider.bounds.center, _hitThisDash);

        if (_dashTimer <= 0f)
        {
            player.ReturnToDefaultState();
        }
    }

    public override void Exit()
    {
        base.Exit();
        // Restore the horizontal velocity from before the dash.
        Rb.linearVelocity = new Vector3(_initialVelocity.x, 0f, _initialVelocity.z);

        // Enemy collisions come back once the player has cleared them.
        player.EndPassThrough();
    }
}
