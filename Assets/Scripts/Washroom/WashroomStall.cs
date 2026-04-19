using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public enum StallState
{
    Available,
    Occupied,
    NeedsToiletPaper
}

/// <summary>
/// Individual washroom stall with door animation, toilet paper tracking, customer queue, and customer handling.
/// Each stall has its own queue like PickupPoint in FoodStore.
/// </summary>
public class WashroomStall : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform door;
    [SerializeField] private Transform customerPosition;
    [SerializeField] private Transform toiletPaperDropPoint;
    
    [Header("Wait Point")]
    [Tooltip("Position where customer waits before entering the stall (outside the door)")]
    [SerializeField] private Transform waitPoint;
    
    [Header("Queue Settings")]
    [SerializeField] private int maxQueueSize = 5;
    [SerializeField] private float queueSpacing = 1.5f;
    [Tooltip("Direction the queue extends from wait point. If null, uses -transform.forward")]
    [SerializeField] private Transform queueDirection;
    
    [Header("Door Settings")]
    [SerializeField] private float doorOpenAngleEntering = -90f;
    [SerializeField] private float doorOpenAngleExiting = 90f;
    [SerializeField] private float doorClosedAngle = 0f;
    [SerializeField] private float doorAnimationDuration = 0.4f;
    [SerializeField] private Ease doorEase = Ease.OutQuad;
    
    [Header("Toilet Paper")]
    [Tooltip("How many customers can use the stall after toilet paper is provided")]
    [SerializeField] private int usesPerToiletPaper = 3;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("UI")]
    [SerializeField] private Canvas needToiletPaperUI;
    
    [Header("Usage Timing")]
    [SerializeField] private float minUsageTime = 3f;
    [SerializeField] private float maxUsageTime = 6f;
    
    private StallState currentState = StallState.Available;
    private int remainingUses = 0;
    private WashroomCustomer currentCustomer;
    private Washroom parentWashroom;
    private bool isPlayerInRange = false;
    private PlayerCarryController playerCarryController;
    
    // Queue management
    private List<WashroomCustomer> queue = new List<WashroomCustomer>();
    
    public StallState State => currentState;
    public bool IsAvailable => currentState == StallState.Available && remainingUses > 0 && currentCustomer == null;
    public bool NeedsToiletPaper => remainingUses <= 0;
    public Transform CustomerPosition => customerPosition;
    public Transform WaitPoint => waitPoint != null ? waitPoint : transform;
    public int RemainingUses => remainingUses;
    public int QueueCount => queue.Count;
    public bool HasQueueSpace => queue.Count < maxQueueSize;
    
    public event Action<WashroomStall> OnStallBecameAvailable;
    public event Action<WashroomStall> OnStallNeedsToiletPaper;
    
    private void Awake()
    {
        // Hide UI initially
        if (needToiletPaperUI != null)
        {
            needToiletPaperUI.enabled = false;
        }
        
        // Ensure door starts closed
        if (door != null)
        {
            door.localRotation = Quaternion.Euler(0, doorClosedAngle, 0);
        }
    }
    
    public void Initialize(Washroom washroom, int initialUses = -1)
    {
        parentWashroom = washroom;
        remainingUses = initialUses >= 0 ? initialUses : usesPerToiletPaper;
        UpdateToiletPaperUI();
        
        Debug.Log($"[WashroomStall] {gameObject.name} initialized with {remainingUses} uses");
    }
    
    #region Queue Management
    
    /// <summary>
    /// Add a customer to this stall's queue. Returns queue position or -1 if full.
    /// </summary>
    public int AddCustomerToQueue(WashroomCustomer customer)
    {
        if (!HasQueueSpace)
        {
            Debug.Log($"[WashroomStall] {gameObject.name} queue is full!");
            return -1;
        }
        
        queue.Add(customer);
        int position = queue.Count - 1;
        Debug.Log($"[WashroomStall] {gameObject.name} - Customer joined queue at position {position}");
        return position;
    }
    
    /// <summary>
    /// Remove a customer from the queue and update positions
    /// </summary>
    public void RemoveCustomerFromQueue(WashroomCustomer customer)
    {
        int index = queue.IndexOf(customer);
        if (index < 0) return;
        
        queue.RemoveAt(index);
        
        // Update positions of remaining customers
        for (int i = index; i < queue.Count; i++)
        {
            queue[i].OnQueuePositionChanged(i);
        }
        
        Debug.Log($"[WashroomStall] {gameObject.name} - Customer left queue. Remaining: {queue.Count}");
    }
    
    /// <summary>
    /// Get world position for a queue slot
    /// </summary>
    public Vector3 GetQueueWorldPosition(int queueIndex)
    {
        Vector3 startPos = waitPoint != null ? waitPoint.position : transform.position;
        
        // Position 0 is at the wait point
        if (queueIndex == 0)
        {
            return startPos;
        }
        
        // Calculate queue direction
        Vector3 direction;
        if (queueDirection != null)
        {
            direction = (queueDirection.position - startPos).normalized;
        }
        else
        {
            // Default: queue extends backward from the stall
            direction = -transform.forward;
        }
        
        return startPos + direction * (queueIndex * queueSpacing);
    }
    
    /// <summary>
    /// Get the front customer in queue
    /// </summary>
    public WashroomCustomer GetFrontCustomer()
    {
        return queue.Count > 0 ? queue[0] : null;
    }
    
    #endregion
    
    #region Customer Entry/Exit
    
    /// <summary>
    /// Called when front customer at wait point wants to enter the stall.
    /// Opens door and lets customer in.
    /// </summary>
    public bool TryEnterStall(WashroomCustomer customer)
    {
        // Must have toilet paper and not be occupied
        if (remainingUses <= 0 || currentCustomer != null)
        {
            Debug.Log($"[WashroomStall] Cannot enter - NeedsTP: {remainingUses <= 0}, Occupied: {currentCustomer != null}");
            return false;
        }
        
        // Must be the front customer in queue
        if (queue.Count == 0 || queue[0] != customer)
        {
            Debug.Log($"[WashroomStall] Customer is not at front of queue");
            return false;
        }
        
        currentCustomer = customer;
        currentState = StallState.Occupied;
        
        // Remove from queue
        RemoveCustomerFromQueue(customer);
        
        // Open door for entering (y = -90)
        OpenDoorForEntering(() =>
        {
            // Tell customer door is open, they can move inside
            customer.OnDoorOpenedForEntry(this);
        });
        
        return true;
    }
    
    /// <summary>
    /// Called when customer has physically entered the stall (reached customerPosition)
    /// </summary>
    public void OnCustomerInsideStall(WashroomCustomer customer)
    {
        if (currentCustomer != customer) return;
        
        // Close door behind them
        CloseDoor(() =>
        {
            // Start usage timer
            StartCoroutine(UsageCoroutine());
        });
    }
    
    private IEnumerator UsageCoroutine()
    {
        float usageTime = UnityEngine.Random.Range(minUsageTime, maxUsageTime);
        yield return new WaitForSeconds(usageTime);
        
        // Customer finished
        OnCustomerFinished();
    }
    
    private void OnCustomerFinished()
    {
        // Decrement uses
        remainingUses--;
        
        // Open door for exiting (y = 90)
        OpenDoorForExiting(() =>
        {
            if (currentCustomer != null)
            {
                currentCustomer.OnReadyToExit();
            }
        });
    }
    
    /// <summary>
    /// Called when customer has exited the stall
    /// </summary>
    public void OnCustomerExited()
    {
        currentCustomer = null;
        
        // Close door
        CloseDoor(() =>
        {
            // Check if needs toilet paper
            if (remainingUses <= 0)
            {
                currentState = StallState.NeedsToiletPaper;
                ShowNeedToiletPaperUI();
                OnStallNeedsToiletPaper?.Invoke(this);
            }
            else
            {
                currentState = StallState.Available;
                OnStallBecameAvailable?.Invoke(this);
                
                // Notify front customer in queue that stall is ready
                NotifyFrontCustomer();
            }
        });
    }
    
    /// <summary>
    /// Notify the front customer that they can enter
    /// </summary>
    private void NotifyFrontCustomer()
    {
        var frontCustomer = GetFrontCustomer();
        if (frontCustomer != null)
        {
            frontCustomer.OnStallReadyForEntry(this);
        }
    }
    
    #endregion
    
    /// <summary>
    /// Called when player delivers toilet paper
    /// </summary>
    public void RefillToiletPaper()
    {
        remainingUses = usesPerToiletPaper;
        HideNeedToiletPaperUI();
        
        if (currentState == StallState.NeedsToiletPaper)
        {
            currentState = StallState.Available;
            OnStallBecameAvailable?.Invoke(this);
            
            // Notify front customer that stall is now ready
            NotifyFrontCustomer();
        }
        
        Debug.Log($"[WashroomStall] Refilled toilet paper. Uses: {remainingUses}");
    }
    
    private void OpenDoorForEntering(Action onComplete = null)
    {
        if (door == null)
        {
            onComplete?.Invoke();
            return;
        }
        
        door.DOLocalRotate(new Vector3(0, doorOpenAngleEntering, 0), doorAnimationDuration)
            .SetEase(doorEase)
            .OnComplete(() => onComplete?.Invoke());
    }
    
    private void OpenDoorForExiting(Action onComplete = null)
    {
        if (door == null)
        {
            onComplete?.Invoke();
            return;
        }
        
        door.DOLocalRotate(new Vector3(0, doorOpenAngleExiting, 0), doorAnimationDuration)
            .SetEase(doorEase)
            .OnComplete(() => onComplete?.Invoke());
    }
    
    private void CloseDoor(Action onComplete = null)
    {
        if (door == null)
        {
            onComplete?.Invoke();
            return;
        }
        
        door.DOLocalRotate(new Vector3(0, doorClosedAngle, 0), doorAnimationDuration)
            .SetEase(doorEase)
            .OnComplete(() => onComplete?.Invoke());
    }
    
    private void ShowNeedToiletPaperUI()
    {
        if (needToiletPaperUI != null)
        {
            needToiletPaperUI.enabled = true;
            
            // Pop animation
            needToiletPaperUI.transform.localScale = Vector3.zero;
            needToiletPaperUI.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }
    }
    
    private void HideNeedToiletPaperUI()
    {
        if (needToiletPaperUI != null)
        {
            needToiletPaperUI.transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() => needToiletPaperUI.enabled = false);
        }
    }
    
    private void UpdateToiletPaperUI()
    {
        if (needToiletPaperUI != null)
        {
            needToiletPaperUI.enabled = remainingUses <= 0;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        
        playerCarryController = other.GetComponent<PlayerCarryController>();
        if (playerCarryController == null)
        {
            playerCarryController = other.GetComponentInParent<PlayerCarryController>();
        }
        
        if (playerCarryController != null)
        {
            isPlayerInRange = true;
            TryDeliverToiletPaper();
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (!isPlayerInRange) return;
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        
        TryDeliverToiletPaper();
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        
        isPlayerInRange = false;
        playerCarryController = null;
    }
    
    private void TryDeliverToiletPaper()
    {
        // Only accept toilet paper if stall needs it
        if (!NeedsToiletPaper) return;
        if (playerCarryController == null || !playerCarryController.HasToiletPaper()) return;
        
        // Take one toilet paper from player
        ToiletPaper paper = playerCarryController.TakeTopToiletPaper();
        if (paper == null) return;
        
        // Throw animation to drop point
        Vector3 targetPos = toiletPaperDropPoint != null 
            ? toiletPaperDropPoint.position 
            : transform.position + Vector3.up * 0.5f;
        
        paper.ThrowTo(targetPos, () =>
        {
            RefillToiletPaper();
        });
        
        Debug.Log("[WashroomStall] Player delivered toilet paper");
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw customer position (inside stall)
        if (customerPosition != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(customerPosition.position, 0.3f);
            Gizmos.DrawLine(transform.position, customerPosition.position);
        }
        
        // Draw wait point
        if (waitPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(waitPoint.position, 0.35f);
        }
        
        // Draw toilet paper drop point
        if (toiletPaperDropPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(toiletPaperDropPoint.position, 0.2f);
        }
        
        // Draw queue positions
        Gizmos.color = Color.yellow;
        for (int i = 0; i < maxQueueSize; i++)
        {
            Vector3 pos = GetQueueWorldPosition(i);
            Gizmos.DrawWireSphere(pos, 0.25f);
            
            if (i < maxQueueSize - 1)
            {
                Gizmos.DrawLine(pos, GetQueueWorldPosition(i + 1));
            }
        }
    }
}
