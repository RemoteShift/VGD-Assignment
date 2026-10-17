using UnityEngine;

public class GroundSlamState : PlayerLocomState
{
    private float startHeight;
    public GroundSlamState(LocomotionController locomotionController, Rigidbody rb) : base(locomotionController, rb) { }

    public override void Enter()
    {
        startHeight = Rb.position.y;
        IgnoreNearbyEnemies();
    }

    public override void Update()
    {
        if (TryLand()) return;
        base.Update();
    }

    public override void FixedUpdate()
    {
        if (TryLand()) return;
        IgnoreNearbyEnemies();
        if (player.Vitals)
            player.Vitals.AddExertion(player.Vitals.slamFallExertionPerSecond * Time.fixedDeltaTime);
        Rb.linearVelocity = new Vector3(0f, -player.slamSpeed, 0f);
    }

    public override void Exit()
    {
        player.EndPassThrough();
    }
    
    private bool TryLand()
    {
        if (!player.GroundCheck.IsGrounded) return false;

        var bounds = player.PlayerCollider.bounds;
        var combat = CombatController.Instance;
        if (startHeight - Rb.position.y >= combat.minimumSlamDistance)
        {
            combat.GroundSlam(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            if (player.Vitals) player.Vitals.AddExertion(player.Vitals.slamImpactExertion);
        }
        player.SwitchState(new GroundedState(player, Rb));
        return true;
    }

    private void IgnoreNearbyEnemies()
    {
        var radius = player.PlayerCollider.bounds.extents.magnitude + player.slamSpeed * Time.fixedDeltaTime + 1f;
        player.BeginPassThrough(CombatController.Instance.attackLayer, radius);
    }
}
