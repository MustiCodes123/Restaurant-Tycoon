using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// Customer behavior for technology stores.
/// Flow: Spawn → (Optional Observatory) → ItemContainer → Wait for service → Get item (bag) → Cashier → Pay → Exit
/// </summary>
public class TechCustomer : MonoBehaviour, IQueueableCustomer
{
    public enum TechCustomerState
    {
        // Initial states
        MovingToObservatoryPoint,
        Observing,
        
        // Item container phase
        MovingToItemContainer,
        WaitingAtItemContainer,
        ReceivingItem,
        
        // Cashier phase  
        MovingToCashier,
        WaitingAtCashier,
        
        // Exit
        Leaving,
        
        // Error state
        NoContainerAvailable
    }
    
    [Header("Movement")]
    [SerializeField] private float arrivalThreshold = 0.5f;
    [SerializeField] private float stoppingDistance = 0.3f;
    
    [Header("Animation")]
    [SerializeField] private Animator animator;
    
    [Header("Waiting UI")]
    [SerializeField] private Canvas waitingUICanvas;
    [SerializeField] private Transform waitingUI;
    [SerializeField] private float pulseMinScale = 0.8f;
    [SerializeField] private float pulseMaxScale = 1.2f;
    [SerializeField] private float pulseDuration = 0.5f;
    [SerializeField] private Ease pulseEase = Ease.InOutSine;
    
    [Header("No Container UI")]
    [SerializeField] private GameObject noContainerUI;
    [SerializeField] private float noContainerDisplayDuration = 2f;
    
    [Header("Observing UI")]
    [SerializeField] private Transform observingUI;
    
    [Header("Money")]
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
    
    // Components
    private NavMeshAgent agent;
    private TechCustomerState currentState;
    
    // References
    private TechStore targetTechStore;
    private TechItemContainer assignedItemContainer;
    private ServiceSpot assignedServiceSpot;
    private Transform exitPoint;
    private TechCustomerSpawner spawner;
    
    // State variables
    private int moneyPerDrop;
    private int queuePosition;
    private int containerQueuePosition;
    private Vector3 currentDestination;
    private bool isMoving;
    private float observationTimer;
    private bool hasBag = false;
    private bool useObservatoryPhase = true;
    
    // Tweens
    private Tween waitingUITween;
    private Tween observingUITween;
    
    public TechCustomerState State => currentState;
    public int QueuePosition => queuePosition;
    public int ContainerQueuePosition => containerQueuePosition;
    public TechStore TargetTechStore => targetTechStore;
    public TechItemContainer AssignedItemContainer => assignedItemContainer;
    
    // IQueueableCustomer implementation
    public bool IsWaitingAtStore => currentState == TechCustomerState.WaitingAtCashier;
    public GameObject GameObject => gameObject;
    
    /// <summary>
    /// Set the spawner reference for tracking customer count
    /// </summary>
    public void SetSpawner(TechCustomerSpawner customerSpawner)
    {
        spawner = customerSpawner;
    }
    
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        HideWaitingUI();
        HideObservingUI();
        HideNoContainerUI();
        HideBag();
        
        // Assign random material
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
    /// Initialize tech customer for a tech store
    /// </summary>
    public void Initialize(TechItemContainer itemContainer, TechStore techStore, Transform exit, bool enableObservatoryPhase)
    {
        if (itemContainer == null || techStore == null)
        {
            Debug.LogError("[TechCustomer] Cannot initialize - missing references!");
            Destroy(gameObject);
            return;
        }
        
        assignedItemContainer = itemContainer;
        targetTechStore = techStore;
        exitPoint = exit;
        useObservatoryPhase = enableObservatoryPhase;
        moneyPerDrop = techStore.BaseStore != null ? techStore.BaseStore.MoneyPerCustomer : 10;
        
        // Check if store has observatory points and observatory phase is enabled
        if (useObservatoryPhase && 
            techStore.BaseStore != null && 
            techStore.BaseStore.ObservatoryPoints != null && 
            techStore.BaseStore.ObservatoryPoints.Count > 0)
        {
            Transform randomObservatoryPoint = techStore.BaseStore.ObservatoryPoints[
                Random.Range(0, techStore.BaseStore.ObservatoryPoints.Count)];
            MoveTo(randomObservatoryPoint.position);
            currentState = TechCustomerState.MovingToObservatoryPoint;
        }
        else
        {
            // Skip observatory, go directly to item container
            JoinItemContainerQueue();
        }
    }
    
