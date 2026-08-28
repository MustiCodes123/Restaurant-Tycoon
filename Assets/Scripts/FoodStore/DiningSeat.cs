using UnityEngine;

/// <summary>
/// A single seat at a dining table.
/// Tracks if occupied by a customer.
/// </summary>
public class DiningSeat : MonoBehaviour
{
    [Header("Positions")]
    [SerializeField] private Transform sitPoint; // Where customer sits (exact final position)
    [SerializeField] private Transform approachPoint; // Where customer stands before sitting (should be on NavMesh surface)
    [Tooltip("Optional: Ground-level position for NavMesh navigation. If null, uses approachPoint.")]
    [SerializeField] private Transform navMeshTarget; // Ground-level target for NavMesh (optional)
    
    [Header("Facing Direction")]
    [Tooltip("Optional: A transform that defines which direction the customer should face when seated. If null, uses sitPoint's forward.")]
    [SerializeField] private Transform facingTarget; // Customer will look at this point when seated
    [Tooltip("If true, customer faces the facingTarget. If false, customer uses sitPoint rotation.")]
    [SerializeField] private bool useFacingTarget = false;
    
    [Header("Settings")]
    [SerializeField] private float eatingDuration = 5f;
    
    private DiningTable parentTable;
    private FoodCustomer occupyingCustomer;
    private bool isOccupied = false;
    
    public Transform SitPoint => sitPoint;
    public Transform ApproachPoint => approachPoint;
    public DiningTable ParentTable => parentTable;
    public float EatingDuration => eatingDuration;
    public bool IsOccupied
    {
        get
        {
            ClearMissingOccupant();
            return isOccupied;
        }
    }
    public bool IsAvailable
    {
        get
        {
            ClearMissingOccupant();
            return !isOccupied && (parentTable == null || !parentTable.HasGarbage);
        }
    }
    public FoodCustomer OccupyingCustomer => occupyingCustomer;
    
    /// <summary>
    /// Gets the position for NavMesh navigation (ground level)
    /// </summary>
    public Vector3 GetNavMeshTargetPosition()
    {
        if (navMeshTarget != null)
        {
            return navMeshTarget.position;
        }
        
        if (approachPoint != null)
        {
            return approachPoint.position;
        }
        
        // Fallback: use sitPoint but keep it on ground level for NavMesh
        if (sitPoint != null)
        {
            Vector3 groundLevel = sitPoint.position;
            groundLevel.y = transform.position.y; // Use ground Y position
            return groundLevel;
        }
        
        return transform.position;
    }
    
    /// <summary>
    /// Gets the rotation the customer should face when seated
    /// </summary>
    public Quaternion GetSeatedRotation()
    {
        if (useFacingTarget && facingTarget != null && sitPoint != null)
        {
            // Calculate rotation to look at facing target from sit position
            Vector3 directionToTarget = facingTarget.position - sitPoint.position;
            directionToTarget.y = 0; // Keep rotation horizontal
            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                return Quaternion.LookRotation(directionToTarget);
            }
        }
        
        // Default: use sitPoint's rotation
        return sitPoint != null ? sitPoint.rotation : Quaternion.identity;
    }
    
    public void Initialize(DiningTable table)
    {
        parentTable = table;
    }
    
    /// <summary>
    /// Reserve this seat for a customer
    /// </summary>
    public bool Reserve(FoodCustomer customer)
    {
        ClearMissingOccupant();
        if (!IsAvailable)
        {
            Debug.LogWarning("[DiningSeat] Seat is already occupied!");
            return false;
        }
        
        isOccupied = true;
        occupyingCustomer = customer;
        
        Debug.Log($"[DiningSeat] Seat reserved for customer");
        return true;
    }
    
    /// <summary>
    /// Release this seat when customer leaves
    /// </summary>
    public void Release()
    {
        isOccupied = false;
        occupyingCustomer = null;
        
        Debug.Log($"[DiningSeat] Seat released");
    }

    private void ClearMissingOccupant()
    {
            if (isOccupied && occupyingCustomer == null)
            {
                isOccupied = false;
                occupyingCustomer = null;
            }
        }
    
    private void OnDrawGizmos()
    {
        // Draw sit point (exact final position - can be above ground)
        if (sitPoint != null)
        {
            Gizmos.color = isOccupied ? Color.red : Color.green;
            Gizmos.DrawWireSphere(sitPoint.position, 0.2f);
            
            // Draw forward direction (where customer will face)
            Quaternion seatedRotation = GetSeatedRotation();
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(sitPoint.position, seatedRotation * Vector3.forward * 0.5f);
        }
        
        // Draw approach point (ground level for NavMesh)
        if (approachPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(approachPoint.position, 0.15f);
            
            if (sitPoint != null)
            {
                Gizmos.DrawLine(approachPoint.position, sitPoint.position);
            }
        }
        
        // Draw NavMesh target (if different from approach point)
        if (navMeshTarget != null && navMeshTarget != approachPoint)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(navMeshTarget.position, 0.12f);
            
            if (sitPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(navMeshTarget.position, sitPoint.position);
            }
        }
        
        // Draw facing target
        if (useFacingTarget && facingTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(facingTarget.position, 0.1f);
            if (sitPoint != null)
            {
                Gizmos.DrawLine(sitPoint.position, facingTarget.position);
            }
        }
    }
}
