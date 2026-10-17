using UnityEngine;

public class SlideState : PlayerLocomState
{
    private Vector3 _slideDir;
    
    public SlideState(LocomotionController playerLocomotion, Rigidbody rb) 
        : base(playerLocomotion, rb) { }

    public override void Enter()
    {
        base.Enter();
        
        player.SetSlideHeight();
        _slideDir = GetMoveDirection();
    }

    public override void Update()
    {
        base.Update();
        if (player.CurrentState != this) return;

        if(player.input.JumpPressed && player.GroundCheck.IsGrounded)
        {
            if (Camera.main)
            {
                Vector3 lookDir = Camera.main.transform.forward;
                player.BulletJump(lookDir);
            }

            return;
        }
    }
    
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        if (!player.input.IsHoldingSlide)
        {
            player.ReturnToDefaultState();
            return;
        }

        if (player.Vitals)
            player.Vitals.AddExertion(player.Vitals.slideExertionPerSecond * Time.fixedDeltaTime);

        Rb.linearVelocity = new Vector3(
            _slideDir.x * player.SlideSpeed,
            Rb.linearVelocity.y,
            _slideDir.z * player.SlideSpeed
        );

    }

    public override void Exit()
    {
        base.Exit();
        player.SetStandingHeight();
    }
}
