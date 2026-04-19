using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using System.Collections.Generic;

/// <summary>
/// An item container/station in a tech store where customers wait to be served.
/// Player stands in trigger zone to serve the customer at the front of the queue.
/// Shows radial progress UI while serving.
/// </summary>
public class TechItemContainer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform itemVisual; // The visual that will pulse
    [SerializeField] private LayerMask playerLayer;
    
    [Header("Stock Items")]
    [Tooltip("List of item GameObjects on the container. Items are removed when customers are served.")]
    [SerializeField] private List<GameObject> stockItems = new List<GameObject>();
    
    [Header("Restock UI")]
    [SerializeField] private Canvas restockCanvas;
    [SerializeField] private Image restockRadialImage;
    [SerializeField] private Color restockProgressColor = Color.yellow;
    [SerializeField] private Color restockEmptyColor = Color.gray;
    [SerializeField] private float restockDuration = 1f;
    
    [Header("Queue Settings")]
    [SerializeField] private Transform customerWaitPoint;
    [SerializeField] private int maxQueueSize = 3;
    [SerializeField] private float queueSpacing = 1.5f;
    [SerializeField] private Transform queueDirection; // Direction queue extends from wait point
    
    [Header("Radial Progress UI")]
    [SerializeField] private Canvas progressCanvas;
    [SerializeField] private Image radialProgressImage;
    [SerializeField] private Color progressColor = Color.green;
    [SerializeField] private Color emptyColor = Color.white;
    
    [Header("Waiting Indicator Animation")]
    [SerializeField] private float indicatorPulseMax = 1.2f;
    [SerializeField] private float indicatorPulseDuration = 0.5f;
    
    [Header("Service Settings")]
    [SerializeField] private float serviceDuration = 2f;
    
    // [Header("Pulse Animation")]
    // [SerializeField] private float pulseMinScale = 0.9f;
    // [SerializeField] private float pulseMaxScale = 1.1f;
    // [SerializeField] private float pulseDuration = 0.3f;
    // [SerializeField] private Ease pulseEase = Ease.InOutSine;
    
    private TechStore parentTechStore;
    private List<TechCustomer> queue = new List<TechCustomer>();
    private bool isServing = false;
    private bool playerInRange = false;
    private float serviceProgress = 0f;
    private Vector3 originalScale;
    private Tween pulseTween;
    private Tween indicatorPulseTween;
    private CharacterController playerCharacterController;
    private bool wasCustomerWaiting = false;
    
    private const float MOVEMENT_THRESHOLD = 0.05f;
    
    // Stock management
    private int currentStockCount = 0;
    private bool isRestocking = false;
    private float restockProgress = 0f;
    private Tween restockPulseTween;
    private List<Vector3> stockItemOriginalScales = new List<Vector3>();
    
    public bool IsServing => isServing;
    public bool HasStock => currentStockCount > 0;
    public bool IsFullyStocked => stockItems.Count == 0 || currentStockCount >= stockItems.Count;
    public bool NeedsRestock => stockItems.Count > 0 && currentStockCount == 0;
    public int CurrentStock => currentStockCount;
    public int MaxStock => stockItems.Count;
    public float ServiceDuration => serviceDuration;
    public int QueueCount => queue.Count;
    public bool CanAcceptCustomer => queue.Count < maxQueueSize && HasStock;
    public bool HasCustomerWaiting => queue.Count > 0 && queue[0].State == TechCustomer.TechCustomerState.WaitingAtItemContainer;
    public TechCustomer FrontCustomer => queue.Count > 0 ? queue[0] : null;
    public Transform CustomerWaitPoint => customerWaitPoint;
    
    public event Action<TechItemContainer, TechCustomer> OnCustomerServed;
    
    private void Awake()
    {
        if (itemVisual != null)
        {
            originalScale = itemVisual.localScale;
        }
    }
    
    private void Start()
    {
        HideProgressUI();
        HideRestockUI();
        InitializeStock();
    }
    
    private void InitializeStock()
    {
        // Store original scales and activate all stock items initially
        stockItemOriginalScales.Clear();
        currentStockCount = stockItems.Count;
        
        foreach (var item in stockItems)
        {
            if (item != null)
            {
                stockItemOriginalScales.Add(item.transform.localScale);
                item.SetActive(true);
            }
            else
            {
                stockItemOriginalScales.Add(Vector3.one);
            }
        }
        Debug.Log($"[TechItemContainer] {gameObject.name} initialized with {currentStockCount} stock items");
    }
    
    public void Initialize(TechStore techStore)
    {
        parentTechStore = techStore;
        Debug.Log($"[TechItemContainer] {gameObject.name} initialized with TechStore: {techStore?.name}");
    }
    
    private void Update()
    {
        // Check if player has stopped moving (used by both restocking and serving)
        bool isPlayerStopped = IsPlayerStopped();
        
        // Handle restocking first (priority over serving)
        // Continue restocking until all items are back even if player started with partial stock
        if (isRestocking || (NeedsRestock && playerInRange))
        {
            // If we started restocking, continue until fully stocked even if NeedsRestock becomes false
            if (isRestocking && IsFullyStocked)
            {
                FinishRestocking();
                return;
            }
            
            // Only start/pause based on player movement if not fully stocked yet
            if (!IsFullyStocked)
            {
                if (isPlayerStopped && !isRestocking)
                {
                    StartRestocking();
                }
                else if (!isPlayerStopped && isRestocking)
                {
                    PauseRestocking();
                }
                
                if (isRestocking)
                {
                    UpdateRestocking(Time.deltaTime);
                }
            }
            return; // Don't process serving while restocking or need restock
        }
        
        // Check if there's stock available to serve
        if (!HasStock && stockItems.Count > 0)
        {
            if (isServing)
            {
                PauseServing();
            }
            return;
        }
        
        // Check if there's a customer waiting at front
        if (!HasCustomerWaiting)
        {
            if (isServing)
            {
                PauseServing();
            }
            return;
        }
        
        // Check if player is in range
        if (!playerInRange)
        {
            if (isServing)
            {
                PauseServing();
            }
            return;
        }
        
        // Start or pause serving based on player movement
        if (isPlayerStopped && !isServing)
        {
            StartServing();
        }
        else if (!isPlayerStopped && isServing)
        {
            PauseServing();
        }
        
        // Update service progress
        if (isServing)
        {
            UpdateService(Time.deltaTime);
        }
    }
    
    private bool IsPlayerStopped()
    {
        if (playerCharacterController != null)
        {
            float speed = playerCharacterController.velocity.magnitude;
            return speed < MOVEMENT_THRESHOLD;
        }
        return true;
    }
    
    #region Queue Management
    
    /// <summary>
    /// Add a customer to this container's queue and return their position
    /// </summary>
    public int AddCustomerToQueue(TechCustomer customer)
    {
        if (customer == null)
        {
            Debug.LogError($"[TechItemContainer] {name} - Cannot add null customer to queue!");
            return -1;
        }
        
        if (queue.Count >= maxQueueSize)
        {
            Debug.LogWarning($"[TechItemContainer] {name} queue is full!");
            return -1;
        }
        
        queue.Add(customer);
        int position = queue.Count - 1;
        Debug.Log($"[TechItemContainer] {name} - Customer added at position {position}. Queue size: {queue.Count}");
        
        return position;
    }
    
    /// <summary>
    /// Gets the world position for a specific queue position
    /// </summary>
    public Vector3 GetQueueWorldPosition(int position)
    {
        if (customerWaitPoint == null) return transform.position;
        
        // Position 0 is at the wait point (service position)
        if (position == 0)
        {
            return customerWaitPoint.position;
        }
        
        // Calculate queue direction
        Vector3 direction;
        if (queueDirection != null)
        {
            direction = (queueDirection.position - customerWaitPoint.position).normalized;
        }
        else
        {
            // Default: queue extends backward from the spot
            direction = -transform.forward;
        }
        
        return customerWaitPoint.position + direction * (position * queueSpacing);
    }
    
    /// <summary>
    /// Remove a customer from the queue and advance others
    /// </summary>
    public void RemoveCustomer(TechCustomer customer)
    {
        int index = queue.IndexOf(customer);
        if (index < 0)
        {
            Debug.LogWarning($"[TechItemContainer] Customer not found in queue!");
            return;
        }
        
        queue.RemoveAt(index);
        Debug.Log($"[TechItemContainer] {name} - Customer removed from position {index}. Queue size: {queue.Count}");
        
        // Reset service progress for next customer
        serviceProgress = 0f;
        if (radialProgressImage != null)
        {
            radialProgressImage.fillAmount = 0f;
            radialProgressImage.color = emptyColor;
        }
        
        // Update positions for all customers behind the removed one
        for (int i = index; i < queue.Count; i++)
        {
            Debug.Log($"[TechItemContainer] Advancing customer at {i+1} to position {i}");
            queue[i].OnContainerQueuePositionChanged(i);
        }
        
        // Hide indicator if no more customers
        if (queue.Count == 0)
        {
            HideWaitingIndicator();
            HideProgressUI();
        }
    }
    
    /// <summary>
    /// Called by TechCustomer when they arrive at the front of the queue (position 0)
    /// </summary>
    public void OnCustomerArrivedAtFront(TechCustomer customer)
    {
        if (queue.Count == 0 || queue[0] != customer)
        {
            Debug.LogWarning($"[TechItemContainer] OnCustomerArrivedAtFront called but customer is not at front!");
            return;
        }
        
        Debug.Log($"[TechItemContainer] {name} - Front customer arrived, showing waiting indicator");
        ShowWaitingIndicator();
    }
    
    #endregion
    
    #region Service
    
    private void StartServing()
    {
        isServing = true;
        ShowProgressUI();
        // StartPulseAnimation();
        
        // Play preparing sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.FoodPreparing);
        }
        
        Debug.Log($"[TechItemContainer] Started serving at {gameObject.name}");
    }
    
    private void PauseServing()
    {
        isServing = false;
        // StopPulseAnimation();
        
        // Stop preparing sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopLoopingSFX();
        }
        
        Debug.Log($"[TechItemContainer] Paused serving at {gameObject.name}");
    }
    
    private void UpdateService(float deltaTime)
    {
        serviceProgress += deltaTime;
        
        // Update radial UI
        if (radialProgressImage != null)
        {
            float fillAmount = serviceProgress / serviceDuration;
            radialProgressImage.fillAmount = fillAmount;
            radialProgressImage.color = Color.Lerp(emptyColor, progressColor, fillAmount);
        }
        
        // Check if service complete
        if (serviceProgress >= serviceDuration)
        {
            CompleteService();
        }
    }
    
    private void CompleteService()
    {
        isServing = false;
        // StopPulseAnimation();
        
        // Stop preparing sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopLoopingSFX();
        }
        
        // Get the front customer before removing
        TechCustomer servedCustomer = FrontCustomer;
        
        // Remove one stock item
        RemoveOneStockItem();
        
        // Play completion feedback
        if (itemVisual != null)
        {
            itemVisual.DOPunchScale(Vector3.one * 0.2f, 0.3f, 1, 0.5f);
        }
        
        Debug.Log($"[TechItemContainer] Completed serving at {gameObject.name}. Remaining stock: {currentStockCount}/{stockItems.Count}");
        
        // Notify customer they've been served
        if (servedCustomer != null)
        {
            servedCustomer.OnServedAtItemContainer();
        }
        
        // Fire event
        OnCustomerServed?.Invoke(this, servedCustomer);
        
        // Reset for next customer
        serviceProgress = 0f;
        if (radialProgressImage != null)
        {
            radialProgressImage.fillAmount = 0f;
            radialProgressImage.color = emptyColor;
        }
        
        // Check if we need to show restock UI
        if (NeedsRestock)
        {
            ShowRestockUI();
        }
    }
    
    #endregion
    
    #region Stock Management
    
    private void RemoveOneStockItem()
    {
        if (currentStockCount <= 0) return;
        
        // Find the last active stock item and deactivate it
        for (int i = stockItems.Count - 1; i >= 0; i--)
        {
            if (stockItems[i] != null && stockItems[i].activeSelf)
            {
                stockItems[i].SetActive(false);
                currentStockCount--;
                Debug.Log($"[TechItemContainer] {gameObject.name} - Removed stock item. Remaining: {currentStockCount}");
                break;
            }
        }
    }
    
    private void RestockOneItem()
    {
        if (currentStockCount >= stockItems.Count) return;
        
        // Find the first inactive stock item and activate it
        for (int i = 0; i < stockItems.Count; i++)
        {
            if (stockItems[i] != null && !stockItems[i].activeSelf)
            {
                stockItems[i].SetActive(true);
                
                // Get original scale for this item
                Vector3 originalScale = (i < stockItemOriginalScales.Count) ? stockItemOriginalScales[i] : Vector3.one;
                
                // Pop animation for restocked item - scale from zero to original
                stockItems[i].transform.localScale = Vector3.zero;
                stockItems[i].transform.DOScale(originalScale, 0.3f).SetEase(Ease.OutBack);
                
                currentStockCount++;
                Debug.Log($"[TechItemContainer] {gameObject.name} - Restocked item. Current: {currentStockCount}/{stockItems.Count}");
                break;
            }
        }
    }
    
    private void StartRestocking()
    {
        isRestocking = true;
        
        // Show restock progress
        if (restockCanvas != null)
        {
            restockCanvas.enabled = true;
        }
        
        // Stop the pulse animation while actively restocking
        restockPulseTween?.Kill();
        if (restockRadialImage != null)
        {
            restockRadialImage.transform.localScale = Vector3.one;
        }
        
        Debug.Log($"[TechItemContainer] {gameObject.name} - Started restocking");
    }
    
    private void PauseRestocking()
    {
        isRestocking = false;
        
        // Resume pulse animation
        if (restockRadialImage != null && NeedsRestock)
        {
            restockPulseTween?.Kill();
            restockPulseTween = restockRadialImage.transform
                .DOScale(Vector3.one * indicatorPulseMax, indicatorPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        
        Debug.Log($"[TechItemContainer] {gameObject.name} - Paused restocking");
    }
    
    private void UpdateRestocking(float deltaTime)
    {
        restockProgress += deltaTime;
        
        // Update radial UI
        if (restockRadialImage != null)
        {
            float fillAmount = restockProgress / restockDuration;
            restockRadialImage.fillAmount = fillAmount;
            restockRadialImage.color = Color.Lerp(restockEmptyColor, restockProgressColor, fillAmount);
        }
        
        // Check if one item restocked
        if (restockProgress >= restockDuration)
        {
            CompleteOneRestock();
        }
    }
    
    private void CompleteOneRestock()
    {
        // Restock ALL items at once
        RestockAllItems();
        
        // Reset progress
        restockProgress = 0f;
        if (restockRadialImage != null)
        {
            restockRadialImage.fillAmount = 0f;
            restockRadialImage.color = restockEmptyColor;
        }
        
        // Finish restocking since all items are now restocked
        FinishRestocking();
    }
    
    private void RestockAllItems()
    {
        // Activate all inactive stock items at once
        for (int i = 0; i < stockItems.Count; i++)
        {
            if (stockItems[i] != null && !stockItems[i].activeSelf)
            {
                stockItems[i].SetActive(true);
                
                // Get original scale for this item
                Vector3 originalScale = (i < stockItemOriginalScales.Count) ? stockItemOriginalScales[i] : Vector3.one;
                
                // Pop animation for restocked item - scale from zero to original
                stockItems[i].transform.localScale = Vector3.zero;
                stockItems[i].transform.DOScale(originalScale, 0.3f).SetEase(Ease.OutBack).SetDelay(i * 0.05f);
                
                currentStockCount++;
            }
        }
        
        Debug.Log($"[TechItemContainer] {gameObject.name} - Restocked all items. Current: {currentStockCount}/{stockItems.Count}");
    }
    
    private void FinishRestocking()
    {
        isRestocking = false;
        restockProgress = 0f;
        
        HideRestockUI();
        
        Debug.Log($"[TechItemContainer] {gameObject.name} - Fully restocked! Stock: {currentStockCount}/{stockItems.Count}");
    }
    
    private void ShowRestockUI()
    {
        Debug.Log($"[TechItemContainer] {gameObject.name} - Showing restock UI");
        
        if (restockCanvas != null)
        {
            restockCanvas.gameObject.SetActive(true);
            restockCanvas.enabled = true;
        }
        
        if (restockRadialImage != null)
        {
            restockRadialImage.gameObject.SetActive(true);
            restockRadialImage.fillAmount = 0f;
            restockRadialImage.color = restockEmptyColor;
            
            // Pulse animation to attract attention
            restockPulseTween?.Kill();
            restockPulseTween = restockRadialImage.transform
                .DOScale(Vector3.one * indicatorPulseMax, indicatorPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }
    
    private void HideRestockUI()
    {
        restockPulseTween?.Kill();
        
        if (restockRadialImage != null)
        {
            restockRadialImage.transform.localScale = Vector3.one;
        }
        
        if (restockCanvas != null)
        {
            restockCanvas.enabled = false;
        }
    }
    
    #endregion
    
    #region UI
    
    private void ShowProgressUI()
    {
        if (progressCanvas != null)
        {
            progressCanvas.enabled = true;
        }
        
        if (radialProgressImage != null)
        {
            radialProgressImage.gameObject.SetActive(true);
        }
    }
    
    private void HideProgressUI()
    {
        if (progressCanvas != null)
        {
            progressCanvas.enabled = false;
        }
    }
    
    /// <summary>
    /// Show the waiting indicator when a customer is waiting
    /// </summary>
    public void ShowWaitingIndicator()
    {
        if (wasCustomerWaiting) return;
        wasCustomerWaiting = true;
        
        Debug.Log($"[TechItemContainer] {gameObject.name} - Showing waiting indicator");
        
        if (progressCanvas != null)
        {
            progressCanvas.gameObject.SetActive(true);
            progressCanvas.enabled = true;
        }
        
        if (radialProgressImage != null)
        {
            radialProgressImage.gameObject.SetActive(true);
            radialProgressImage.fillAmount = 0f;
            radialProgressImage.color = emptyColor;
            
            // Pulse the radial image to attract attention
            indicatorPulseTween?.Kill();
            indicatorPulseTween = radialProgressImage.transform
                .DOScale(Vector3.one * indicatorPulseMax, indicatorPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }
    
    /// <summary>
    /// Hide the waiting indicator
    /// </summary>
    public void HideWaitingIndicator()
    {
        if (!wasCustomerWaiting) return;
        wasCustomerWaiting = false;
        
        Debug.Log($"[TechItemContainer] {gameObject.name} - Hiding waiting indicator");
        
        indicatorPulseTween?.Kill();
        
        if (radialProgressImage != null)
        {
            radialProgressImage.transform.localScale = Vector3.one;
        }
        
        HideProgressUI();
    }
    
    #endregion
    
    #region Animation
    
    // private void StartPulseAnimation()
    // {
    //     if (itemVisual == null) return;
        
    //     pulseTween?.Kill();
        
    //     pulseTween = itemVisual
    //         .DOScale(originalScale * pulseMaxScale, pulseDuration)
    //         .SetEase(pulseEase)
    //         .SetLoops(-1, LoopType.Yoyo);
    // }
    
    // private void StopPulseAnimation()
    // {
    //     pulseTween?.Kill();
        
    //     if (itemVisual != null)
    //     {
    //         itemVisual.DOScale(originalScale, 0.2f);
    //     }
    // }
    
    #endregion
    
    #region Trigger Detection
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[TechItemContainer] {gameObject.name} OnTriggerEnter: {other.gameObject.name}, Layer: {other.gameObject.layer}");
        
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = true;
            playerCharacterController = other.GetComponent<CharacterController>();
            
            if (playerCharacterController == null)
            {
                playerCharacterController = other.GetComponentInParent<CharacterController>();
            }
            
            Debug.Log($"[TechItemContainer] {gameObject.name} - Player entered! CharacterController found: {playerCharacterController != null}");
            Debug.Log($"[TechItemContainer] {gameObject.name} - HasCustomerWaiting: {HasCustomerWaiting}, QueueCount: {QueueCount}");
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            Debug.Log($"[TechItemContainer] {gameObject.name} - Player exited");
            playerInRange = false;
            playerCharacterController = null;
            
            if (isServing)
            {
                PauseServing();
            }
        }
    }
    
    #endregion
    
    private void OnDestroy()
    {
        pulseTween?.Kill();
        indicatorPulseTween?.Kill();
        restockPulseTween?.Kill();
    }
    
    private void OnDrawGizmos()
    {
        // Draw service area - red if needs restock
        Gizmos.color = NeedsRestock ? Color.red : (HasCustomerWaiting ? Color.yellow : (queue.Count > 0 ? Color.blue : Color.green));
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        
        if (customerWaitPoint != null)
        {
            // Draw wait point
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(customerWaitPoint.position, 0.3f);
            Gizmos.DrawLine(transform.position, customerWaitPoint.position);
            
            // Draw queue positions
            Gizmos.color = Color.yellow;
            for (int i = 0; i < maxQueueSize; i++)
            {
                Vector3 pos = GetQueueWorldPosition(i);
                Gizmos.DrawWireSphere(pos, 0.2f);
                if (i > 0)
                {
                    Gizmos.DrawLine(GetQueueWorldPosition(i - 1), pos);
                }
            }
        }
    }
}
