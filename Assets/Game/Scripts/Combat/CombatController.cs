using System;
using UnityEngine;

public class CombatController : Singleton<CombatController>
{
    private string _currentAnimStateName = IDLE;
    [SerializeField] private Animator anim;

    private const string IDLE = "Idle";
    private const string WALK = "Walk";
    private const string ATTACK1 = "Attack 1";
    private const string ATTACK2 = "Attack 2";

    public float attackDistance = 3f;
    public float attackDelay = 0.4f;
    public float attackSpeed = 1f;
    public int attackDamage = 1;
    public LayerMask attackLayer;
    private bool _attacking;
    private bool _readyToAttack = true;
    private int _attackCount = 0;
    
    private Vector3 PlayerVelocity => LocomotionController.Instance.rb.linearVelocity;

    private void OnEnable()
    {
        InputHandler.Instance.OnAttackInput += Attack;
    }

    private void Update()
    {
        SetAnimations();
    }

    private void Attack()
    {
        if (_attacking || !_readyToAttack) return;

        _readyToAttack = false;
        _attacking = true;
        
        Invoke(nameof(ResetAttack), attackSpeed);
        Invoke(nameof(AttackRaycast), attackDelay);

        if (_attackCount == 0)
        {
            ChangeAnimationState(ATTACK1);
            _attackCount++;
        }
        else
        {
            ChangeAnimationState(ATTACK2);
            _attackCount = 0;
        }
    }

    private void ResetAttack()
    {
        _attacking = false;
        _readyToAttack = true;
    }

    private void AttackRaycast()
    {
        Camera cam = CameraLook.Instance.GetComponentInChildren<Camera>();
        Vector3 attackOrigin = cam.transform.position;
        Vector3 attackDirection = cam.transform.forward;

        if (Physics.Raycast(attackOrigin, attackDirection, out var hit, attackDistance, attackLayer))
        {
            var damageable = hit.collider.GetComponent<IDamageable>();
            damageable?.TakeDamage(attackDamage);
        }
    }
    
    private void ChangeAnimationState(string newState)
    {
        if(_currentAnimStateName.Equals(newState)) return;
        
        _currentAnimStateName = newState;
        anim.CrossFadeInFixedTime(_currentAnimStateName, 0.2f);
    }

    private void SetAnimations()
    {
        if (!_attacking)
        {
            if (LocomotionController.Instance.CurrentState is SlideState or DashState)
            {
                ChangeAnimationState(IDLE);
                return;
            }

            ChangeAnimationState(PlayerVelocity switch{
                {x: <0.1f, z: <0.1f} => IDLE,
                _ => WALK
            });
        }
    }
}
