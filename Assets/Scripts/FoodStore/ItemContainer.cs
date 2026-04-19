using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// A station where the player prepares food items.
/// Player stands in trigger zone to start preparation.
/// Shows radial progress UI and DoTween pulse animation while preparing.
/// </summary>
public class ItemContainer : MonoBehaviour
{
    [Header("References")]
    // [SerializeField] private Transform foodVisual; // The visual that will pulse
    [SerializeField] private LayerMask playerLayer;
    
    [Header("Radial Progress UI")]
    [SerializeField] private Canvas progressCanvas;
    [SerializeField] private Image radialProgressImage;
    [SerializeField] private Color progressColor = Color.green;
    [SerializeField] private Color emptyColor = Color.white;
    
    [Header("Waiting Indicator Animation")]
    [SerializeField] private float indicatorPulseMax = 1.2f;
    [SerializeField] private float indicatorPulseDuration = 0.5f;
    
    [Header("Preparation Settings")]
    [SerializeField] private float preparationDuration = 2f;
    
    // [Header("Pulse Animation")]
    // [SerializeField] private float pulseMinScale = 0.9f;
    // [SerializeField] private float pulseMaxScale = 1.1f;
    // [SerializeField] private float pulseDuration = 0.3f;
    // [SerializeField] private Ease pulseEase = Ease.InOutSine;
    
    private FoodStore parentFoodStore;
    private bool isPrepared = false;
    private bool isPreparing = false;
    private bool playerInRange = false;
    private float preparationProgress = 0f;
    private Vector3 originalScale;
    private Tween pulseTween;
    private Tween indicatorPulseTween;
    private CharacterController playerCharacterController;
    private Vector3 lastPlayerPosition;
    private bool wasCustomerWaiting = false;
    
    private const float MOVEMENT_THRESHOLD = 0.05f;
    
    public bool IsPrepared => isPrepared;
    public bool IsPreparing => isPreparing;
    public float PreparationDuration => preparationDuration;
    
    public event Action<ItemContainer> OnItemPrepared;
    
    // private void Awake()
    // {
    //     if (foodVisual != null)
    //     {
    //         originalScale = foodVisual.localScale;
    //     }
    // }
    
    private void Start()
    {
        HideProgressUI();
    }
    
    public void Initialize(FoodStore foodStore)
    {
        parentFoodStore = foodStore;
        Debug.Log($"[ItemContainer] {gameObject.name} initialized with FoodStore: {foodStore?.name}");
    }
    
    private void Update()
    {
        // Don't process if already prepared
        if (isPrepared) return;
        
        // Check if we have a parent food store
        if (parentFoodStore == null)
        {
            return;
        }
        
        // Check if there's a customer at pickup that can be served
        // (not waiting for seat - which blocks serving next customers)
        if (!parentFoodStore.CanServeCustomer)
        {
            return;
        }
        
        // Check if player is in range
        if (!playerInRange)
        {
            return;
        }
        
        // Check if player has stopped moving
        bool isPlayerStopped = IsPlayerStopped();
        
        if (isPlayerStopped && !isPreparing)
        {
            StartPreparing();
        }
        else if (!isPlayerStopped && isPreparing)
        {
            PausePreparing();
        }
        
        // Update preparation progress
        if (isPreparing)
        {
            UpdatePreparation(Time.deltaTime);
        }
    }
    
    private bool IsPlayerStopped()
    {
        if (playerCharacterController != null)
        {
            // Check velocity from CharacterController
            float speed = playerCharacterController.velocity.magnitude;
            return speed < MOVEMENT_THRESHOLD;
        }
        return true; // Default to true if no controller found
    }
    
    private void StartPreparing()
    {
        isPreparing = true;
        ShowProgressUI();
        // StartPulseAnimation();
        
        // Play preparing sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.FoodPreparing);
        }
        