    private void Update()
    {
        switch (currentState)
        {
            case TechCustomerState.Observing:
                observationTimer -= Time.deltaTime;
                if (observationTimer <= 0f)
                {
                    OnFinishedObserving();
                }
                break;
                
            case TechCustomerState.NoContainerAvailable:
                // Don't process movement when no container available
                return;
        }
        
        // Handle movement arrival
        if (isMoving && agent != null)
        {
            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= arrivalThreshold)
                {
                    if (agent.velocity.sqrMagnitude < 0.01f || !agent.hasPath)
                    {
                        StopMoving();
                        OnReachedDestination();
                    }
                }
            }
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
            case TechCustomerState.MovingToObservatoryPoint:
                StartObserving();
                break;
                
            case TechCustomerState.MovingToItemContainer:
                OnArrivedAtItemContainer();
                break;
                
            case TechCustomerState.MovingToCashier:
                currentState = TechCustomerState.WaitingAtCashier;
                ShowWaitingUI();
                break;
                
            case TechCustomerState.Leaving:
                OnExitedStore();
                break;
        }
    }
    
    /// <summary>
    /// Called when customer reaches exit point
    /// </summary>
    private void OnExitedStore()
    {
        if (spawner != null)
        {
            spawner.OnCustomerExited();
        }
        
        Debug.Log("[TechCustomer] Exited store");
        Destroy(gameObject);
    }
    
    #region Observatory Phase
    
    private void StartObserving()
    {
        currentState = TechCustomerState.Observing;
        observationTimer = Random.Range(minObservationTime, maxObservationTime);
        SetWalking(false);
        ShowObservingUI();
    }
    
    private void OnFinishedObserving()
    {
        HideObservingUI();
        JoinItemContainerQueue();
    }
    
    #endregion
    
    #region Item Container Phase
    
    private void JoinItemContainerQueue()
    {
        if (assignedItemContainer == null)
        {
            Debug.LogError("[TechCustomer] No item container assigned!");
            OnNoContainerAvailable();
            return;
        }
        
        containerQueuePosition = assignedItemContainer.AddCustomerToQueue(this);
        
        if (containerQueuePosition < 0)
        {
            Debug.LogError("[TechCustomer] Failed to join item container queue!");
            OnNoContainerAvailable();
            return;
        }
        
        MoveTo(assignedItemContainer.GetQueueWorldPosition(containerQueuePosition));
        currentState = TechCustomerState.MovingToItemContainer;
        
        Debug.Log($"[TechCustomer] Joined item container queue at position {containerQueuePosition}");
    }
    
    private void OnArrivedAtItemContainer()
    {
        currentState = TechCustomerState.WaitingAtItemContainer;
        
        // Only show waiting UI and notify container if we're at the front
        if (containerQueuePosition == 0)
        {
            ShowWaitingUI();
            assignedItemContainer?.OnCustomerArrivedAtFront(this);
        }
        
        Debug.Log($"[TechCustomer] Arrived at item container (position {containerQueuePosition}), waiting for service...");
    }
    
    /// <summary>
    /// Called when position in item container queue changes
    /// </summary>
    public void OnContainerQueuePositionChanged(int newPosition)
    {
        containerQueuePosition = newPosition;
        
        // Move to new queue position
        Vector3 queuePos = assignedItemContainer.GetQueueWorldPosition(newPosition);
        MoveTo(queuePos);
        
        Debug.Log($"[TechCustomer] Container queue position changed to {newPosition}");
        
        // If now at front and waiting, show waiting UI and notify container
        if (newPosition == 0 && currentState == TechCustomerState.WaitingAtItemContainer)
        {
            ShowWaitingUI();
            assignedItemContainer?.OnCustomerArrivedAtFront(this);
        }
    }
    
    /// <summary>
    /// Called when customer is served at item container (gets the item)
    /// </summary>
    public void OnServedAtItemContainer()
    {
        HideWaitingUI();
        
        currentState = TechCustomerState.ReceivingItem;
        
        // Show bag (customer received the item)
        ShowBag();
        
        // Play bag animation
        SetWalkingWithBag(true);
        
        // Remove from item container queue
        if (assignedItemContainer != null)
        {
            assignedItemContainer.RemoveCustomer(this);
        }
        
        // Notify spawner that a customer was served at container
        if (spawner != null)
        {
            spawner.OnCustomerServedAtContainer(targetTechStore);
        }
        
        Debug.Log("[TechCustomer] Served at item container - got item, moving to cashier");
        
        // Move to cashier
        MoveToCashier();
    }
    
    private void OnNoContainerAvailable()
    {
        currentState = TechCustomerState.NoContainerAvailable;
        
        // Stop any movement
        isMoving = false;
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        SetWalking(false);
        
        // Show no container UI
        if (waitingUICanvas != null)
        {
            waitingUICanvas.enabled = true;
        }
        
        if (noContainerUI != null)
        {
            noContainerUI.SetActive(true);
            Debug.Log("[TechCustomer] No container UI shown");
        }
        
        // Leave after showing UI briefly
        DOVirtual.DelayedCall(noContainerDisplayDuration, () =>
        {
            HideNoContainerUI();
            StartLeaving();
        });
        
        Debug.Log("[TechCustomer] No container available - will leave shortly");
    }
    
    #endregion
    
    #region Cashier Phase
    
    private void MoveToCashier()
    {
        // Get an available service spot from the store
        assignedServiceSpot = targetTechStore.BaseStore?.GetAvailableSpotForQueue();
        
        if (assignedServiceSpot == null)
        {
            Debug.LogWarning("[TechCustomer] No available cashier - leaving");
            StartLeaving();
            return;
        }
        
        queuePosition = assignedServiceSpot.AddCustomerToQueue(this);
        
        if (queuePosition < 0)
        {
            Debug.LogError("[TechCustomer] Failed to join cashier queue!");
            StartLeaving();
            return;
        }
        
        MoveTo(assignedServiceSpot.GetQueueWorldPosition(queuePosition));
        currentState = TechCustomerState.MovingToCashier;
        
        Debug.Log($"[TechCustomer] Moving to cashier queue position {queuePosition}");
    }
    
    /// <summary>
    /// Called when customer is served at cashier (pays)
    /// </summary>
    public void OnServedAtCashier()
    {
        HideWaitingUI();
        
        // Remove from cashier queue
        if (assignedServiceSpot != null)
        {
            assignedServiceSpot.RemoveCustomer(this);
        }
        
        // Drop money
        DropMoney();
        
        // Register as served with level manager
        if (LevelManager.Instance != null && targetTechStore?.BaseStore != null)
        {
            LevelManager.Instance.RegisterCustomerServedAtStore(targetTechStore.BaseStore.StoreName);
        }
        
        // Make store dirty (optional - tech stores may or may not need cleaning)
        if (targetTechStore?.BaseStore != null)
        {
            targetTechStore.BaseStore.OnCustomerServed();
        }
        
        // Play customer served sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.CustomerServed);
        }
        
        Debug.Log("[TechCustomer] Served at cashier - paid, now leaving");
        
        StartLeaving();
    }
    
    // IQueueableCustomer interface implementation
    public void OnQueuePositionChanged(int newPosition, ServiceSpot spot)
    {
        HideWaitingUI();
        queuePosition = newPosition;
        currentState = TechCustomerState.MovingToCashier;
        MoveTo(spot.GetQueueWorldPosition(newPosition));
    }
    
    public void OnServed()
    {
        OnServedAtCashier();
    }
    
    #endregion
    
    #region Leaving
    
    private void StartLeaving()
    {
        currentState = TechCustomerState.Leaving;
        MoveTo(exitPoint.position);
    }
    
    #endregion
    
    #region Movement & Animation
    
    private void MoveTo(Vector3 destination)
    {
        if (agent == null) return;
        
        currentDestination = new Vector3(destination.x, transform.position.y, destination.z);
        agent.SetDestination(destination);
        isMoving = true;
        
        if (hasBag)
        {
            SetWalkingWithBag(true);
        }
        else
        {
            SetWalking(true);
        }
    }
    
    private void SetWalking(bool walking)
    {
        if (animator != null)
        {
            animator.SetBool("IsWalking", walking);
            animator.SetBool("IsWalkingWithBag", false);
        }
    }
    
    private void SetWalkingWithBag(bool walking)
    {
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsWalkingWithBag", walking);
        }
    }
    
    #endregion
    
    #region Bag
    
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
    
    #endregion
    
    #region UI
    
    public void ShowWaitingUI()
    {
        if (waitingUICanvas != null)
            waitingUICanvas.enabled = true;
        
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
            observingUI.localScale = Vector3.one * pulseMinScale;
            
            observingUITween?.Kill();
            observingUITween = observingUI
                .DOScale(pulseMaxScale, pulseDuration)
                .SetEase(pulseEase)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }
    
    public void HideObservingUI()
    {
        observingUITween?.Kill();
        
        if (observingUI != null)
            observingUI.gameObject.SetActive(false);
    }
    
    private void HideNoContainerUI()
    {
        if (noContainerUI != null)
            noContainerUI.SetActive(false);
        
        if (waitingUICanvas != null)
            waitingUICanvas.enabled = false;
    }
    
    #endregion
    
    #region Money
    
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
    
    #endregion
}
