using UnityEngine;

[RequireComponent(typeof(Light))]
public class PlayerFollowSpotlight : MonoBehaviour
{
    public Transform target;
    [Min(0f)] public float verticalOffset = 6f;
    [Tooltip("Seconds taken to catch up to player movement.")]
    [Min(0.01f)] public float followDelay = 0.4f;
    public float aimHeight = 0.8f;
    private Vector3 velocity;
    private bool positioned;

    private void LateUpdate()
    {
        if (!target)
        {
            var player = FindFirstObjectByType<LocomotionController>();
            if (!player) return;
            target = player.transform;
            positioned = false;
        }
        var desired = target.position + Vector3.up * verticalOffset;
        if (!positioned)
        {
            transform.position = desired;
            positioned = true;
        }
        else
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, followDelay);

        var direction = target.position + Vector3.up * aimHeight - transform.position;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }
}
