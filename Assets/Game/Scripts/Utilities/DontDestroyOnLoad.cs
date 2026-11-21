using UnityEngine;

public class DontDestroyOnLoad : MonoBehaviour
{
    // Story arenas own their rig so reload also restores pickups, input and physics.
    public bool sceneOwned;
    private void Awake()
    {
        if (!sceneOwned) DontDestroyOnLoad(gameObject);
    }
}
