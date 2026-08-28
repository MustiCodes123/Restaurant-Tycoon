using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using DG.Tweening;

public enum TableCleanerState
{
    Idle,
    MovingToGarbage,
    PickingUpGarbage,
    MovingToGarbageBin,
    DisposingGarbage,
    MovingToIdleSpot
}

/// <summary>
/// AI controller for table cleaner characters. Automatically finds and clears garbage from dining tables.
/// Similar to PlayerCarryController but for AI workers.
/// </summary>
public class TableCleanerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float arrivalThreshold = 0.5f;
    
    [Header("Carry Settings")]
    [Tooltip("How many trays the cleaner can carry at once")]
    [SerializeField] public int maxTrayCapacity = 1;
    
    [Header("Carry Points")]
    [Tooltip("Empty GameObjects where carried items will be placed. First is lowest, last is highest.")]
    [SerializeField] private List<Transform> carryPoints = new List<Transform>();
    
    [Header("Timing")]
    [SerializeField] private float searchInterval = 0.5f; // How often to search for garbage
    [SerializeField] private float pickupDuration = 0.5f; // Time to pick up garbage
    [SerializeField] private float disposeDuration = 0.5f; // Time to dispose garbage
    
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkingParam = "IsWalking";
    [SerializeField] private string liftIdleParam = "IsLiftIdle";
    [SerializeField] private string liftWalkParam = "IsLiftWalking";
    
    [Header("Pickup Animation")]
    [SerializeField] private float pickupJumpHeight = 0.3f;
    [SerializeField] private float pickupAnimDuration = 0.2f;
    
    [Header("Idle Spots")]
    [SerializeField] private List<TableCleanerIdleSpot> availableIdleSpots = new List<TableCleanerIdleSpot>();
    
    [Header("Dining Areas")]
    [SerializeField] private List<DiningArea> diningAreas = new List<DiningArea>();
    
    [Header("Garbage Bins")]
    [SerializeField] private List<GarbageBin> garbageBins = new List<GarbageBin>();
    
    private NavMeshAgent agent;
    private TableCleanerState currentState = TableCleanerState.Idle;
    private Garbage targetGarbage;
    private GarbageBin targetGarbageBin;
    private TableCleanerIdleSpot assignedIdleSpot;
    private float searchTimer = 0f;
    private float actionTimer = 0f;
    private bool isMoving = false;
    private Vector3 currentDestination;
    private float initialDistanceToTarget = 0f;
    private float movementStartTime = 0f;
    
    // Carried garbage
    private List<Garbage> carriedGarbage = new List<Garbage>();
    
    public TableCleanerState State => currentState;
    public int CarriedCount => carriedGarbage.Count;
    public bool IsCarryingGarbage => carriedGarbage.Count > 0;
    public bool CanCarryMore => carriedGarbage.Count < maxTrayCapacity && carriedGarbage.Count < carryPoints.Count;
    
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = 0.3f;
        }
        
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }
    
    private void Start()
    {
        // Register with manager
        if (TableCleanerManager.Instance != null)
        {
            TableCleanerManager.Instance.RegisterCleaner(this);
        }
        
        // Find all idle spots in scene if not assigned
        if (availableIdleSpots.Count == 0)
        {
            availableIdleSpots.AddRange(FindObjectsOfType<TableCleanerIdleSpot>());
        }
        
        // Find all dining areas if not assigned
        if (diningAreas.Count == 0)
        {
            diningAreas.AddRange(FindObjectsOfType<DiningArea>());
        }
        
        // Find all garbage bins if not assigned
        if (garbageBins.Count == 0)
        {
            garbageBins.AddRange(FindObjectsOfType<GarbageBin>());
        }
        
        // Start by going to nearest idle spot
        GoToNearestIdleSpot();
    }
    
    private void Update()
    {
        switch (currentState)
        {
            case TableCleanerState.Idle:
                HandleIdleState();
                break;
                
            case TableCleanerState.MovingToGarbage:
                HandleMovingToGarbage();
                break;
                
            case TableCleanerState.PickingUpGarbage:
                HandlePickingUpGarbage();
                break;
                
            case TableCleanerState.MovingToGarbageBin:
                HandleMovingToGarbageBin();
                break;
                
            case TableCleanerState.DisposingGarbage:
                HandleDisposingGarbage();
                break;
                
            case TableCleanerState.MovingToIdleSpot:
                HandleMovingToIdleSpot();
                break;
        }
        
        UpdateMovementCheck();
        UpdateAnimation();
    }
    
    private void HandleIdleState()
    {
        searchTimer += Time.deltaTime;
        
        if (searchTimer >= searchInterval)
        {
            searchTimer = 0f;
            
            // Search for garbage on tables
            Garbage garbage = FindNearestGarbage();
            if (garbage != null)
            {
                // Release idle spot before going to collect
                ReleaseIdleSpot();
                
                targetGarbage = garbage;
                MoveTo(garbage.transform.position);
                currentState = TableCleanerState.MovingToGarbage;
                SetWalking(true);
                
                Debug.Log($"[TableCleaner] Found garbage, moving to collect");
            }
        }
    }
    
    private void HandleMovingToGarbage()
    {
        // Check if target is still valid
        if (targetGarbage == null || targetGarbage.IsPickedUp || !targetGarbage.gameObject.activeInHierarchy)
        {
            // Target became invalid, find new one or decide next action
            StopMoving();
            targetGarbage = null;
            
            // If carrying garbage, go to bin
            if (IsCarryingGarbage)
            {
                GoToNearestGarbageBin();
            }
            else
            {
                // Find new garbage or go idle
                Garbage newGarbage = FindNearestGarbage();
                if (newGarbage != null)
                {
                    targetGarbage = newGarbage;
                    MoveTo(newGarbage.transform.position);
                }
                else
                {
                    GoToNearestIdleSpot();
                }
            }
            return;
        }
        
        // Check if arrived
        if (HasReachedDestination())
        {
            StopMoving();
            StartPickingUpGarbage();
        }
    }
    
    private void StartPickingUpGarbage()
    {
        if (targetGarbage == null) return;
        
        currentState = TableCleanerState.PickingUpGarbage;
        actionTimer = 0f;
        
        // Face the garbage
        Vector3 lookDir = (targetGarbage.transform.position - transform.position).normalized;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }
        
        Debug.Log($"[TableCleaner] Starting to pick up garbage");
    }
    
    private void HandlePickingUpGarbage()
    {
        actionTimer += Time.deltaTime;
        
        if (actionTimer >= pickupDuration)
        {
            // Actually pick up the garbage
            PickupGarbage(targetGarbage);
            targetGarbage = null;
            
            // Decide what to do next
            if (CanCarryMore)
            {
                // Look for more garbage nearby
                Garbage nextGarbage = FindNearestGarbage();
                if (nextGarbage != null)
                {
                    targetGarbage = nextGarbage;
                    MoveTo(nextGarbage.transform.position);
                    currentState = TableCleanerState.MovingToGarbage;
                    SetWalking(true);
                }
                else
                {
                    // No more garbage, go to bin
                    GoToNearestGarbageBin();
                }
            }
            else
            {
                // At capacity, go to bin
                GoToNearestGarbageBin();
            }
        }
    }
    
    private void PickupGarbage(Garbage garbage)
    {
        if (garbage == null || !CanCarryMore) return;
        
        // Get the next available carry point
        int slotIndex = carriedGarbage.Count;
        if (slotIndex >= carryPoints.Count)
        {
            Debug.LogWarning("[TableCleaner] Not enough carry points defined");
            return;
        }
        
        Transform carryPoint = carryPoints[slotIndex];
        
        // Add to list
        carriedGarbage.Add(garbage);
        
        garbage.MarkPickedUp();
        
        // Parent and animate to carry point
        garbage.transform.SetParent(carryPoint);
        
        // Disable the collider so player can't pick it up
        Collider garbageCollider = garbage.GetComponent<Collider>();
        if (garbageCollider != null)
        {
            garbageCollider.enabled = false;
        }
        
        // Jump animation to carry point
        Sequence pickupSequence = DOTween.Sequence();
        pickupSequence.Append(garbage.transform.DOLocalJump(Vector3.zero, pickupJumpHeight, 1, pickupAnimDuration));
        pickupSequence.Join(garbage.transform.DOLocalRotate(Vector3.zero, pickupAnimDuration));
        
        // Play pickup sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.ItemPickup);
        }
        
        Debug.Log($"[TableCleaner] Picked up garbage. Now carrying {carriedGarbage.Count}");
    }
    
    private void GoToNearestGarbageBin()
    {
        GarbageBin nearest = FindNearestGarbageBin();
        
        if (nearest != null)
        {
            targetGarbageBin = nearest;
            MoveTo(nearest.transform.position);
            currentState = TableCleanerState.MovingToGarbageBin;
            SetWalking(true);
            
            Debug.Log($"[TableCleaner] Moving to garbage bin to dispose {carriedGarbage.Count} items");
        }
        else
        {
            Debug.LogWarning("[TableCleaner] No garbage bin found!");
            GoToNearestIdleSpot();
        }
    }
    
    private void HandleMovingToGarbageBin()
    {
        if (HasReachedDestination())
        {
            StopMoving();
            StartDisposingGarbage();
        }
    }
    
    private void StartDisposingGarbage()
    {
        currentState = TableCleanerState.DisposingGarbage;
        actionTimer = 0f;
        
        // Face the bin
        if (targetGarbageBin != null)
        {
            Vector3 lookDir = (targetGarbageBin.transform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
        
        Debug.Log($"[TableCleaner] Starting to dispose garbage");
    }
    
    private void HandleDisposingGarbage()
    {
        actionTimer += Time.deltaTime;
        
        if (actionTimer >= disposeDuration)
        {
            // Dispose all carried garbage
            DisposeAllGarbage();
            targetGarbageBin = null;
            
            // Look for more garbage or go idle
            Garbage nextGarbage = FindNearestGarbage();
            if (nextGarbage != null)
            {
                targetGarbage = nextGarbage;
                MoveTo(nextGarbage.transform.position);
                currentState = TableCleanerState.MovingToGarbage;
                SetWalking(true);
            }
            else
            {
                GoToNearestIdleSpot();
            }
        }
    }
    
    private void DisposeAllGarbage()
    {
        int count = carriedGarbage.Count;
        
        // Dispose each garbage
        foreach (var garbage in carriedGarbage)
        {
            if (garbage != null)
            {
                garbage.Dispose();
            }
        }
        
        carriedGarbage.Clear();
        
        // Play garbage drop sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.GarbageDrop);
        }
        
        Debug.Log($"[TableCleaner] Disposed {count} garbage items");
    }
    
    private void HandleMovingToIdleSpot()
    {
        if (HasReachedDestination())
        {
            StopMoving();
            currentState = TableCleanerState.Idle;
            SetWalking(false);
        }
        
        // While moving to idle, still check for garbage
        searchTimer += Time.deltaTime;
        if (searchTimer >= searchInterval)
        {
            searchTimer = 0f;
            Garbage garbage = FindNearestGarbage();
            if (garbage != null)
            {
                ReleaseIdleSpot();
                targetGarbage = garbage;
                MoveTo(garbage.transform.position);
                currentState = TableCleanerState.MovingToGarbage;
            }
        }
    }
    
    private Garbage FindNearestGarbage()
    {
        Garbage nearest = null;
        float nearestDistance = float.MaxValue;
        
        // Search through all dining areas
        foreach (var diningArea in diningAreas)
        {
            if (diningArea == null) continue;
            
            foreach (var table in diningArea.Tables)
            {
                if (table == null || !table.HasGarbage) continue;
                
                // Get the garbage component from the table
                // We need to find the garbage object - it should be at the garbage spawn point
                Transform garbageSpawnPoint = table.GarbageSpawnPoint;
                if (garbageSpawnPoint == null) continue;
                
                // Find garbage in children or nearby
                Garbage garbage = garbageSpawnPoint.GetComponentInChildren<Garbage>();
                if (garbage == null)
                {
                    // Try to find nearby
                    Collider[] nearby = Physics.OverlapSphere(garbageSpawnPoint.position, 0.5f);
                    foreach (var col in nearby)
                    {
                        garbage = col.GetComponent<Garbage>();
                        if (garbage != null && !garbage.IsPickedUp) break;
                    }
                }
                
                if (garbage != null && !garbage.IsPickedUp && garbage.gameObject.activeInHierarchy)
                {
                    float distance = Vector3.Distance(transform.position, garbage.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = garbage;
                    }
                }
            }
        }
        
        // Also search globally using FindObjectsOfType as fallback
        if (nearest == null)
        {
            Garbage[] allGarbage = FindObjectsOfType<Garbage>();
            foreach (var garbage in allGarbage)
            {
                if (garbage != null && !garbage.IsPickedUp && garbage.gameObject.activeInHierarchy)
                {
                    float distance = Vector3.Distance(transform.position, garbage.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = garbage;
                    }
                }
            }
        }
        
        return nearest;
    }
    
    private GarbageBin FindNearestGarbageBin()
    {
        GarbageBin nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var bin in garbageBins)
        {
            if (bin == null) continue;
            
            float distance = Vector3.Distance(transform.position, bin.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = bin;
            }
        }
        
        // Fallback to finding any garbage bin
        if (nearest == null)
        {
            GarbageBin[] allBins = FindObjectsOfType<GarbageBin>();
            foreach (var bin in allBins)
            {
                float distance = Vector3.Distance(transform.position, bin.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = bin;
                }
            }
        }
        
        return nearest;
    }
    
    private void GoToNearestIdleSpot()
    {
        TableCleanerIdleSpot nearest = FindNearestAvailableIdleSpot();
        
        if (nearest != null)
        {
            // Reserve the spot
            if (nearest.Reserve(this))
            {
                assignedIdleSpot = nearest;
                MoveTo(nearest.Position);
                currentState = TableCleanerState.MovingToIdleSpot;
                SetWalking(true);
            }
            else
            {
                // Spot got taken, try again
                currentState = TableCleanerState.Idle;
                SetWalking(false);
            }
        }
        else
        {
            // No idle spots available, just stand here
            currentState = TableCleanerState.Idle;
            SetWalking(false);
        }
    }
    
    private TableCleanerIdleSpot FindNearestAvailableIdleSpot()
    {
        // Try using manager first for efficiency
        if (TableCleanerManager.Instance != null)
        {
            return TableCleanerManager.Instance.GetNearestAvailableIdleSpot(transform.position, this);
        }
        
        // Fallback: Refresh list in case new spots were added
        if (availableIdleSpots.Count == 0)
        {
            availableIdleSpots.AddRange(FindObjectsOfType<TableCleanerIdleSpot>());
        }
        
        TableCleanerIdleSpot nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var spot in availableIdleSpots)
        {
            if (spot == null) continue;
            
            // Check if spot is not occupied, or if it's our assigned spot
            if (!spot.IsOccupied || spot.IsOwnedBy(this))
            {
                float distance = Vector3.Distance(transform.position, spot.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = spot;
                }
            }
        }
        
        return nearest;
    }
    
    private void ReleaseIdleSpot()
    {
        if (assignedIdleSpot != null)
        {
            assignedIdleSpot.Release();
            assignedIdleSpot = null;
        }
    }
    
    private void MoveTo(Vector3 destination)
    {
        if (agent == null) return;
        
        currentDestination = destination;
        initialDistanceToTarget = Vector3.Distance(transform.position, destination);
        movementStartTime = Time.time;
        agent.SetDestination(destination);
        isMoving = true;
    }
    
    private void StopMoving()
    {
        if (agent != null)
        {
            agent.ResetPath();
        }
        isMoving = false;
        initialDistanceToTarget = 0f;
        SetWalking(false);
    }
    
    private bool HasReachedDestination()
    {
        if (!isMoving) return false;
        if (agent == null) return false;
        
        // Must wait for path to be calculated
        if (agent.pathPending) return false;
        
        // Minimum time before checking arrival
        float minTravelTime = 0.5f;
        if (Time.time - movementStartTime < minTravelTime) return false;
        
        // Simple distance check
        float currentDistance = Vector3.Distance(transform.position, currentDestination);
        
        if (currentDistance <= arrivalThreshold)
        {
            return true;
        }
        
        // Also check if agent has stopped moving
        if (agent.hasPath && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            return true;
        }
        
        return false;
    }
    
    private void UpdateMovementCheck()
    {
        // Update animation based on actual agent velocity
        if (agent != null && animator != null)
        {
            bool isActuallyMoving = agent.velocity.sqrMagnitude > 0.01f;
            
            if (IsCarryingGarbage)
            {
                // Use lift walk animation
                animator.SetBool(walkingParam, false);
                animator.SetBool(liftWalkParam, isActuallyMoving && isMoving);
                animator.SetBool(liftIdleParam, !isActuallyMoving || !isMoving);
            }
            else
            {
                // Use normal walk animation
                animator.SetBool(walkingParam, isActuallyMoving && isMoving);
                animator.SetBool(liftWalkParam, false);
                animator.SetBool(liftIdleParam, false);
            }
        }
    }
    
    private void UpdateAnimation()
    {
        if (animator == null) return;
        
        // Additional animation updates if needed
    }
    
    private void SetWalking(bool walking)
    {
        if (animator != null)
        {
            if (IsCarryingGarbage)
            {
                animator.SetBool(liftWalkParam, walking);
                animator.SetBool(liftIdleParam, !walking);
                animator.SetBool(walkingParam, false);
            }
            else
            {
                animator.SetBool(walkingParam, walking);
                animator.SetBool(liftWalkParam, false);
                animator.SetBool(liftIdleParam, false);
            }
        }
    }
    
    /// <summary>
    /// Initialize cleaner with settings from unlock data
    /// </summary>
    public void Initialize(TableCleanerUnlockData data, List<TableCleanerIdleSpot> idleSpots, 
                          List<DiningArea> areas, List<GarbageBin> bins)
    {
        if (data != null)
        {
            moveSpeed = data.MoveSpeed;
            maxTrayCapacity = data.MaxTrayCapacity;
            
            if (agent != null)
            {
                agent.speed = moveSpeed;
            }
        }
        
        if (idleSpots != null && idleSpots.Count > 0)
        {
            availableIdleSpots = new List<TableCleanerIdleSpot>(idleSpots);
        }
        
        if (areas != null && areas.Count > 0)
        {
            diningAreas = new List<DiningArea>(areas);
        }
        
        if (bins != null && bins.Count > 0)
        {
            garbageBins = new List<GarbageBin>(bins);
        }
        
        // Start by going to idle spot
        GoToNearestIdleSpot();
    }
    
    private void OnDestroy()
    {
        ReleaseIdleSpot();
        
        // Unregister from manager
        if (TableCleanerManager.Instance != null)
        {
            TableCleanerManager.Instance.UnregisterCleaner(this);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw line to target garbage
        if (targetGarbage != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetGarbage.transform.position);
        }
        
        // Draw line to target bin
        if (targetGarbageBin != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetGarbageBin.transform.position);
        }
        
        if (assignedIdleSpot != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, assignedIdleSpot.Position);
        }
    }
}
