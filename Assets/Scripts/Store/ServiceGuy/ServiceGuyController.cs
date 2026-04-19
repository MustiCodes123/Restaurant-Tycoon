using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum ServiceGuyState
{
    Idle,
    MovingToServiceSpot,
    Serving,
    MovingToIdleSpot
}

/// <summary>
/// AI controller for service guy characters. Automatically serves customers at assigned store.
/// </summary>
public class ServiceGuyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float arrivalThreshold = 0.5f;
    
    [Header("Service")]
    [SerializeField] private float serviceDuration = 2f;
    [SerializeField] private float searchInterval = 0.3f;
    
    [Header("Animation")]
    [SerializeField] private Animator animator;
    
    [Header("Store Assignment")]
    [SerializeField] private Store assignedStore;
    
    [Header("Idle Spot")]
    [SerializeField] private ServiceGuyIdleSpot idleSpot;
    
    private NavMeshAgent agent;
    private ServiceGuyState currentState = ServiceGuyState.Idle;
    private ServiceSpot targetServiceSpot;
    private IQueueableCustomer targetCustomer;
    private float serviceTimer = 0f;
    private float searchTimer = 0f;
    private bool isMoving = false;
    private Vector3 currentDestination;
    private float initialDistanceToTarget = 0f;
    private float movementStartTime = 0f;
    
    public ServiceGuyState State => currentState;
    public Store AssignedStore => assignedStore;
    
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
        // Start by going to idle spot
        GoToIdleSpot();
    }
    
    private void Update()
    {
        switch (currentState)
        {
            case ServiceGuyState.Idle:
                HandleIdleState();
                break;
                
            case ServiceGuyState.MovingToServiceSpot:
                HandleMovingToServiceSpot();
                break;
                
            case ServiceGuyState.Serving:
                HandleServing();
                break;
                
            case ServiceGuyState.MovingToIdleSpot:
                HandleMovingToIdleSpot();
                break;
        }
        
        UpdateAnimation();
    }
    
    private void HandleIdleState()
    {
        searchTimer += Time.deltaTime;
        
        if (searchTimer >= searchInterval)
        {
            searchTimer = 0f;
            
            // Search for customers waiting at service spots
            ServiceSpot spotWithCustomer = FindServiceSpotWithWaitingCustomer();
            if (spotWithCustomer != null)
            {
                // Release idle spot before going to serve
                ReleaseIdleSpot();
                
                targetServiceSpot = spotWithCustomer;
                targetCustomer = spotWithCustomer.CurrentCustomer;
                MoveTo(spotWithCustomer.Position);
                currentState = ServiceGuyState.MovingToServiceSpot;
                SetWalking(true);
            }
        }
    }
    
    private void HandleMovingToServiceSpot()
    {
        // Check if target is still valid (customer still waiting)
        if (targetServiceSpot == null || targetCustomer == null || 
            !targetCustomer.IsWaitingAtStore)
        {
            // Target became invalid, find new one or go idle
            StopMoving();
            ServiceSpot newSpot = FindServiceSpotWithWaitingCustomer();
            if (newSpot != null)
            {
                targetServiceSpot = newSpot;
                targetCustomer = newSpot.CurrentCustomer;
                MoveTo(newSpot.Position);
            }
            else
            {
                GoToIdleSpot();
            }
            return;
        }
        
        // Check if arrived
        if (HasReachedDestination())
        {
            StopMoving();
            StartServing();
        }
    }
    
    private void HandleServing()
    {
        serviceTimer += Time.deltaTime;
        
        // Check if service completed
        if (serviceTimer >= serviceDuration)
        {
            FinishServing();
        }
    }
    
    private void HandleMovingToIdleSpot()
    {
        if (HasReachedDestination())
        {
            StopMoving();
            currentState = ServiceGuyState.Idle;
            SetWalking(false);
            
            // Face the idle spot's forward direction
            if (idleSpot != null)
            {
                transform.rotation = idleSpot.Rotation;
            }
        }
        
        // While moving to idle, still check for customers
        searchTimer += Time.deltaTime;
        if (searchTimer >= searchInterval)
        {
            searchTimer = 0f;
            ServiceSpot spotWithCustomer = FindServiceSpotWithWaitingCustomer();
            if (spotWithCustomer != null)
            {
                ReleaseIdleSpot();
                targetServiceSpot = spotWithCustomer;
                targetCustomer = spotWithCustomer.CurrentCustomer;
                MoveTo(spotWithCustomer.Position);
                currentState = ServiceGuyState.MovingToServiceSpot;
            }
        }
    }
    
    private void StartServing()
    {
        if (targetServiceSpot == null || targetCustomer == null) return;
        
        currentState = ServiceGuyState.Serving;
        serviceTimer = 0f;
        
        // Face the customer wait point
        if (targetServiceSpot.CustomerWaitPoint != null)
        {
            Vector3 lookDir = (targetServiceSpot.CustomerWaitPoint.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
        
        // Play serving animation
        SetServing(true);
        
        // Hide customer's waiting UI (using GameObject to access the component)
        if (targetCustomer != null && targetCustomer.GameObject != null)
        {
            // Try to get Customer or FoodCustomer to hide UI
            var customer = targetCustomer.GameObject.GetComponent<Customer>();
            if (customer != null)
            {
                customer.HideWaitingUI();
            }
            else
            {
                var foodCustomer = targetCustomer.GameObject.GetComponent<FoodCustomer>();
                if (foodCustomer != null)
                {
                    foodCustomer.HideWaitingUI();
                }
            }
        }
    }
    
    private void FinishServing()
    {
        // Stop serving animation
        SetServing(false);
        
        // Complete service on the customer
        if (targetCustomer != null && targetServiceSpot != null)
        {
            // Notify the customer that service is complete
            targetCustomer.OnServed();
        }
        
        targetServiceSpot = null;
        targetCustomer = null;
        serviceTimer = 0f;
        
        // Look for next customer or go to idle
        ServiceSpot nextSpot = FindServiceSpotWithWaitingCustomer();
        if (nextSpot != null)
        {
            targetServiceSpot = nextSpot;
            targetCustomer = nextSpot.CurrentCustomer;
            MoveTo(nextSpot.Position);
            currentState = ServiceGuyState.MovingToServiceSpot;
            SetWalking(true);
        }
        else
        {
            GoToIdleSpot();
        }
    }
    
    private ServiceSpot FindServiceSpotWithWaitingCustomer()
    {
        if (assignedStore == null)
        {
            Debug.LogWarning($"[ServiceGuy] {gameObject.name} has no assigned store!");
            return null;
        }
        
        // Use reflection or direct access to get service spots from store
        // For now, we'll find service spots that belong to our store
        ServiceSpot[] allSpots = FindObjectsOfType<ServiceSpot>();
        
        Debug.Log($"[ServiceGuy] Searching for customers. Found {allSpots.Length} spots. Assigned store: {assignedStore.StoreName}");
        
        foreach (var spot in allSpots)
        {
            // Only consider service spots belonging to our store
            if (spot.ParentStore != assignedStore)
            {
                Debug.Log($"[ServiceGuy] Spot {spot.name} skipped - ParentStore is {(spot.ParentStore != null ? spot.ParentStore.StoreName : "null")}, expected {assignedStore.StoreName}");
                continue;
            }
            
            // Check if there's a customer waiting at this spot
            if (spot.CurrentCustomer != null)
            {
                Debug.Log($"[ServiceGuy] Spot {spot.name} has customer. IsWaitingAtStore: {spot.CurrentCustomer.IsWaitingAtStore}");
                
                if (spot.CurrentCustomer.IsWaitingAtStore)
                {
                    Debug.Log($"[ServiceGuy] Found waiting customer at {spot.name}!");
                    return spot;
                }
            }
            else
            {
                Debug.Log($"[ServiceGuy] Spot {spot.name} has no customer");
            }
        }
        
        return null;
    }
    
    private void GoToIdleSpot()
    {
        if (idleSpot != null)
        {
            // Reserve the spot
            if (idleSpot.Reserve(this) || idleSpot.IsOwnedBy(this))
            {
                MoveTo(idleSpot.Position);
                currentState = ServiceGuyState.MovingToIdleSpot;
                SetWalking(true);
            }
            else
            {
                // Spot is taken, just stand here
                currentState = ServiceGuyState.Idle;
                SetWalking(false);
            }
        }
        else
        {
            // No idle spot assigned, just stand here
            currentState = ServiceGuyState.Idle;
            SetWalking(false);
        }
    }
    
    private void ReleaseIdleSpot()
    {
        if (idleSpot != null && idleSpot.IsOwnedBy(this))
        {
            idleSpot.Release();
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
    
    private void UpdateAnimation()
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
    
    private void SetServing(bool serving)
    {
        if (animator != null)
        {
            animator.SetBool("IsServing", serving);
        }
    }
    
    /// <summary>
    /// Initialize service guy with settings
    /// </summary>
    public void Initialize(ServiceGuyUnlockData data, Store store, ServiceGuyIdleSpot idleSpotRef)
    {
        if (data != null)
        {
            moveSpeed = data.MoveSpeed;
            serviceDuration = data.ServiceDuration;
            
            if (agent != null)
            {
                agent.speed = moveSpeed;
            }
        }
        
        assignedStore = store;
        idleSpot = idleSpotRef;
        
        // Start by going to idle spot
        GoToIdleSpot();
    }
    
    private void OnDestroy()
    {
        ReleaseIdleSpot();
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw line to target
        if (targetServiceSpot != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetServiceSpot.Position);
        }
        
        if (idleSpot != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, idleSpot.Position);
        }
    }
}
