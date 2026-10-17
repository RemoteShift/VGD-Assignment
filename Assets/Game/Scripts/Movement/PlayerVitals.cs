using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(LocomotionController))]
public class PlayerVitals : Singleton<PlayerVitals>
{
    [Header("Oxygen")]
    [Min(1f)] public float maxOxygen = 100f;
    [Min(0f)] public float startingOxygen = 100f;
    [Tooltip("Oxygen units consumed per second at zero exertion.")]
    [Min(0f)] public float baseOxygenDrain = 0.1f;
    [Tooltip("Oxygen drain multiplier at maximum exertion. Interpolates from 1.")]
    [FormerlySerializedAs("maxEnduranceOxygenMultiplier")]
    [Min(1f)] public float maxExertionOxygenMultiplier = 3f;

    [Header("Exertion")]
    [FormerlySerializedAs("maxEndurance")]
    [Min(1f)] public float maxExertion = 100f;
    [FormerlySerializedAs("startingEndurance")]
    [Min(0f)] public float startingExertion;
    [FormerlySerializedAs("enduranceRecoveryPerSecond")]
    [Min(0f)] public float exertionRecoveryPerSecond = 12f;
    [Tooltip("Recovery rate while movement input is held, relative to idle. 0.75 means 25% slower recovery.")]
    [Range(0f, 1f)] public float movingRecoveryMultiplier = 0.75f;
    [Min(0f)] public float recoveryDelay = 0.75f;

    [Header("Movement At Maximum Exertion")]
    [Tooltip("Horizontal movement speed at full exertion. 0.5 means half speed; normal speed returns below maximum.")]
    [Range(0.1f, 1f)] public float exhaustedSpeedMultiplier = 0.5f;
    public bool IsExhausted => Exertion >= maxExertion;
    public float MovementSpeedMultiplier => IsExhausted ? exhaustedSpeedMultiplier : 1f;

    [Header("Exertion Per Action")]
    [Tooltip("Exertion added once for a normal jump or bullet jump.")]
    [Min(0f)] public float jumpExertion = 3f;
    [FormerlySerializedAs("dashEndurance")]
    [Min(0f)] public float dashExertion = 12f;
    [FormerlySerializedAs("lightAttackEndurance")]
    [Min(0f)] public float lightAttackExertion = 4f;
    [FormerlySerializedAs("heavyAttackEndurance")]
    [Min(0f)] public float heavyAttackExertion = 14f;
    [FormerlySerializedAs("slamImpactEndurance")]
    [Min(0f)] public float slamImpactExertion = 18f;

    [Header("Exertion Per Second")]
    [FormerlySerializedAs("slideEndurancePerSecond")]
    [Min(0f)] public float slideExertionPerSecond = 8f;
    [FormerlySerializedAs("slamFallEndurancePerSecond")]
    [Min(0f)] public float slamFallExertionPerSecond = 12f;

    [Header("Gameplay & Fainting")]
    public bool simulateVitals = true;
    [Tooltip("No oxygen drain or exertion gain in these scenes.")]
    public string[] safeScenes = { "Stage" };
    public bool restartArenaOnEmpty = true;
    [Min(0f)] public float faintDelay = 1.5f;
    public UnityEvent onOxygenDepleted = new UnityEvent();

    [Header("Runtime Values")]
    [SerializeField] private float oxygen;
    [FormerlySerializedAs("endurance")]
    [SerializeField] private float exertion;
    public float Oxygen => oxygen;
    public float Exertion => exertion;
    public float OxygenFraction => Mathf.Clamp01(oxygen / Mathf.Max(1f, maxOxygen));
    public float ExertionFraction => Mathf.Clamp01(exertion / Mathf.Max(1f, maxExertion));
    public float OxygenDrainPerSecond => baseOxygenDrain * Mathf.Lerp(1f, maxExertionOxygenMultiplier, ExertionFraction);
    public bool IsDepleted { get; private set; }

    private LocomotionController locomotion;
    private float lastExertionTime = float.NegativeInfinity;
    private bool safeScene;
    private bool restarting;

    private void Awake()
    {
        base.Awake();
        locomotion = GetComponent<LocomotionController>();
        ResetVitals();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnSceneChanged;
        RefreshSafeScene(SceneManager.GetActiveScene());
    }

    private void OnDisable() => SceneManager.activeSceneChanged -= OnSceneChanged;
    private void OnSceneChanged(Scene previous, Scene next) => RefreshSafeScene(next);

    private void RefreshSafeScene(Scene scene)
    {
        safeScene = System.Array.IndexOf(safeScenes, scene.name) >= 0;
    }

    private bool IsActive => simulateVitals && !safeScene && !IsDepleted && !restarting && locomotion.canMove && Time.timeScale > 0f;

    private void Update()
    {
        if (!IsActive) return;

        oxygen = Mathf.Clamp(oxygen - OxygenDrainPerSecond * Time.deltaTime, 0f, maxOxygen);
        if (Time.time - lastExertionTime >= recoveryDelay)
        {
            bool hasMovementInput = locomotion.input.moveInput.sqrMagnitude >= 0.01f;
            float recoveryRate = exertionRecoveryPerSecond * (hasMovementInput ? movingRecoveryMultiplier : 1f);
            exertion = Mathf.MoveTowards(exertion, 0f, recoveryRate * Time.deltaTime);
        }

        if (oxygen <= 0f)
        {
            IsDepleted = true;
            onOxygenDepleted.Invoke();
            if (restartArenaOnEmpty) RestartAfterDefeat();
        }
    }

    public void AddExertion(float amount)
    {
        if (!IsActive || amount <= 0f) return;
        exertion = Mathf.Clamp(exertion + amount, 0f, maxExertion);
        lastExertionTime = Time.time;
    }

    public void RestoreOxygen(float amount)
    {
        if (IsDepleted) return;
        oxygen = Mathf.Clamp(oxygen + Mathf.Max(0f, amount), 0f, maxOxygen);
    }

    public void ResetVitals()
    {
        oxygen = Mathf.Clamp(startingOxygen, 0f, maxOxygen);
        exertion = Mathf.Clamp(startingExertion, 0f, maxExertion);
        lastExertionTime = float.NegativeInfinity;
        IsDepleted = false;
    }

    public void RestartAfterDefeat()
    {
        if (restarting) return;
        restarting = true;
        StartCoroutine(RestartArena());
    }

    private IEnumerator RestartArena()
    {
        locomotion.SwitchState(new AirborneState(locomotion, locomotion.rb));
        locomotion.canMove = false;
        locomotion.rb.linearVelocity = Vector3.zero;
        var combat = GetComponent<CombatController>();
        if (combat) combat.enabled = false;
        var cameraLook = transform.root.GetComponentInChildren<CameraLook>();
        if (cameraLook) cameraLook.UnlockCursor();
        yield return new WaitForSeconds(faintDelay);

        // Bring the persistent player back into the arena so reload replaces it too.
        var scene = SceneManager.GetActiveScene();
        SceneManager.MoveGameObjectToScene(transform.root.gameObject, scene);
        SceneManager.LoadScene(scene.path);
    }
}