        Debug.Log($"[ItemContainer] Started preparing {gameObject.name}");
    }
    
    private void PausePreparing()
    {
        isPreparing = false;
        // StopPulseAnimation();
        
        // Stop preparing sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopLoopingSFX();
        }
        
        Debug.Log($"[ItemContainer] Paused preparing {gameObject.name}");
    }
    
    private void UpdatePreparation(float deltaTime)
    {
        preparationProgress += deltaTime;
        
        // Update radial UI
        if (radialProgressImage != null)
        {
            float fillAmount = preparationProgress / preparationDuration;
            radialProgressImage.fillAmount = fillAmount;
            radialProgressImage.color = Color.Lerp(emptyColor, progressColor, fillAmount);
        }
        
        // Check if preparation complete
        if (preparationProgress >= preparationDuration)
        {
            CompletePreparing();
        }
    }
    
    private void CompletePreparing()
    {
        isPrepared = true;
        isPreparing = false;
        
        // StopPulseAnimation();
        HideProgressUI();
        
        // Stop preparing sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopLoopingSFX();
        }
        
        // // Play completion feedback
        // if (foodVisual != null)
        // {
        //     foodVisual.DOPunchScale(Vector3.one * 0.2f, 0.3f, 1, 0.5f);
        // }
        
        Debug.Log($"[ItemContainer] Completed preparing {gameObject.name}");
        
        OnItemPrepared?.Invoke(this);
    }
    
    public void ResetContainer()
    {
        isPrepared = false;
        isPreparing = false;
        preparationProgress = 0f;
        
        // StopPulseAnimation();
        HideProgressUI();
        
        if (radialProgressImage != null)
        {
            radialProgressImage.fillAmount = 0f;
            radialProgressImage.color = emptyColor;
        }
        
        // if (foodVisual != null)
        // {
        //     foodVisual.localScale = originalScale;
        // }
    }
    
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
    
    // private void StartPulseAnimation()
    // {
    //     if (foodVisual == null) return;
        
    //     pulseTween?.Kill();
        
    //     pulseTween = foodVisual
    //         .DOScale(originalScale * pulseMaxScale, pulseDuration)
    //         .SetEase(pulseEase)
    //         .SetLoops(-1, LoopType.Yoyo);
    // }
    
    // private void StopPulseAnimation()
    // {
    //     pulseTween?.Kill();
        
    //     if (foodVisual != null)
    //     {
    //         foodVisual.DOScale(originalScale, 0.2f);
    //     }
    // }
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[ItemContainer] {gameObject.name} OnTriggerEnter: {other.gameObject.name}, Layer: {other.gameObject.layer}");
        
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = true;
            playerCharacterController = other.GetComponent<CharacterController>();
            
            if (playerCharacterController == null)
            {
                // Try to get from parent
                playerCharacterController = other.GetComponentInParent<CharacterController>();
            }
            
            if (playerCharacterController != null)
            {
                lastPlayerPosition = playerCharacterController.transform.position;
            }
            
            Debug.Log($"[ItemContainer] {gameObject.name} - Player entered! CharacterController found: {playerCharacterController != null}");
            Debug.Log($"[ItemContainer] {gameObject.name} - CanServeCustomer: {parentFoodStore?.CanServeCustomer}, IsPrepared: {isPrepared}");
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            Debug.Log($"[ItemContainer] {gameObject.name} - Player exited");
            playerInRange = false;
            playerCharacterController = null;
            
            if (isPreparing)
            {
                PausePreparing();
            }
        }
    }
    
    /// <summary>
    /// Show the waiting indicator when a customer is waiting for food.
    /// Shows the radial progress UI so player knows to come here.
    /// </summary>
    public void ShowWaitingIndicator()
    {
        if (wasCustomerWaiting) return;
        wasCustomerWaiting = true;
        
        Debug.Log($"[ItemContainer] {gameObject.name} - Showing waiting indicator (progress canvas)");
        
        // Show the progress canvas as the waiting indicator
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
    /// Hide the waiting indicator when customer leaves or item is prepared
    /// </summary>
    public void HideWaitingIndicator()
    {
        if (!wasCustomerWaiting) return;
        wasCustomerWaiting = false;
        
        Debug.Log($"[ItemContainer] {gameObject.name} - Hiding waiting indicator");
        
        indicatorPulseTween?.Kill();
        
        if (radialProgressImage != null)
        {
            radialProgressImage.transform.localScale = Vector3.one;
        }
        
        // Hide progress UI
        HideProgressUI();
    }
    
    private void OnDestroy()
    {
        pulseTween?.Kill();
        indicatorPulseTween?.Kill();
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = isPrepared ? Color.green : (isPreparing ? Color.yellow : Color.red);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
