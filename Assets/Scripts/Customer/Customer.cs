using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;
using System.Collections.Generic;

public enum CustomerState
{
    MovingToObservatoryPoint,
    Observing,
    MovingToServiceSpot,
    WaitingAtStore,
    Leaving
}

public class Customer : MonoBehaviour, IQueueableCustomer
{
    [Header("Movement")]
    [SerializeField] private float arrivalThreshold = 0.3f;
    
    [Header("Animation")]
    [SerializeField] private Animator animator;
    
    [Header("Waiting UI")]
    [SerializeField] private Canvas waitingUICanvas;
    [SerializeField] private Transform waitingUI;
    [SerializeField] private float pulseMinScale = 0.8f;
    [SerializeField] private float pulseMaxScale = 1.2f;
    [SerializeField] private float pulseDuration = 0.5f;
    [SerializeField] private Ease pulseEase = Ease.InOutSine;
    
    [Header("Observing UI")]
    [SerializeField] private Transform observingUI;
    [SerializeField] private float observingPulseMinScale = 0.8f;
    [SerializeField] private float observingPulseMaxScale = 1.2f;
    [SerializeField] private float observingPulseDuration = 0.5f;
    [SerializeField] private Ease observingPulseEase = Ease.InOutSine;
    [SerializeField] private GameObject moneyPrefab;
    [SerializeField] private Vector3 moneyDropOffset = new Vector3(0, 0.5f, 0);
    
    [Header("Appearance")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private List<Material> materials;
    
    [Header("Bag")]
    [SerializeField] private GameObject bag;
    
    [Header("Observation")]
    [SerializeField] private float minObservationTime = 2f;
    [SerializeField] private float maxObservationTime = 4f;
    
    private NavMeshAgent agent;
    private CustomerState currentState;
    private Store targetStore;
    private Transform exitPoint;
    private ServiceSpot assignedStoreSpot;
    private int moneyPerDrop;
    private int queuePosition;
    private Vector3 currentDestination;
    private bool isMoving;
    private Tween waitingUITween;
    private Tween observingUITween;
    private bool hasBag = false;
    private float observationTimer;
    
    public CustomerState State => currentState;
    public int QueuePosition => queuePosition;
    
    // IQueueableCustomer implementation
    public bool IsWaitingAtStore => currentState == CustomerState.WaitingAtStore;
    public GameObject GameObject => gameObject;
    
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        HideWaitingUI();
        HideObservingUI();
        HideBag();
        
        // Assign random material to skinned mesh renderer
        if (skinnedMeshRenderer != null && materials != null && materials.Count > 0)
        {
            int randomIndex = Random.Range(0, materials.Count);
            skinnedMeshRenderer.material = materials[randomIndex];
        }
    }
    
    private void OnDestroy()
    {
        waitingUITween?.Kill();
        observingUITween?.Kill();
    }
    
    /// <summary>
    /// Initialize customer to go directly to a store's service spot queue
    /// </summary>
    public void Initialize(ServiceSpot spot, Transform exit)
    {
        if (spot == null)
        {
            Debug.LogError("[Customer] Cannot initialize - spot is null!");
            Destroy(gameObject);
            return;
        }
        
        assignedStoreSpot = spot;
        targetStore = spot.ParentStore;
        exitPoint = exit;
        moneyPerDrop = targetStore != null ? targetStore.MoneyPerCustomer : 10;
        
        // Check if store has observatory points
        if (targetStore != null && targetStore.ObservatoryPoints != null && targetStore.ObservatoryPoints.Count > 0)
        {
            // Move to a random observatory point first
            Transform randomObservatoryPoint = targetStore.ObservatoryPoints[Random.Range(0, targetStore.ObservatoryPoints.Count)];
            MoveTo(randomObservatoryPoint.position);
            currentState = CustomerState.MovingToObservatoryPoint;
        }
        else
        {
            // No observatory points, go directly to service spot
            JoinServiceSpotQueue();
        }
    }
    
    private void JoinServiceSpotQueue()
    {
        // Join the service spot's queue
        queuePosition = assignedStoreSpot.AddCustomerToQueue(this);
        
        if (queuePosition < 0)
        {
            Debug.LogError("[Customer] Failed to join queue! Leaving mall.");
            // Hide bag and leave without it since they didn't actually shop
            HideBag();
            StartLeavingWithoutBag();
            return;
        }
        
        // Move to queue position
        MoveTo(assignedStoreSpot.GetQueueWorldPosition(queuePosition));
        currentState = CustomerState.MovingToServiceSpot;
        
        // If joining at position 0 (first in queue), show waiting UI immediately
        // since we're already at or very close to the service spot
        if (queuePosition == 0)
        {
            ShowWaitingUI();
        }
    }
    
    private void Update()
    {
        // Handle observation state timer
        if (currentState == CustomerState.Observing)
        {
            observationTimer -= Time.deltaTime;
            if (observationTimer <= 0f)
            {
                OnFinishedObserving();
            }
            return;
        }
        
        if (!isMoving) return;
        
        // Use XZ distance only (ignore Y)
        Vector3 currentPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 targetPosXZ = new Vector3(currentDestination.x, 0, currentDestination.z);
        float distanceToTarget = Vector3.Distance(currentPosXZ, targetPosXZ);
        
        if (distanceToTarget <= arrivalThreshold)
        {
            StopMoving();
            OnReachedDestination();
        }
    }
    
    private void StopMoving()
    {
        isMoving = false;
        agent.ResetPath();
        SetWalking(false);
    }
    
    private void OnReachedDestination()
    {
        switch (currentState)
        {
            case CustomerState.MovingToObservatoryPoint:
                StartObserving();
                break;
                
            case CustomerState.MovingToServiceSpot:
                currentState = CustomerState.WaitingAtStore;
                ShowWaitingUI();
                break;
                
            case CustomerState.Leaving:
                Destroy(gameObject);
                break;
        }
    }
    
