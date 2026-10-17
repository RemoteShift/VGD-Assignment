using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LocomotionController), typeof(PlayerVitals))]
public class PlayerInteraction : Singleton<PlayerInteraction>
{
    public Camera view;
    [Min(0.1f)] public float interactionRange = 3f;
    public LayerMask interactionLayers = ~0;
    public InputAction interact = new InputAction("Use", InputActionType.Button, "<Keyboard>/e");
    public string Prompt { get; private set; } = "";
    private LocomotionController player => LocomotionController.Instance;
    private PlayerVitals vitals => PlayerVitals.Instance;
    private PlayerHealth health => PlayerHealth.Instance;
    private OxygenCanister usingCanister;
    private float useTime;

    private void Awake()
    {
        base.Awake();
        if (!view) view = GetComponentInChildren<Camera>();
    }

    private void OnEnable() => interact.Enable();
    private void OnDisable()
    {
        interact.Disable();
        CancelUse();
    }
    private void OnDestroy() => interact.Dispose();

    private void Update()
    {
        if (!player.canMove || vitals.IsDepleted || (health && health.IsDead))
        {
            CancelUse();
            return;
        }
        if (Time.timeScale <= 0f) return;
        if (usingCanister)
        {
            if (Vector3.Distance(view.transform.position, usingCanister.transform.position) > interactionRange)
            {
                CancelUse();
                return;
            }
            useTime += Time.deltaTime;
            float progress = Mathf.Clamp01(useTime / Mathf.Max(0.01f, usingCanister.useDuration));
            Prompt = $"USING OXYGEN  {progress:P0}";
            usingCanister.ShowUseProgress(progress);
            if (useTime >= usingCanister.useDuration)
            {
                usingCanister.Collect(vitals);
                CancelUse();
            }
            return;
        }

        if (player.IsInteracting) CancelUse();
        Prompt = "";
        if (!view || !Physics.Raycast(view.transform.position, view.transform.forward,
            out var hit, interactionRange, interactionLayers, QueryTriggerInteraction.Ignore)) return;
        var canister = hit.collider.GetComponentInParent<OxygenCanister>();
        if (!canister || canister.IsConsumed) return;
        Prompt = "[" + interact.GetBindingDisplayString() + "] USE OXYGEN CANISTER";
        if (interact.WasPressedThisFrame()) BeginUse(canister);
    }

    public bool BeginUse(OxygenCanister canister)
    {
        if (!canister || canister.IsConsumed || usingCanister || !player.canMove ||
            (CombatController.Instance.IsAttacking) ||
            vitals.IsDepleted || (health && health.IsDead) || Time.timeScale <= 0f || !view ||
            Vector3.Distance(view.transform.position, canister.transform.position) > interactionRange) return false;
        usingCanister = canister;
        useTime = 0f;
        player.ReturnToDefaultState();
        player.rb.linearVelocity = new Vector3(0f, player.rb.linearVelocity.y, 0f);
        player.IsInteracting = true;
        return true;
    }

    private void CancelUse()
    {
        if (usingCanister) usingCanister.ShowUseProgress(0f);
        usingCanister = null;
        if (player) player.IsInteracting = false;
        Prompt = "";
    }
}
