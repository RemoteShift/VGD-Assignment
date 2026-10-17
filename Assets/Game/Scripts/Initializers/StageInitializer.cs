using UnityEngine;

public class StageInitializer : MonoBehaviour
{
    void Start()
    {
        CameraLook.Instance.UnlockCursor();
    }
}
