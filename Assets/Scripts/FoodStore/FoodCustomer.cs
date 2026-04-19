using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

/// <summary>
/// Extended customer behavior for food stores.
/// Handles the complete flow: Cashier → Pickup → Table → Eat → Leave
/// </summary>
public class FoodCustomer : MonoBehaviour, IQueueableCustomer
{
    public enum FoodCustomerState
    {
        // Initial states (similar to regular customer)
        MovingToObservatoryPoint,
        Observing,
        MovingToServiceSpot,
        WaitingAtCashier,
        
        // Food store specific states
        MovingToPickupPoint,
        WaitingForFood,
        PickingUpTray,
        MovingToTable,
        SittingDown,
        Eating,
        StandingUp,
        Leaving,
        
        // Error state
        NoSeatAvailable
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
    
    [Header("No Seat UI")]
    [SerializeField] private GameObject noSeatUI;
    [SerializeField] private float noSeatDisplayDuration = 2f;
    
    [Header("Observing UI")]
    [SerializeField] private Transform observingUI;
    
    [Header("Money")]
    [SerializeField] private GameObject moneyPrefab;
    [SerializeField] private Vector3 moneyDropOffset = new Vector3(0, 0.5f, 0);
    
    [Header("Appearance")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
    [SerializeField] private System.Collections.Generic.List<Material> materials;
    
    [Header("Tray Carry")]
    [SerializeField] private Transform trayHoldPoint; // Where tray attaches when carrying
    
    [Header("Observation")]
    [SerializeField] private float minObservationTime = 2f;
    [SerializeField] private float maxObservationTime = 4f;
    
    [Header("Sit/Stand Animation")]
    [SerializeField] private float sitDuration = 0.5f;
    [SerializeField] private float standDuration = 0.5f;
    
    // Components
    private NavMeshAgent agent;
    private FoodCustomerState currentState;
    
    // References
    private FoodStore targetFoodStore;
    private ServiceSpot assignedServiceSpot;
    private PickupPoint targetPickupPoint;
    private DiningSeat assignedSeat;
    private FoodTray currentTray;
    private Transform exitPoint;
    private FoodCustomerSpawner spawner; // Reference to spawner for tracking
    
    // State variables
    private int moneyPerDrop;
    private int queuePosition;
    private Vector3 currentDestination;
    private bool isMoving;
    private float observationTimer;
    private float eatingTimer;
    
    // Tweens
    private Tween waitingUITween;
    private Tween observingUITween;
    
    public FoodCustomerState State => currentState;
    public int QueuePosition => queuePosition;
    public FoodStore TargetFoodStore => targetFoodStore;
    
    // IQueueableCustomer implementation
    public bool IsWaitingAtStore => currentState == FoodCustomerState.WaitingAtCashier;
    public GameObject GameObject => gameObject;
    
    /// <summary>
    /// Set the spawner reference for tracking customer count
    /// </summary>
    public void SetSpawner(FoodCustomerSpawner customerSpawner)
    {
        spawner = customerSpawner;
    }
    
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        HideWaitingUI();
        HideObservingUI();
        HideNoSeatUI();
        
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
    /// Initialize food customer for a food store
    /// </summary>
    public void Initialize(ServiceSpot serviceSpot, FoodStore foodStore, Transform exit, bool enableObservatoryPhase = true)
    {
        if (serviceSpot == null || foodStore == null)
        {
            Debug.LogError("[FoodCustomer] Cannot initialize - missing references!");
            Destroy(gameObject);
            return;
        }
        
        assignedServiceSpot = serviceSpot;
        targetFoodStore = foodStore;
        targetPickupPoint = foodStore.PickupPoint;
        exitPoint = exit;
        moneyPerDrop = foodStore.BaseStore != null ? foodStore.BaseStore.MoneyPerCustomer : 10;
        
        // Check if observatory phase is enabled and store has observatory points
        if (enableObservatoryPhase &&
            foodStore.BaseStore != null && 
            foodStore.BaseStore.ObservatoryPoints != null && 
            foodStore.BaseStore.ObservatoryPoints.Count > 0)
        {
            Transform randomObservatoryPoint = foodStore.BaseStore.ObservatoryPoints[
                Random.Range(0, foodStore.BaseStore.ObservatoryPoints.Count)];
            MoveTo(randomObservatoryPoint.position);
            currentState = FoodCustomerState.MovingToObservatoryPoint;
        }
        else
        {
            JoinCashierQueue();
        }
    }
    
    private void Update()
    {
        switch (currentState)
        {
            case FoodCustomerState.Observing:
                observationTimer -= Time.deltaTime;
                if (observationTimer <= 0f)
                {
                    OnFinishedObserving();
                }
                break;
                
            case FoodCustomerState.Eating:
                eatingTimer -= Time.deltaTime;
                if (eatingTimer <= 0f)
                {
                    OnFinishedEating();
                }
                break;
                
            case FoodCustomerState.NoSeatAvailable:
                // Don't process movement when waiting for seat
                return;
        }
        
        // Handle movement arrival - use NavMeshAgent's built-in detection for reliability
        if (isMoving && agent != null)
        {
            // Check if agent has finished calculating path and has reached destination
            if (!agent.pathPending)
            {
                // Use agent's remaining distance (more reliable than manual calculation)
                if (agent.remainingDistance <= arrivalThreshold)
                {
                    // Additional check: velocity is very low (agent has stopped moving)
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
            case FoodCustomerState.MovingToObservatoryPoint:
                StartObserving();
                break;
                
            case FoodCustomerState.MovingToServiceSpot:
                currentState = FoodCustomerState.WaitingAtCashier;
                ShowWaitingUI();
                break;
                
            case FoodCustomerState.MovingToPickupPoint:
                OnArrivedAtPickupPoint();
                break;
                
            case FoodCustomerState.MovingToTable:
                OnArrivedAtTable();
                break;
                
            case FoodCustomerState.Leaving:
                OnExitedStore();
                break;
        }
    }
    
    /// <summary>
    /// Called when customer reaches exit point after eating
    /// </summary>
    private void OnExitedStore()
    {
        // Notify spawner that customer has exited (for spawn limit tracking)
        if (spawner != null)
        {
            spawner.OnCustomerExited();
        }
        
        Debug.Log("[FoodCustomer] Exited store");
        Destroy(gameObject);
    }
    
    #region Observatory Phase
    
    private void StartObserving()
    {
        currentState = FoodCustomerState.Observing;
        observationTimer = Random.Range(minObservationTime, maxObservationTime);
        SetWalking(false);
        ShowObservingUI();
    }
    
    private void OnFinishedObserving()
    {
        HideObservingUI();
        JoinCashierQueue();
    }
    
    #endregion
    
    #region Cashier Phase
    
    private void JoinCashierQueue()
    {
        // Pass 'this' since FoodCustomer implements IQueueableCustomer
        queuePosition = assignedServiceSpot.AddCustomerToQueue(this);
        
        if (queuePosition < 0)
        {
            Debug.LogError("[FoodCustomer] Failed to join cashier queue!");
            StartLeaving();
            return;
        }
        
        MoveTo(assignedServiceSpot.GetQueueWorldPosition(queuePosition));
        currentState = FoodCustomerState.MovingToServiceSpot;
        
        if (queuePosition == 0)
        {
            ShowWaitingUI();
        }
    }
    
    /// <summary>
    /// Called when player/service guy serves at cashier
    /// </summary>
    public void OnServedAtCashier()
    {
        HideWaitingUI();
        
        // Remove from cashier queue - pass 'this' since we implement IQueueableCustomer
        if (assignedServiceSpot != null)
        {
            assignedServiceSpot.RemoveCustomer(this);
        }
        
        // Drop money
        DropMoney();
        
        // Register as served with level manager (food customer is "served" at cashier)
        if (LevelManager.Instance != null && targetFoodStore?.BaseStore != null)
        {
            LevelManager.Instance.RegisterCustomerServedAtStore(targetFoodStore.BaseStore.StoreName);
        }
        
        // NOTE: Food stores do NOT trigger cleaning/janitor - only regular stores do
        // So we skip calling targetFoodStore.BaseStore.OnCustomerServed()
        
        // Play customer served sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.CustomerServed);
        }
        
        // Notify spawner that a customer was served - triggers immediate spawn of replacement
        if (spawner != null)
        {
            spawner.OnCustomerServedAtCashier(targetFoodStore);
        }
        
        Debug.Log("[FoodCustomer] Served at cashier - registered as served");
        
        // Move to pickup point
        MoveToPickupPoint();
    }
    
    // IQueueableCustomer interface implementation
    public void OnQueuePositionChanged(int newPosition, ServiceSpot spot)
    {
        UpdateCashierQueuePosition(newPosition);
    }
    
    public void OnServed()
    {
        OnServedAtCashier();
    }
    
    #endregion
    
    #region Pickup Phase
    
    private void MoveToPickupPoint()
    {
        if (targetPickupPoint == null)
        {
            Debug.LogError("[FoodCustomer] No pickup point assigned!");
            StartLeaving();
            return;
        }
        
        // Join the pickup queue
        int position = targetPickupPoint.AddCustomerToQueue(this);
        if (position < 0)
        {
            Debug.LogError("[FoodCustomer] Could not join pickup queue!");
            StartLeaving();
            return;
        }
        
        queuePosition = position;
        currentState = FoodCustomerState.MovingToPickupPoint;
        
        // Move to queue position
        Vector3 queuePos = targetPickupPoint.GetQueueWorldPosition(position);
        MoveTo(queuePos);
        
        Debug.Log($"[FoodCustomer] Moving to pickup queue position {position}");
    }
    
    /// <summary>
    /// Called when position in pickup queue changes
    /// </summary>
    public void OnPickupQueuePositionChanged(int newPosition)
    {
        queuePosition = newPosition;
        
        // Move to new queue position
        Vector3 queuePos = targetPickupPoint.GetQueueWorldPosition(newPosition);
        MoveTo(queuePos);
        
        Debug.Log($"[FoodCustomer] Pickup queue position changed to {newPosition}");
        
        // If we're now at front and waiting for food, notify pickup point
        if (newPosition == 0 && currentState == FoodCustomerState.WaitingForFood)
        {
            targetPickupPoint?.OnFrontCustomerReadyForService(this);
        }
    }
    
    private void OnArrivedAtPickupPoint()
    {
        currentState = FoodCustomerState.WaitingForFood;
        
        // If we're at the front of the queue, notify pickup point we're ready
        if (queuePosition == 0)
        {
            targetPickupPoint?.OnFrontCustomerReadyForService(this);
        }
        
        Debug.Log($"[FoodCustomer] Arrived at pickup point (position {queuePosition}), waiting for food...");
    }
    
    /// <summary>
    /// Called by FoodStore when all items are prepared
    /// </summary>
    public void OnFoodReady(FoodTray tray)
    {
        Debug.Log($"[FoodCustomer] OnFoodReady called. Tray: {tray?.name ?? "NULL"}, TrayHoldPoint: {trayHoldPoint?.name ?? "NULL"}");
        
        if (trayHoldPoint != null)
        {
            Debug.Log($"[FoodCustomer] TrayHoldPoint rotation - Local: {trayHoldPoint.localEulerAngles}, World: {trayHoldPoint.eulerAngles}");
        }
        
        currentState = FoodCustomerState.PickingUpTray;
        currentTray = tray;
        
        // Animate tray to hand
        if (tray != null && trayHoldPoint != null)
        {
            Debug.Log($"[FoodCustomer] Moving tray to hand at {trayHoldPoint.name}");
            tray.MoveToHand(trayHoldPoint, OnTrayPickedUp);
        }
        else
        {
            Debug.LogWarning($"[FoodCustomer] Cannot move tray - Tray null: {tray == null}, TrayHoldPoint null: {trayHoldPoint == null}");
            OnTrayPickedUp();
        }
    }
    
    private void OnTrayPickedUp()
    {
        // Start lift animation
        SetLiftCarrying(true);
        
        // Find a seat BEFORE leaving pickup (so we stay in queue if no seat)
        assignedSeat = targetFoodStore?.FindAvailableSeat();
        
        if (assignedSeat == null)
        {
            // No seat available - wait in pickup queue
            OnNoSeatAvailable();
            return;
        }
        
        // We have a seat! Now leave the pickup queue
        targetPickupPoint?.OnCustomerLeft(this);
        
        // Notify spawner that customer left pickup queue - this triggers spawn of next customer
        if (spawner != null && targetFoodStore != null)
        {
            spawner.OnCustomerLeftPickup(targetFoodStore);
        }
        
        // Reserve the seat and move
        MoveToSeat();
    }
    
    #endregion
    
    #region Dining Phase
    
    private void MoveToSeat()
    {
        if (assignedSeat == null) return;
        
        // Reserve the seat
        assignedSeat.Reserve(this);
        
        // Move to NavMesh-friendly approach position first
        currentState = FoodCustomerState.MovingToTable;
        Vector3 navTarget = assignedSeat.GetNavMeshTargetPosition();
        MoveTo(navTarget);
        
        Debug.Log($"[FoodCustomer] Moving to table seat via NavMesh target: {navTarget}");
    }
    
    private void FindAndMoveToSeat()
    {
        assignedSeat = targetFoodStore?.FindAvailableSeat();
        
        if (assignedSeat == null)
        {
            // No seat available - show UI and leave
            OnNoSeatAvailable();
            return;
        }
        
        MoveToSeat();
    }
    
    private void OnNoSeatAvailable()
    {
        currentState = FoodCustomerState.NoSeatAvailable;
        
        // FULLY stop any movement - customer stays in pickup queue position
        isMoving = false;
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.updatePosition = false;  // Prevent NavMesh from moving transform
            agent.updateRotation = false;
        }
        SetWalking(false);
        SetLiftIdle(true);  // Show lift idle animation (holding tray, not moving)
        
        // Enable the canvas first (noSeatUI is likely a child of waitingUICanvas)
        if (waitingUICanvas != null)
        {
            waitingUICanvas.enabled = true;
        }
        
        // Keep holding tray and show waiting UI
        if (noSeatUI != null)
        {
            noSeatUI.SetActive(true);
            Debug.Log("[FoodCustomer] No seat UI shown");
        }
        else
        {
            Debug.LogWarning("[FoodCustomer] No seat UI is null - cannot show!");
        }
        
        // Notify food store we're blocking the queue
        targetFoodStore?.OnCustomerWaitingForSeat(this);
        
        Debug.Log("[FoodCustomer] No seat available - waiting in pickup queue for seat to clear");
    }
    
    /// <summary>
    /// Called when a seat becomes available (garbage was picked up)
    /// </summary>
    public void OnSeatBecameAvailable()
    {
        if (currentState != FoodCustomerState.NoSeatAvailable) return;
        
        Debug.Log("[FoodCustomer] Seat became available notification received");
        
        // Try to find a seat again
        assignedSeat = targetFoodStore?.FindAvailableSeat();
        
        if (assignedSeat != null)
        {
            Debug.Log("[FoodCustomer] Seat found! Moving to table.");
            
            // Re-enable agent movement FULLY
            if (agent != null)
            {
                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.isStopped = false;
            }
            
            // Stop lift idle, start lift walking
            SetLiftIdle(false);
            
            // Hide no seat UI and canvas
            if (noSeatUI != null)
            {
                noSeatUI.SetActive(false);
            }
            
            if (waitingUICanvas != null)
            {
                waitingUICanvas.enabled = false;
            }
            
            // Leave pickup queue
            targetPickupPoint?.OnCustomerLeft(this);
            
            // Notify spawner that customer left pickup queue - this triggers spawn of next customer
            if (spawner != null && targetFoodStore != null)
            {
                spawner.OnCustomerLeftPickup(targetFoodStore);
            }
            
            // Move to seat
            MoveToSeat();
        }
        else
        {
            Debug.Log("[FoodCustomer] Seat became available but couldn't find one - still waiting");
        }
    }
    
    private void OnArrivedAtTable()
    {
        // Start sitting animation
        currentState = FoodCustomerState.SittingDown;
        SetLiftCarrying(false);
        
        // Disable NavMeshAgent to prevent conflicts during precise positioning
        if (agent != null)
        {
            agent.enabled = false;
        }
        
        // Face the correct direction using the seat's configured rotation
        Quaternion seatedRotation = assignedSeat.GetSeatedRotation();
        transform.DORotateQuaternion(seatedRotation, sitDuration * 0.5f);
        
        // Move to EXACT sit position (including Y coordinate)
        if (assignedSeat.SitPoint != null)
        {
            Vector3 exactSitPosition = assignedSeat.SitPoint.position;
            Debug.Log($"[FoodCustomer] Moving to exact sit position: {exactSitPosition}");
            transform.DOMove(exactSitPosition, sitDuration)
                .OnComplete(StartSitting);
        }
        else
        {
            StartSitting();
        }
    }
    
    private void StartSitting()
    {
        // Place tray on table
        if (currentTray != null && assignedSeat.ParentTable != null)
        {
            Transform trayPoint = assignedSeat.ParentTable.TrayPlacementPoint;
            if (trayPoint != null)
            {
                currentTray.PlaceOnTable(trayPoint);
            }
        }
        
        // Play sit animation
        SetSitting(true);
        
        // Start eating after sit animation
        DOVirtual.DelayedCall(sitDuration, StartEating);
    }
    
    private void StartEating()
    {
        currentState = FoodCustomerState.Eating;
        eatingTimer = assignedSeat?.EatingDuration ?? 5f;
        
        // Play eating animation
        SetEating(true);
        
        Debug.Log($"[FoodCustomer] Started eating. Duration: {eatingTimer}s");
    }
    
    private void OnFinishedEating()
    {
        SetEating(false);
        
        // Destroy tray
        if (currentTray != null)
        {
            currentTray.DestroyTray();
            currentTray = null;
        }
        
        // Stand up
        currentState = FoodCustomerState.StandingUp;
        SetSitting(false);
        SetStandingUp(true);
        
        DOVirtual.DelayedCall(standDuration, OnStoodUp);
    }
    
    private void OnStoodUp()
    {
        SetStandingUp(false);
        
        // Re-enable NavMeshAgent for movement
        if (agent != null)
        {
            agent.enabled = true;
        }
        
        // Spawn garbage on table
        if (assignedSeat?.ParentTable != null)
        {
            assignedSeat.ParentTable.SpawnGarbage();
        }
        
        // Release seat
        assignedSeat?.Release();
        assignedSeat = null;
        
        // Leave
        StartLeaving();
    }
    
    #endregion
    
    #region Leaving
    
    private void StartLeaving()
    {
        currentState = FoodCustomerState.Leaving;
        MoveTo(exitPoint.position);
    }
    
    #endregion
    
    #region Movement & Animation
    
    private void MoveTo(Vector3 destination)
    {
        if (agent == null) return;
        
        currentDestination = destination;
        agent.stoppingDistance = stoppingDistance;
        agent.SetDestination(destination);
        isMoving = true;
        SetWalking(true);
    }
    
    private void SetWalking(bool walking)
    {
        if (animator != null)
        {
            animator.SetBool("IsWalking", walking);
        }
    }
    
    private void SetLiftCarrying(bool carrying)
    {
        if (animator != null)
        {
            if (carrying)
            {
                animator.SetBool("IsLiftWalking", true);
                animator.SetBool("IsLiftIdle", false);
                animator.SetBool("IsWalking", false);
            }
            else
            {
                animator.SetBool("IsLiftWalking", false);
                animator.SetBool("IsLiftIdle", false);
            }
        }
    }
    
    private void SetLiftIdle(bool liftIdle)
    {
        if (animator != null)
        {
            animator.SetBool("IsLiftIdle", liftIdle);
            animator.SetBool("IsLiftWalking", false);
            animator.SetBool("IsWalking", false);
        }
    }
    
    private void SetSitting(bool sitting)
    {
        if (animator != null)
        {
            animator.SetBool("IsSitting", sitting);
        }
    }
    
    private void SetEating(bool eating)
    {
        if (animator != null)
        {
            animator.SetBool("IsEating", eating);
        }
    }
    
    private void SetStandingUp(bool standingUp)
    {
        if (animator != null)
        {
            animator.SetBool("IsStandingUp", standingUp);
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
            waitingUICanvas.enabled = true;
        
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
    
    public void ShowNoSeatUI()
    {
        if (noSeatUI != null)
        {
            noSeatUI.SetActive(true);
        }
    }
    
    public void HideNoSeatUI()
    {
        if (noSeatUI != null)
        {
            noSeatUI.SetActive(false);
        }
    }
    
    #endregion
    
    #region Money
    
    private void DropMoney()
    {
        Debug.Log($"[FoodCustomer] DropMoney called. MoneyPrefab: {moneyPrefab != null}, MoneyPerDrop: {moneyPerDrop}");
        
        if (moneyPrefab == null)
        {
            Debug.LogWarning("[FoodCustomer] Cannot drop money - moneyPrefab is not assigned!");
            return;
        }
        
        if (moneyPerDrop <= 0)
        {
            Debug.LogWarning($"[FoodCustomer] MoneyPerDrop is {moneyPerDrop} - skipping drop");
            return;
        }
        
        Vector3 dropPosition = transform.position + moneyDropOffset;
        GameObject moneyDrop = Instantiate(moneyPrefab, dropPosition, Quaternion.identity);
        
        MoneyDrop moneyScript = moneyDrop.GetComponent<MoneyDrop>();
        if (moneyScript != null)
        {
            moneyScript.Initialize(moneyPerDrop);
            Debug.Log($"[FoodCustomer] Money dropped! Amount: {moneyPerDrop}");
        }
        else
        {
            Debug.LogWarning("[FoodCustomer] MoneyDrop component not found on money prefab!");
        }
    }
    
    #endregion
    
    #region Queue Integration
    
    /// <summary>
    /// Called when queue position changes
    /// </summary>
    public void UpdateCashierQueuePosition(int newPosition)
    {
        HideWaitingUI();
        queuePosition = newPosition;
        currentState = FoodCustomerState.MovingToServiceSpot;
        MoveTo(assignedServiceSpot.GetQueueWorldPosition(newPosition));
    }
    
    #endregion
}
