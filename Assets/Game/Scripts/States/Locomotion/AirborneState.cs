using UnityEngine;

public class AirborneState : PlayerLocomState
{
    public AirborneState(LocomotionController locomotionController, Rigidbody rb) : base(locomotionController, rb) { }

    public override void Update()
    {
        base.Update();
        if (player.CurrentState != this) return;

        if (player.GroundCheck.IsGrounded && Rb.linearVelocity.y <= 0f)
        {
            player.SwitchState(new GroundedState(player, Rb));
            return;
        }

        if (player.input.SlidePressed)
        {
            player.SwitchState(new GroundSlamState(player, Rb));
            return;
        }
    }
    
    public override void FixedUpdate()
    {
        Vector3 dir = GetCameraRelativeMoveDirection();
        Vector3 velocity = Rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

        // Preserve launch momentum without letting air steering build unlimited speed.
        float maxSpeed = player.Vitals && player.Vitals.IsExhausted
            ? player.MoveSpeed
            : Mathf.Max(player.MoveSpeed, horizontalVelocity.magnitude);
        horizontalVelocity += dir * player.AirAcc * Time.fixedDeltaTime;
        horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, maxSpeed);
        Rb.linearVelocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
    }
}
