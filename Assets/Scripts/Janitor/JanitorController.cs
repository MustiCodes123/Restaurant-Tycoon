using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum JanitorState
{
    Idle,
    MovingToRack,
    CollectingToiletPaper,
    MovingToStall,
    DeliveringToiletPaper,
    MovingToIdleSpot
}

/// <summary>
/// AI controller for janitor characters. Monitors washroom stalls and delivers toilet paper when needed.
/// </summary>
public class JanitorController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float arrivalThreshold = 0.5f;
    
    [Header("Timing")]
    [SerializeField] private float searchInterval = 0.5f;
    [SerializeField] private float collectDelay = 0.5f;
    [SerializeField] private float deliverDelay = 0.5f;
    
    [Header("Animation")]
    [SerializeField] private Animator animator;
    
    [Header("Carry Position")]
    [Tooltip("Position where carried toilet paper is held")]
    [SerializeField] private Transform carryPoint;
    
    [Header("Idle Spots")]
    [SerializeField] private List<JanitorIdleSpot> availableIdleSpots = new List<JanitorIdleSpot>();
    
    [Header("Washroom References")]
    [SerializeField] private List<Washroom> assignedWashrooms = new List<Washroom>();
    
    private NavMeshAgent agent;
    private JanitorState currentState = JanitorState.Idle;
    private WashroomStall targetStall;
    private ToiletPaperRack targetRack;
    private JanitorIdleSpot assignedIdleSpot;
    private float searchTimer = 0f;
    private float actionTimer = 0f;
    private bool isMoving = false;
    private Vector3 currentDestination;
    private float movementStartTime = 0f;
    
    // Toilet paper carrying
    private ToiletPaper carriedToiletPaper;
    
    public JanitorState State => currentState;
    public bool IsCarryingToiletPaper => carriedToiletPaper != null;
    
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
        
        // Create carry point if not assigned
        if (carryPoint == null)
        {
            GameObject carryObj = new GameObject("CarryPoint");
            carryObj.transform.SetParent(transform);
            carryObj.transform.localPosition = new Vector3(0, 1f, 0.3f);
            carryPoint = carryObj.transform;
        }
    }
    
    private void Start()
    {
        // Register with manager
        if (JanitorManager.Instance != null)
        {
            JanitorManager.Instance.RegisterJanitor(this);
        }
        
        // Find all idle spots in scene if not assigned
        if (availableIdleSpots.Count == 0)
        {
            availableIdleSpots.AddRange(FindObjectsOfType<JanitorIdleSpot>());
        }
        
        // Find all washrooms if not assigned
        if (assignedWashrooms.Count == 0)
        {
            assignedWashrooms.AddRange(FindObjectsOfType<Washroom>());
        }
        
        // Subscribe to stall events
        foreach (var washroom in assignedWashrooms)
        {
            if (washroom != null)
            {
                washroom.OnStallNeedsToiletPaper += OnStallNeedsToiletPaper;
            }
        }
        
        // Start by going to nearest idle spot
        GoToNearestIdleSpot();
    }
    
    private void OnDestroy()
    {
        ReleaseIdleSpot();
        
        // Unsubscribe from washroom events
        foreach (var washroom in assignedWashrooms)
        {
            if (washroom != null)
            {
                washroom.OnStallNeedsToiletPaper -= OnStallNeedsToiletPaper;
            }
        }
        
        // Unregister from manager
        if (JanitorManager.Instance != null)
        {
            JanitorManager.Instance.UnregisterJanitor(this);
        }
    }
    
    private void Update()
    {
        switch (currentState)
        {
            case JanitorState.Idle:
                HandleIdleState();
                break;
                
            case JanitorState.MovingToRack:
                HandleMovingToRack();
                break;
                
            case JanitorState.CollectingToiletPaper:
                HandleCollectingToiletPaper();
                break;
                
            case JanitorState.MovingToStall:
                HandleMovingToStall();
                break;
                
            case JanitorState.DeliveringToiletPaper:
                HandleDeliveringToiletPaper();
                break;
                
            case JanitorState.MovingToIdleSpot:
                HandleMovingToIdleSpot();
                break;
        }
        
        UpdateMovementCheck();
    }
    
    private void HandleIdleState()
    {
        searchTimer += Time.deltaTime;
        
        if (searchTimer >= searchInterval)
        {
            searchTimer = 0f;
            
            // Search for stalls needing toilet paper
            WashroomStall needyStall = FindStallNeedingToiletPaper();
            if (needyStall != null)
            {
                Debug.Log($"[JanitorController] Found stall needing toilet paper: {needyStall.name}");
                StartToiletPaperDelivery(needyStall);
            }
            else
            {
                Debug.Log($"[JanitorController] No stalls need toilet paper. Assigned washrooms: {assignedWashrooms.Count}");
            }
        }
    }
    
    private void HandleMovingToRack()
    {
        // Check if target rack is still valid
        if (targetRack == null)
        {
            // Find another rack
            targetRack = FindNearestToiletPaperRack();
            if (targetRack != null)
            {
                MoveTo(targetRack.transform.position);
            }
            else
            {
                // No rack available, abort mission
                Debug.LogWarning("[JanitorController] No toilet paper rack available!");
                GoToNearestIdleSpot();
            }
            return;
        }
        
        // Check if arrived at rack
        if (HasReachedDestination())
        {
            StopMoving();
            StartCollectingToiletPaper();
        }
    }
    
    private void HandleCollectingToiletPaper()
    {
        actionTimer += Time.deltaTime;
        
        if (actionTimer >= collectDelay)
        {
            // Collect toilet paper from rack
            CollectToiletPaperFromRack();
            
            if (carriedToiletPaper != null && targetStall != null)
            {
                // Move to the stall's wait point (outside the door)
                MoveTo(targetStall.WaitPoint.position);
                currentState = JanitorState.MovingToStall;
                SetWalking(true);
            }
            else
            {
                // Failed to collect, try again or go idle
                Debug.LogWarning("[JanitorController] Failed to collect toilet paper");
                GoToNearestIdleSpot();
            }
        }
    }
    
    private void HandleMovingToStall()
    {
        // Check if target stall is still valid and needs toilet paper
        if (targetStall == null || !targetStall.NeedsToiletPaper)
        {
            // Stall no longer needs toilet paper, find another or go idle
            WashroomStall newStall = FindStallNeedingToiletPaper();
            if (newStall != null)
            {
                targetStall = newStall;
                MoveTo(targetStall.WaitPoint.position);
            }
            else
            {
                // No stalls need toilet paper, drop paper and go idle
                if (carriedToiletPaper != null)
                {
                    Destroy(carriedToiletPaper.gameObject);
                    carriedToiletPaper = null;
                }
                GoToNearestIdleSpot();
            }
            return;
        }
        
        // Check if arrived at stall
        if (HasReachedDestination())
        {
            StopMoving();
            StartDeliveringToiletPaper();
        }
    }
    
    private void HandleDeliveringToiletPaper()
    {
        actionTimer += Time.deltaTime;
        
        if (actionTimer >= deliverDelay)
        {
            // Deliver toilet paper to stall
            DeliverToiletPaperToStall();
            
            // Check for more stalls needing toilet paper
            WashroomStall nextStall = FindStallNeedingToiletPaper();
            if (nextStall != null)
            {
                StartToiletPaperDelivery(nextStall);
            }
            else
            {
                GoToNearestIdleSpot();
            }
        }
    }
    
    private void HandleMovingToIdleSpot()
    {
        if (HasReachedDestination())
        {
            StopMoving();
            currentState = JanitorState.Idle;
            SetWalking(false);
        }
        
        // While moving to idle, still check for stalls needing toilet paper
        searchTimer += Time.deltaTime;
        if (searchTimer >= searchInterval)
        {
            searchTimer = 0f;
            WashroomStall needyStall = FindStallNeedingToiletPaper();
            if (needyStall != null)
            {
                ReleaseIdleSpot();
                StartToiletPaperDelivery(needyStall);
            }
        }
    }
    
    /// <summary>
    /// Called when a stall needs toilet paper - can interrupt idle state
    /// </summary>
    private void OnStallNeedsToiletPaper(WashroomStall stall)
    {
        Debug.Log($"[JanitorController] OnStallNeedsToiletPaper event received for stall: {stall?.name}, currentState: {currentState}");
        
        // If idle or moving to idle, start delivery immediately
        if (currentState == JanitorState.Idle || currentState == JanitorState.MovingToIdleSpot)
        {
            ReleaseIdleSpot();
            StartToiletPaperDelivery(stall);
        }
        else
        {
            Debug.Log($"[JanitorController] Janitor is busy (state: {currentState}), cannot respond to event");
        }
    }
    
    private void StartToiletPaperDelivery(WashroomStall stall)
    {
        targetStall = stall;
        
        // First, go to toilet paper rack
        targetRack = FindNearestToiletPaperRack();
        if (targetRack == null)
        {
            Debug.LogWarning("[JanitorController] No toilet paper rack found!");
            GoToNearestIdleSpot();
            return;
        }
        
        ReleaseIdleSpot();
        MoveTo(targetRack.transform.position);
        currentState = JanitorState.MovingToRack;
        SetWalking(true);
        
        Debug.Log($"[JanitorController] Starting toilet paper delivery to {stall.name}");
    }
    
    private void StartCollectingToiletPaper()
    {
        currentState = JanitorState.CollectingToiletPaper;
        actionTimer = 0f;
        
        // Face the rack
        if (targetRack != null)
        {
            Vector3 lookDir = (targetRack.transform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
        
        SetWalking(false);
    }
    
    private void CollectToiletPaperFromRack()
    {
        if (targetRack == null) return;
        
        // Get toilet paper from rack
        ToiletPaper paper = targetRack.TakeToiletPaperForJanitor();
        
        if (paper != null)
        {
            carriedToiletPaper = paper;
            
            // Parent to carry point
            paper.transform.SetParent(carryPoint);
            paper.transform.localPosition = Vector3.zero;
            paper.transform.localRotation = Quaternion.identity;
            
            Debug.Log("[JanitorController] Collected toilet paper from rack");
        }
    }
    
    private void StartDeliveringToiletPaper()
    {
        currentState = JanitorState.DeliveringToiletPaper;
        actionTimer = 0f;
        
        // Face the stall
        if (targetStall != null)
        {
            Vector3 lookDir = (targetStall.transform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
        
        SetWalking(false);
    }
    
    private void DeliverToiletPaperToStall()
    {
        if (targetStall == null || carriedToiletPaper == null) return;
        
        // Refill the stall
        targetStall.RefillToiletPaper();
        
        // Destroy the carried toilet paper (it's been "placed")
        Destroy(carriedToiletPaper.gameObject);
        carriedToiletPaper = null;
        targetStall = null;
        
        Debug.Log("[JanitorController] Delivered toilet paper to stall");
    }
    
    private WashroomStall FindStallNeedingToiletPaper()
    {
        WashroomStall nearest = null;
        float nearestDistance = float.MaxValue;
        int totalStalls = 0;
        int stallsNeedingTP = 0;
        
        foreach (var washroom in assignedWashrooms)
        {
            if (washroom == null) continue;
            
            foreach (var stall in washroom.Stalls)
            {
                totalStalls++;
                if (stall != null && stall.NeedsToiletPaper)
                {
                    stallsNeedingTP++;
                    float distance = Vector3.Distance(transform.position, stall.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = stall;
                    }
                }
            }
        }
        
        if (stallsNeedingTP > 0)
        {
            Debug.Log($"[JanitorController] Found {stallsNeedingTP}/{totalStalls} stalls needing TP. Nearest: {nearest?.name}");
        }
        
        return nearest;
    }
    
    private ToiletPaperRack FindNearestToiletPaperRack()
    {
        ToiletPaperRack nearest = null;
        float nearestDistance = float.MaxValue;
        
        // Find all racks in scene
        ToiletPaperRack[] allRacks = FindObjectsOfType<ToiletPaperRack>();
        Debug.Log($"[JanitorController] Searching for racks. Found {allRacks.Length} total racks in scene");
        
        foreach (var rack in allRacks)
        {
            if (rack != null)
            {
                Debug.Log($"[JanitorController] Rack {rack.name} - Available: {rack.AvailableCount}");
                if (rack.AvailableCount > 0)
                {
                    float distance = Vector3.Distance(transform.position, rack.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = rack;
                    }
                }
            }
        }
        
        if (nearest != null)
        {
            Debug.Log($"[JanitorController] Selected nearest rack: {nearest.name} at distance {nearestDistance:F2}");
        }
        else
        {
            Debug.LogWarning($"[JanitorController] No racks with available toilet paper found!");
        }
        
        return nearest;
    }
    
    private void GoToNearestIdleSpot()
    {
        JanitorIdleSpot nearest = FindNearestAvailableIdleSpot();
        
        if (nearest != null)
        {
            // Reserve the spot
            if (nearest.Reserve(this))
            {
                assignedIdleSpot = nearest;
                MoveTo(nearest.Position);
                currentState = JanitorState.MovingToIdleSpot;
                SetWalking(true);
            }
            else
            {
                // Spot got taken, try again
                currentState = JanitorState.Idle;
                SetWalking(false);
            }
        }
        else
        {
            // No idle spots available, just stand here
            currentState = JanitorState.Idle;
            SetWalking(false);
        }
    }
    
    private JanitorIdleSpot FindNearestAvailableIdleSpot()
    {
        // Try using manager first for efficiency
        if (JanitorManager.Instance != null)
        {
            return JanitorManager.Instance.GetNearestAvailableIdleSpot(transform.position, this);
        }
        
        // Fallback: Refresh list in case new spots were added
        if (availableIdleSpots.Count == 0)
        {
            availableIdleSpots.AddRange(FindObjectsOfType<JanitorIdleSpot>());
        }
        
        JanitorIdleSpot nearest = null;
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
            animator.SetBool("IsWalking", isActuallyMoving && isMoving);
        }
    }
    
    private void SetWalking(bool walking)
    {
        if (animator != null)
        {
            animator.SetBool("IsWalking", walking);
        }
    }
    
    /// <summary>
    /// Initialize janitor with settings from unlock data
    /// </summary>
    public void Initialize(JanitorUnlockData data, List<JanitorIdleSpot> idleSpots)
    {
        if (data != null)
        {
            moveSpeed = data.MoveSpeed;
            
            if (agent != null)
            {
                agent.speed = moveSpeed;
            }
        }
        
        if (idleSpots != null && idleSpots.Count > 0)
        {
            availableIdleSpots = new List<JanitorIdleSpot>(idleSpots);
        }
        
        // Start by going to idle spot
        GoToNearestIdleSpot();
    }
    
    /// <summary>
    /// Assign washrooms for this janitor to monitor
    /// </summary>
    public void AssignWashrooms(List<Washroom> washrooms)
    {
        // Unsubscribe from old washrooms
        foreach (var washroom in assignedWashrooms)
        {
            if (washroom != null)
            {
                washroom.OnStallNeedsToiletPaper -= OnStallNeedsToiletPaper;
            }
        }
        
        assignedWashrooms = new List<Washroom>(washrooms);
        
        // Subscribe to new washrooms
        foreach (var washroom in assignedWashrooms)
        {
            if (washroom != null)
            {
                washroom.OnStallNeedsToiletPaper += OnStallNeedsToiletPaper;
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw line to target stall
        if (targetStall != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetStall.transform.position);
        }
        
        // Draw line to target rack
        if (targetRack != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetRack.transform.position);
        }
        
        if (assignedIdleSpot != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, assignedIdleSpot.Position);
        }
    }
}
