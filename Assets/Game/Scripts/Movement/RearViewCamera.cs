using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class RearViewCamera : MonoBehaviour
{
    public Camera frontCamera;
    public Camera rearCamera;
    public CameraLook cameraLook;
    [Min(0f)] public float backwardOffset = 0.35f;
    public InputAction lookBehind = new InputAction("Look Behind", InputActionType.Button, "<Keyboard>/q");
    public bool IsViewingBehind { get; private set; }
    private Quaternion fixedRotation;

    private void OnEnable() => lookBehind.Enable();
    private void OnDisable()
    {
        lookBehind.Disable();
        EndRearView();
    }
    private void OnDestroy() => lookBehind.Dispose();

    private void Update()
    {
        bool shouldView = frontCamera && rearCamera && cameraLook && cameraLook.canLook &&
            Time.timeScale > 0f && lookBehind.IsPressed();
        if (shouldView && !IsViewingBehind)
        {
            // Capture a level, rear-facing direction once. Mouse movement cannot rotate it.
            fixedRotation = Quaternion.Euler(0f, frontCamera.transform.eulerAngles.y + 180f, 0f);
            IsViewingBehind = true;
            cameraLook.RearViewLocked = true;
            UpdateRearPose();
            rearCamera.enabled = true;
        }
        else if (!shouldView && IsViewingBehind) EndRearView();
    }

    private void LateUpdate()
    {
        if (IsViewingBehind) UpdateRearPose();
    }

    private void UpdateRearPose()
    {
        rearCamera.transform.SetPositionAndRotation(
            frontCamera.transform.position + fixedRotation * Vector3.forward * backwardOffset,
            fixedRotation);
    }

    private void EndRearView()
    {
        IsViewingBehind = false;
        if (rearCamera) rearCamera.enabled = false;
        if (cameraLook) cameraLook.RearViewLocked = false;
    }
}