    private void StartObserving()
    {
        currentState = CustomerState.Observing;
        observationTimer = Random.Range(minObservationTime, maxObservationTime);
        SetWalking(false); // Play idle animation
        ShowObservingUI();
    }
    
    private void OnFinishedObserving()
    {
        HideObservingUI();
        // Show the bag after observing
        ShowBag();
        
        // Now join the service spot queue
        JoinServiceSpotQueue();
    }
    
    private void ShowBag()
    {
        hasBag = true;
        if (bag != null)
        {
            bag.SetActive(true);
        }
    }
    
    private void HideBag()
    {
        hasBag = false;
        if (bag != null)
        {
            bag.SetActive(false);
        }
    }
    
    public void ShowWaitingUI()
    {
        Debug.Log($"ShowWaitingUI called. waitingUI: {waitingUI}, waitingUICanvas: {waitingUICanvas}");
        
        if (waitingUICanvas != null)
        {
            waitingUICanvas.enabled = true;
            Debug.Log($"Canvas enabled: {waitingUICanvas.enabled}");
        }
        
        if (waitingUI != null)
        {
            waitingUI.gameObject.SetActive(true);
            waitingUI.localScale = Vector3.one * pulseMinScale;
            
            waitingUITween?.Kill();
            waitingUITween = waitingUI
                .DOScale(pulseMaxScale, pulseDuration)
                .SetEase(pulseEase)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }
    
    public void HideWaitingUI()
    {
        waitingUITween?.Kill();
        
        if (waitingUI != null)
            waitingUI.gameObject.SetActive(false);
        
        if (waitingUICanvas != null)
            waitingUICanvas.enabled = false;
    }
    
    public void ShowObservingUI()
    {
        if (waitingUICanvas != null)
        {
            waitingUICanvas.enabled = true;
        }
        
        if (observingUI != null)
        {
            observingUI.gameObject.SetActive(true);
            observingUI.localScale = Vector3.one * observingPulseMinScale;
            
            observingUITween?.Kill();
            observingUITween = observingUI
                .DOScale(observingPulseMaxScale, observingPulseDuration)
                .SetEase(observingPulseEase)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }
    
    public void HideObservingUI()
    {
        observingUITween?.Kill();
        
        if (observingUI != null)
            observingUI.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Called when this customer's queue position changes at the store
    /// </summary>
    public void UpdateStoreQueuePosition(int newPosition, ServiceSpot spot)
    {
        HideWaitingUI();
        queuePosition = newPosition;
        currentState = CustomerState.MovingToServiceSpot;
        MoveTo(spot.GetQueueWorldPosition(newPosition));
    }
    
    // IQueueableCustomer interface implementation
    public void OnQueuePositionChanged(int newPosition, ServiceSpot spot)
    {
        UpdateStoreQueuePosition(newPosition, spot);
    }
    
    public void OnServed()
    {
        OnServedAtStore();
    }
    
    public void OnServedAtStore()
    {
        HideWaitingUI();
        
        // Remove from service spot queue
        if (assignedStoreSpot != null)
        {
            assignedStoreSpot.RemoveCustomer(this);
            assignedStoreSpot = null;
        }
        
        // Drop money when served
        DropMoney();
        
        // Register with level manager
        if (LevelManager.Instance != null && targetStore != null)
        {
            LevelManager.Instance.RegisterCustomerServedAtStore(targetStore.StoreName);
        }
        
        // Make the store dirty after customer is served
        if (targetStore != null)
        {
            targetStore.OnCustomerServed();
        }
        
        StartLeaving();
    }
    
    private void StartLeaving()
    {
        currentState = CustomerState.Leaving;
        MoveTo(exitPoint.position);
    }
    
    /// <summary>
    /// Called when customer needs to leave without a bag (e.g., failed to join queue)
    /// </summary>
    private void StartLeavingWithoutBag()
    {
        currentState = CustomerState.Leaving;
        // Set destination without using MoveTo to avoid SetWalking which checks hasBag
        currentDestination = new Vector3(exitPoint.position.x, transform.position.y, exitPoint.position.z);
        agent.SetDestination(exitPoint.position);
        isMoving = true;
        // Force normal walking animation (without bag)
        if (animator != null)
        {
            animator.SetBool("IsWalking", true);
            animator.SetBool("IsWalkingWithBag", false);
        }
    }
    
    private void MoveTo(Vector3 destination)
    {
        currentDestination = new Vector3(destination.x, transform.position.y, destination.z);
        agent.SetDestination(destination);
        isMoving = true;
        SetWalking(true);
    }
    
    private void DropMoney()
    {
        if (moneyPrefab == null) return;
        
        Vector3 dropPosition = transform.position + moneyDropOffset;
        GameObject moneyDrop = Instantiate(moneyPrefab, dropPosition, Quaternion.identity);
        
        MoneyDrop moneyScript = moneyDrop.GetComponent<MoneyDrop>();
        if (moneyScript != null)
        {
            moneyScript.Initialize(moneyPerDrop);
        }
    }
    
    private void SetWalking(bool walking)
    {
        if (animator != null)
        {
            if (walking && hasBag)
            {
                // Walking with bag
                animator.SetBool("IsWalking", false);
                animator.SetBool("IsWalkingWithBag", true);
            }
            else if (walking)
            {
                // Normal walking without bag
                animator.SetBool("IsWalking", true);
                animator.SetBool("IsWalkingWithBag", false);
            }
            else
            {
                // Idle
                animator.SetBool("IsWalking", false);
                animator.SetBool("IsWalkingWithBag", false);
            }
        }
    }
}
