using UnityEngine;

/// <summary>
/// Marks a position where service guys can stand idle when not serving customers.
/// Place on empty GameObjects in the scene near the store.
/// </summary>
public class ServiceGuyIdleSpot : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Is this spot currently occupied by a service guy?")]
    [SerializeField] private bool isOccupied = false;
    
    public bool IsOccupied => isOccupied;
    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;
    
    private ServiceGuyController currentServiceGuy;
    
    /// <summary>
    /// Reserve this spot for a service guy
    /// </summary>
    public bool Reserve(ServiceGuyController serviceGuy)
    {
        if (isOccupied) return false;
        
        isOccupied = true;
        currentServiceGuy = serviceGuy;
        return true;
    }
    
    /// <summary>
    /// Release this spot
    /// </summary>
    public void Release()
    {
        isOccupied = false;
        currentServiceGuy = null;
    }
    
    /// <summary>
    /// Check if a specific service guy owns this spot
    /// </summary>
    public bool IsOwnedBy(ServiceGuyController serviceGuy)
    {
        return currentServiceGuy == serviceGuy;
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = isOccupied ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        
        // Draw forward direction
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 0.8f);
    }
}
