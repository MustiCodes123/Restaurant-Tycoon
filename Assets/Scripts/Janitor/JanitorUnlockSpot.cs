using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// The unlock spot where player stands to pay and unlock a janitor.
/// Similar to UnlockSpot for stores.
/// </summary>
public class JanitorUnlockSpot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask playerLayer;
    
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject visualIcon;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI janitorNameText;
    [SerializeField] private Image progressFillImage;
    
    [Header("Payment Settings")]
    [SerializeField] private float paymentRate = 50f; // Money per second
    [SerializeField] private float paymentInterval = 0.1f; // How often to deduct money
    
    [Header("Money Flow Effect")]
    [SerializeField] private MoneyFlowEffect moneyFlowEffect;
    [SerializeField] private float moneyFlowInterval = 0.15f;
    
    [Header("Visual Feedback")]
    [SerializeField] private float pulseScale = 1.1f;
    [SerializeField] private float pulseDuration = 0.5f;
    [SerializeField] private Ease pulseEase = Ease.InOutSine;
    
    [Header("Show/Hide Animation")]
    [SerializeField] private float showDuration = 0.25f;
    [SerializeField] private float hideDuration = 0.2f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;
    
    private JanitorUnlock janitorUnlock;
    private int currentPayment = 0;
    private int totalCost = 0;
    private bool isPlayerInRange = false;
    private bool isPaymentActive = false;
    private float paymentTimer = 0f;
    private float lastMoneyFlowTime = 0f;
    private Tween pulseTween;
    private Tween showHideTween;
    private Transform playerTransform;
    private Vector3 originalCanvasScale;
    
    public bool PlayerInRange => isPlayerInRange;
    public bool IsPaymentActive => isPaymentActive;
    
    private void Awake()
    {
        // Store original canvas scale before hiding
        if (canvas != null)
        {
            originalCanvasScale = canvas.transform.localScale;
        }
        
        // Hide by default
        gameObject.SetActive(false);
    }
    
    private void Start()
    {
        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = 0f;
        }
    }
    
    private void Update()
    {
        if (!isPlayerInRange || !isPaymentActive || janitorUnlock == null) return;
        
        paymentTimer += Time.deltaTime;
        
        if (paymentTimer >= paymentInterval)
        {
            paymentTimer = 0f;
            ProcessPayment();
        }
    }
    
    private void ProcessPayment()
    {
        if (janitorUnlock == null || CurrencyManager.Instance == null) return;
        
        int remainingCost = totalCost - currentPayment;
        
        // Check if already complete
        if (remainingCost <= 0)
        {
            CompletePayment();
            return;
        }
        
        int paymentAmount = Mathf.CeilToInt(paymentRate * paymentInterval);
        paymentAmount = Mathf.Min(paymentAmount, remainingCost);
        paymentAmount = Mathf.Min(paymentAmount, CurrencyManager.Instance.CurrentMoney);
        
        if (paymentAmount <= 0)
        {
            // Player has no money, pause
            return;
        }
        
        // Deduct money from player
        if (CurrencyManager.Instance.SpendMoney(paymentAmount))
        {
            currentPayment += paymentAmount;
            janitorUnlock.SavePaymentProgress(currentPayment);
            
            // Trigger money flow animation
            if (Time.time - lastMoneyFlowTime >= moneyFlowInterval && moneyFlowEffect != null && playerTransform != null)
            {
                moneyFlowEffect.SpawnMoneyToTarget(
                    playerTransform.position,
                    transform.position,
                    paymentAmount
                );
                lastMoneyFlowTime = Time.time;
            }
            
            UpdateUI();
            
            // Check if unlock is complete
            if (currentPayment >= totalCost)
            {
                CompletePayment();
            }
        }
    }
    
    public void Show(JanitorUnlock unlock)
    {
        janitorUnlock = unlock;
        totalCost = unlock.UnlockCost;
        currentPayment = unlock.LoadPaymentProgress();
        paymentTimer = 0f;
        lastMoneyFlowTime = 0f;
        
        // Ensure clean state when showing
        isPaymentActive = false;
        isPlayerInRange = false;
        playerTransform = null;
        
        UpdateUI();
        
        // Update janitor name if we have the text component
        if (janitorNameText != null && unlock.UnlockData != null)
        {
            janitorNameText.text = unlock.UnlockData.JanitorName;
        }
        
        gameObject.SetActive(true);
        
        // Check if player is already standing on this spot using physics overlap
        CheckForPlayerOverlap();
        
        // Show visual icon with animation
        ShowVisualIcon();
        
        // Register dynamic mission when unlock spot becomes visible
        if (DynamicMissionManager.Instance != null && unlock.UnlockData != null)
        {
            string janitorId = unlock.UnlockData.JanitorName.Replace(" ", "_");
            DynamicMissionManager.Instance.RegisterJanitorUnlockMission(janitorId, unlock.UnlockData.JanitorName);
        }
        
        // Start pulse animation
        StartPulseAnimation();
        
        Debug.Log($"[JanitorUnlockSpot] Show called - JanitorUnlock set: {janitorUnlock != null}, isPlayerInRange: {isPlayerInRange}, playerTransform: {playerTransform != null}");
    }
    
    public void Hide()
    {
        StopPulseAnimation();
        HideVisualIcon();
        
        // Remove dynamic mission when unlock spot is hidden (if not completed)
        if (DynamicMissionManager.Instance != null && janitorUnlock != null && janitorUnlock.UnlockData != null && !janitorUnlock.IsUnlocked)
        {
            string janitorId = janitorUnlock.UnlockData.JanitorName.Replace(" ", "_");
            DynamicMissionManager.Instance.RemoveJanitorUnlockMission(janitorId);
        }
        
        gameObject.SetActive(false);
        
        // Reset all payment state to prevent stale data
        isPaymentActive = false;
        isPlayerInRange = false;
        playerTransform = null;
        currentPayment = 0;
        paymentTimer = 0f;
        lastMoneyFlowTime = 0f;
    }
    
    private void UpdateUI()
    {
        int remainingCost = Mathf.Max(0, totalCost - currentPayment);
        
        if (costText != null)
        {
            costText.text = $"${remainingCost}";
        }
        
        if (progressFillImage != null)
        {
            float progress = totalCost > 0 ? (float)currentPayment / totalCost : 0f;
            progressFillImage.fillAmount = progress;
        }
    }
    
    private void CompletePayment()
    {
        isPaymentActive = false;
        
        // Complete the dynamic mission
        if (DynamicMissionManager.Instance != null && janitorUnlock != null && janitorUnlock.UnlockData != null)
        {
            string janitorId = janitorUnlock.UnlockData.JanitorName.Replace(" ", "_");
            DynamicMissionManager.Instance.CompleteJanitorUnlockMission(janitorId);
        }
        
        // Notify the JanitorUnlock component
        if (janitorUnlock != null)
        {
            janitorUnlock.ClearPaymentProgress();
            janitorUnlock.CompleteUnlock();
        }
        
        // Play completion effect
        if (canvas != null)
        {
            canvas.transform.DOPunchScale(originalCanvasScale * 0.05f, 0.3f, 5, 0.5f);
        }
    }
    
    /// <summary>
    /// Starts the payment process when player is ready (called externally)
    /// </summary>
    public void StartPayment(Transform player)
    {
        if (isPaymentActive || janitorUnlock == null) return;
        
        playerTransform = player;
        isPlayerInRange = true;
        isPaymentActive = true;
        paymentTimer = 0f;
        lastMoneyFlowTime = Time.time;
        
        StopPulseAnimation();
        
        Debug.Log("[JanitorUnlockSpot] Payment started!");
    }
    
    /// <summary>
    /// Cancels the payment process when player moves away
    /// </summary>
    public void CancelPayment()
    {
        if (!isPaymentActive) return;
        
        isPaymentActive = false;
        
        if (gameObject.activeInHierarchy)
        {
            StartPulseAnimation();
        }
        
        Debug.Log("[JanitorUnlockSpot] Payment cancelled!");
    }
    
    private void StartPulseAnimation()
    {
        if (visualIcon == null) return;

        pulseTween?.Kill();
        Transform iconT = visualIcon.transform;
        iconT.localScale = Vector3.one * (1f - 0.05f); // Start slightly smaller

        pulseTween = iconT
            .DOScale(pulseScale, pulseDuration)
            .SetEase(pulseEase)
            .SetLoops(-1, LoopType.Yoyo);
    }
    
    private void StopPulseAnimation()
    {
        pulseTween?.Kill();
        
        if (visualIcon != null)
        {
            visualIcon.transform.localScale = Vector3.one;
        }
    }
    
    private void ShowVisualIcon()
    {
        if (visualIcon != null)
        {
            visualIcon.SetActive(true);
            // Start from zero scale and animate in
            visualIcon.transform.localScale = Vector3.zero;
            showHideTween?.Kill();
            showHideTween = visualIcon.transform
                .DOScale(Vector3.one, showDuration)
                .SetEase(showEase);
        }
    }
    
    private void HideVisualIcon()
    {
        // Stop pulse when hiding
        StopPulseAnimation();

        // Animate scale down then disable
        if (visualIcon != null)
        {
            showHideTween?.Kill();
            showHideTween = visualIcon.transform
                .DOScale(Vector3.zero, hideDuration)
                .SetEase(hideEase)
                .OnComplete(() => {
                    if (visualIcon != null)
                    {
                        visualIcon.SetActive(false);
                    }
                });
        }
        
        if (canvas != null)
        {
            canvas.transform.localScale = originalCanvasScale;
        }
    }
    
    /// <summary>
    /// Checks if the player is already overlapping with this spot when it becomes active.
    /// This handles the case where the spot is shown while the player is already standing on it.
    /// </summary>
    private void CheckForPlayerOverlap()
    {
        Collider myCollider = GetComponent<Collider>();
        if (myCollider == null) return;
        
        // Get all colliders overlapping with this spot's bounds
        Collider[] overlappingColliders = Physics.OverlapBox(
            myCollider.bounds.center,
            myCollider.bounds.extents,
            transform.rotation,
            playerLayer
        );
        
        foreach (Collider col in overlappingColliders)
        {
            if (((1 << col.gameObject.layer) & playerLayer) != 0)
            {
                isPlayerInRange = true;
                playerTransform = col.transform;
                StopPulseAnimation();
                Debug.Log($"[JanitorUnlockSpot] Player already overlapping on Show!");
                return;
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        
        Debug.Log($"[JanitorUnlockSpot] Player entered - CurrentMoney: {(CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentMoney.ToString() : "CurrencyManager null")}, JanitorUnlock null: {janitorUnlock == null}");
        
        isPlayerInRange = true;
        playerTransform = other.transform;
        
        // Don't start payment automatically - wait for external call to StartPayment()
        StopPulseAnimation();
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        
        Debug.Log($"[JanitorUnlockSpot] Player exited - Was payment active: {isPaymentActive}");
        
        isPlayerInRange = false;
        playerTransform = null;
        
        if (isPaymentActive)
        {
            CancelPayment();
        }
        
        // Resume pulse when player exits
        if (gameObject.activeInHierarchy)
        {
            StartPulseAnimation();
        }
    }
    
    private void OnDisable()
    {
        StopPulseAnimation();
    }
}
