using UnityEngine;
using Unity.Cinemachine;

public class StoreCameraTrigger : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private CinemachineCamera storeCamera;
    [SerializeField] private int activePriority = 10;
    [SerializeField] private int inactivePriority = 0;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (storeCamera != null)
            {
                storeCamera.Priority = activePriority;
                Debug.Log($"[StoreCameraTrigger] Player entered store - Camera priority set to {activePriority}");
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (storeCamera != null)
            {
                storeCamera.Priority = inactivePriority;
                Debug.Log($"[StoreCameraTrigger] Player exited store - Camera priority set to {inactivePriority}");
            }
        }
    }
}
