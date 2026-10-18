using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Light))]
public class PlayerNightFlashlight : MonoBehaviour
{
    public GardenDayNightCycle cycle;
    public Camera playerCamera;
    public Vector3 cameraOffset = new Vector3(0.2f, -0.15f, 0.35f);
    public bool switchOnAtNight = true;
    public InputAction toggle = new InputAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
    public bool IsOn => beam && beam.enabled;
    private Light beam;
    private bool wasNight;
    private bool requestedOn;

    private void Awake()
    {
        beam = GetComponent<Light>();
        beam.enabled = false;
    }
    private void OnEnable() => toggle.Enable();
    private void OnDisable()
    {
        toggle.Disable();
        if (beam) beam.enabled = false;
    }
    private void OnDestroy() => toggle.Dispose();

    private void Update()
    {
        bool night = cycle && cycle.IsNight;
        if (night && !wasNight) requestedOn = switchOnAtNight;
        if (!night) requestedOn = false;
        if (night && Time.timeScale > 0f && toggle.WasPressedThisFrame()) requestedOn = !requestedOn;
        beam.enabled = night && requestedOn && playerCamera;
        wasNight = night;
    }

    private void LateUpdate()
    {
        if (!playerCamera)
        {
            var player = FindFirstObjectByType<LocomotionController>();
            if (player) playerCamera = player.GetComponentInChildren<Camera>();
            if (!playerCamera) return;
        }
        transform.SetPositionAndRotation(playerCamera.transform.TransformPoint(cameraOffset), playerCamera.transform.rotation);
    }
}
