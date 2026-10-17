using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : Singleton<InputHandler>
{
    [Header("Action References")] 
    // Movement
    [SerializeField] private InputActionReference LookInput;
    [SerializeField] private InputActionReference MoveInput;
    [SerializeField] private InputActionReference JumpInput;
    [SerializeField] private InputActionReference DashInput;
    [SerializeField] private InputActionReference SlideInput;
    
    // Combat
    [SerializeField] private InputActionReference AttackInput;
    [SerializeField] private InputActionReference HeavyAttackInput;
    
    public event Action<Vector2, bool> OnLookInputChanged;
    public event Action OnAttackInput;
    
    // Heavy attack is a "hold" input, so consumers get the press and the release separately.
    public event Action OnHeavyAttackStarted;
    public event Action OnHeavyAttackCanceled;
    
    public Vector2 moveInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SlidePressed { get; private set; }
    public bool IsHoldingSlide { get; private set; } // Track continuous press state
    public bool DashPressed { get; private set; }

    private void OnEnable()
    {
        LookInput.action.Enable();
        MoveInput.action.Enable();
        JumpInput.action.Enable();
        DashInput.action.Enable();
        SlideInput.action.Enable();
        AttackInput.action.Enable();

        LookInput.action.performed += HandleLookInput;
        LookInput.action.canceled += HandleLookInput;
        AttackInput.action.performed += HandleAttackInput;

        if (HeavyAttackInput != null)
        {
            HeavyAttackInput.action.Enable();
            HeavyAttackInput.action.started += HandleHeavyAttackStarted;
            HeavyAttackInput.action.canceled += HandleHeavyAttackCanceled;
        }
        else
        {
            Debug.LogWarning("InputHandler: HeavyAttackInput is not assigned, so the heavy attack is disabled.", this);
        }
    }

    private void OnDisable()
    {
        LookInput.action.performed -= HandleLookInput;
        LookInput.action.canceled -= HandleLookInput;
        AttackInput.action.performed -= HandleAttackInput;

        if (HeavyAttackInput != null)
        {
            HeavyAttackInput.action.started -= HandleHeavyAttackStarted;
            HeavyAttackInput.action.canceled -= HandleHeavyAttackCanceled;
            HeavyAttackInput.action.Disable();
        }

        LookInput.action.Disable();
        MoveInput.action.Disable();
        JumpInput.action.Disable();
        DashInput.action.Disable();
        SlideInput.action.Disable();
        AttackInput.action.Disable();
    }

    private void Update()
    {
        moveInput = MoveInput.action.ReadValue<Vector2>();
        
        JumpPressed  = JumpInput.action.WasPressedThisFrame();
        DashPressed  = DashInput.action.WasPressedThisFrame();
        SlidePressed = SlideInput.action.WasPressedThisFrame();
        
        // Is user holding the key right now?
        IsHoldingSlide = SlideInput.action.IsPressed(); 
    }

    private void HandleLookInput(InputAction.CallbackContext context)
    {
        OnLookInputChanged?.Invoke(context.ReadValue<Vector2>(), context.control.device is Gamepad);
    }
    
    private void HandleAttackInput(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnAttackInput?.Invoke();
        }
    }

    private void HandleHeavyAttackStarted(InputAction.CallbackContext context)
    {
        OnHeavyAttackStarted?.Invoke();
    }

    private void HandleHeavyAttackCanceled(InputAction.CallbackContext context)
    {
        OnHeavyAttackCanceled?.Invoke();
    }
}
