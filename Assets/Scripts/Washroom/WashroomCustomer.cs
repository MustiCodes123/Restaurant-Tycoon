using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;
using System.Collections.Generic;

public enum WashroomCustomerState
{
    MovingToQueue,
    WaitingInQueue,
    WaitingAtWaitPoint,    // At front of queue, waiting for stall to be ready
    MovingIntoStall,       // Door opened, moving from wait point to inside stall
    UsingStall,
    ExitingStall,
    Leaving
}

/// <summary>
/// Customer AI for washroom usage. 
/// Joins a specific stall's queue, waits at wait point, enters when stall is ready, uses it, and leaves.
/// Each stall has its own queue now.
/// </summary>
public class WashroomCustomer : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float arrivalThreshold = 0.3f;
    
    [Header("Animation")]
    [SerializeField] private Animator animator;
    
    [Header("Appearance")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private List<Material> materials;
    
    private NavMeshAgent agent;
    private WashroomCustomerState currentState;
    private Washroom targetWashroom;
    private WashroomStall targetStall;
    private Transform exitPoint;
    private int queuePosition;
    private Vector3 currentDestination;
    private bool isMoving;
    
    public WashroomCustomerState State => currentState;
    public int QueuePosition => queuePosition;
    public WashroomStall TargetStall => targetStall;
    public GameObject GameObject => gameObject;
    
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
        // Assign random material
        if (skinnedMeshRenderer != null && materials != null && materials.Count > 0)
        {
            int randomIndex = Random.Range(0, materials.Count);
            skinnedMeshRenderer.material = materials[randomIndex];
        }
    }
    
    /// <summary>
    /// Initialize customer to go to a specific stall's queue
    /// </summary>
    public void Initialize(Washroom washroom, WashroomStall stall, Transform exit)
    {
        targetWashroom = washroom;
        targetStall = stall;
        exitPoint = exit;
        
        // Join the stall's queue
        queuePosition = stall.AddCustomerToQueue(this);
        
        if (queuePosition < 0)
        {
            Debug.LogWarning("[WashroomCustomer] Failed to join stall queue!");
            StartLeaving();
            return;
        }
        
        // Move to queue position
        MoveTo(stall.GetQueueWorldPosition(queuePosition));
        currentState = WashroomCustomerState.MovingToQueue;
        
        Debug.Log($"[WashroomCustomer] Initialized for stall {stall.gameObject.name}, queue position {queuePosition}");
    }
    
    private void Update()
    {
        if (!isMoving) return;
        
        Vector3 currentPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 targetPosXZ = new Vector3(currentDestination.x, 0, currentDestination.z);
        float distanceToTarget = Vector3.Distance(currentPosXZ, targetPosXZ);
        
        if (distanceToTarget <= arrivalThreshold)
        {
            StopMoving();
            OnReachedDestination();
        }
    }
    
    private void MoveTo(Vector3 destination)
    {
        currentDestination = destination;
        agent.SetDestination(destination);
        isMoving = true;
        SetWalking(true);
    }
    
    private void StopMoving()
    {
        isMoving = false;
        agent.ResetPath();
        SetWalking(false);
    }
    
    private void SetWalking(bool walking)
    {
        if (animator != null)
        {
            animator.SetBool("IsWalking", walking);
        }
    }
    
    private void OnReachedDestination()
    {
        switch (currentState)
        {
            case WashroomCustomerState.MovingToQueue:
                OnReachedQueuePosition();
                break;
                
            case WashroomCustomerState.MovingIntoStall:
                OnReachedInsideStall();
                break;
                
            case WashroomCustomerState.Leaving:
                Destroy(gameObject);
                break;
        }
    }
    
    private void OnReachedQueuePosition()
    {
        // Are we at the front (wait point)?
        if (queuePosition == 0)
        {
            currentState = WashroomCustomerState.WaitingAtWaitPoint;
            Debug.Log("[WashroomCustomer] At wait point, checking if stall is ready...");
            
            // Check if stall is available and has toilet paper
            TryEnterStall();
        }
        else
        {
            currentState = WashroomCustomerState.WaitingInQueue;
            Debug.Log($"[WashroomCustomer] Waiting in queue at position {queuePosition}");
        }
    }
    
    /// <summary>
    /// Called when queue position changes (someone left queue)
    /// </summary>
    public void OnQueuePositionChanged(int newPosition)
    {
        int oldPosition = queuePosition;
        queuePosition = newPosition;
        
        Debug.Log($"[WashroomCustomer] Queue position changed from {oldPosition} to {newPosition}");
        
        // Move to new position
        if (targetStall != null)
        {
            MoveTo(targetStall.GetQueueWorldPosition(queuePosition));
            currentState = WashroomCustomerState.MovingToQueue;
        }
    }
    
    /// <summary>
    /// Called by stall when it's ready for this customer to enter (stall available + has toilet paper)
    /// </summary>
    public void OnStallReadyForEntry(WashroomStall stall)
    {
        if (stall != targetStall) return;
        if (currentState != WashroomCustomerState.WaitingAtWaitPoint) return;
        
        TryEnterStall();
    }
    
    private void TryEnterStall()
    {
        if (targetStall == null) return;
        if (currentState != WashroomCustomerState.WaitingAtWaitPoint) return;
        
        // Try to enter the stall - this will open the door
        if (targetStall.TryEnterStall(this))
        {
            Debug.Log("[WashroomCustomer] Stall accepted entry, waiting for door to open...");
            // Door will open, then OnDoorOpenedForEntry will be called
        }
        else
        {
            Debug.Log("[WashroomCustomer] Stall not ready yet, waiting...");
        }
    }
    
    /// <summary>
    /// Called by stall when door has finished opening for entry
    /// </summary>
    public void OnDoorOpenedForEntry(WashroomStall stall)
    {
        if (stall != targetStall) return;
        
        Debug.Log("[WashroomCustomer] Door opened, moving into stall...");
        
        currentState = WashroomCustomerState.MovingIntoStall;
        
        // Move to inside the stall
        if (stall.CustomerPosition != null)
        {
            MoveTo(stall.CustomerPosition.position);
        }
    }
    
    private void OnReachedInsideStall()
    {
        Debug.Log("[WashroomCustomer] Reached inside stall position");
        
        currentState = WashroomCustomerState.UsingStall;
        
        // Face correct direction
        if (targetStall != null && targetStall.CustomerPosition != null)
        {
            transform.rotation = targetStall.CustomerPosition.rotation;
        }
        
        // Tell stall we're inside
        targetStall.OnCustomerInsideStall(this);
    }
    
    /// <summary>
    /// Called by stall when customer can exit (door opened for exit)
    /// </summary>
    public void OnReadyToExit()
    {
        Debug.Log("[WashroomCustomer] Ready to exit stall");
        currentState = WashroomCustomerState.ExitingStall;
        StartLeaving();
    }
    
    private void StartLeaving()
    {
        currentState = WashroomCustomerState.Leaving;
        
        // Notify stall we've exited
        if (targetStall != null)
        {
            targetStall.OnCustomerExited();
            targetStall = null;
        }
        
        // Move to exit
        if (exitPoint != null)
        {
            MoveTo(exitPoint.position);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void OnDestroy()
    {
        // Clean up if destroyed while in queue
        if (targetStall != null && 
            (currentState == WashroomCustomerState.WaitingInQueue || 
             currentState == WashroomCustomerState.MovingToQueue ||
             currentState == WashroomCustomerState.WaitingAtWaitPoint))
        {
            targetStall.RemoveCustomerFromQueue(this);
        }
    }
}
