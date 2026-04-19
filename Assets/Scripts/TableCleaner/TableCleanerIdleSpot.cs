using UnityEngine;

/// <summary>
/// Marks a position where table cleaners can stand idle when not collecting garbage.
/// Just a simple marker component - place on empty GameObjects in the scene.
/// </summary>
public class TableCleanerIdleSpot : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Is this spot currently occupied by a table cleaner?")]
    [SerializeField] private bool isOccupied = false;
    
    public bool IsOccupied => isOccupied;
    public Vector3 Position => transform.position;
    
    private TableCleanerController currentCleaner;
    
    /// <summary>
    /// Reserve this spot for a table cleaner
    /// </summary>
    public bool Reserve(TableCleanerController cleaner)
    {
        if (isOccupied) return false;
        
        isOccupied = true;
        currentCleaner = cleaner;
        return true;
    }
    
    /// <summary>
    /// Release this spot
    /// </summary>
    public void Release()
    {
        isOccupied = false;
        currentCleaner = null;
    }
    
    /// <summary>
    /// Check if a specific cleaner owns this spot
    /// </summary>
    public bool IsOwnedBy(TableCleanerController cleaner)
    {
        return currentCleaner == cleaner;
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = isOccupied ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawIcon(transform.position + Vector3.up, "d_Prefab Icon", true);
    }
}
