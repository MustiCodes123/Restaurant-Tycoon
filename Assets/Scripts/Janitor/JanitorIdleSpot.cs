using UnityEngine;

/// <summary>
/// Marks a position where janitors can stand idle when not cleaning.
/// Just a simple marker component - place on empty GameObjects in the scene.
/// </summary>
public class JanitorIdleSpot : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Is this spot currently occupied by a janitor?")]
    [SerializeField] private bool isOccupied = false;
    
    public bool IsOccupied => isOccupied;
    public Vector3 Position => transform.position;
    
    private JanitorController currentJanitor;
    
    /// <summary>
    /// Reserve this spot for a janitor
    /// </summary>
    public bool Reserve(JanitorController janitor)
    {
        if (isOccupied) return false;
        
        isOccupied = true;
        currentJanitor = janitor;
        return true;
    }
    
    /// <summary>
    /// Release this spot
    /// </summary>
    public void Release()
    {
        isOccupied = false;
        currentJanitor = null;
    }
    
    /// <summary>
    /// Check if a specific janitor owns this spot
    /// </summary>
    public bool IsOwnedBy(JanitorController janitor)
    {
        return currentJanitor == janitor;
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = isOccupied ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawIcon(transform.position + Vector3.up, "d_Prefab Icon", true);
    }
}
