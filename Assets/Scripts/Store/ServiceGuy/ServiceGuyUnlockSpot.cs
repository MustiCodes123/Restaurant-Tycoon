using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// The unlock spot where player stands to pay and unlock a service guy.
/// Linked to a specific store.
/// </summary>
public class ServiceGuyUnlockSpot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask playerLayer;
    
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject visualIcon;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI serviceGuyNameText;
    [SerializeField] private Image progressFillImage;
    
    [Header("Payment Settings")]
    [SerializeField] private float paymentRate = 50f;
    [SerializeField] private float paymentInterval = 0.1f;
    
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
    
    private ServiceGuyUnlock serviceGuyUnlock;
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
        if (canvas != null)
        {
            originalCanvasScale = canvas.transform.localScale;
        }
        
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
        if (!isPlayerInRange || !isPaymentActive || serviceGuyUnlock == null) return;
        
        paymentTimer += Time.deltaTime;
        
        if (paymentTimer >= paymentInterval)
        {
            paymentTimer = 0f;
            ProcessPayment();
        }
    }
    
    private void ProcessPayment()
    {
        if (serviceGuyUnlock == null || CurrencyManager.Instance == null) return;
        
        int remainingCost = totalCost - currentPayment;
        
        if (remainingCost <= 0)
        {
            CompletePayment();
            return;
        }
        
        int paymentAmount = Mathf.CeilToInt(paymentRate * paymentInterval);
        paymentAmount = Mathf.Min(paymentAmount, remainingCost);
        paymentAmount = Mathf.Min(paymentAmount, CurrencyManager.Instance.CurrentMoney);
        
        Debug.Log($"[ServiceGuyUnlockSpot] Processing payment - CurrentMoney: {CurrencyManager.Instance.CurrentMoney}, PaymentAmount: {paymentAmount}, RemainingCost: {remainingCost}");
        
        if (paymentAmount <= 0)
        {
            Debug.LogWarning($"[ServiceGuyUnlockSpot] Payment amount is 0 - Player has no money! CurrentMoney: {CurrencyManager.Instance.CurrentMoney}");
            return;
        }
        
        if (CurrencyManager.Instance.SpendMoney(paymentAmount))
        {
            currentPayment += paymentAmount;
            
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
            
            if (currentPayment >= totalCost)
            {
                CompletePayment();
            }
        }
    }
    
    public void Show(ServiceGuyUnlock unlock)
    {
        serviceGuyUnlock = unlock;
        totalCost = unlock.UnlockCost;
        currentPayment = 0;
        paymentTimer = 0f;
        lastMoneyFlowTime = 0f;
        
        // Ensure clean state when showing
        isPaymentActive = false;
        
        UpdateUI();
        
        if (serviceGuyNameText != null && unlock.UnlockData != null)
        {
            serviceGuyNameText.text = unlock.UnlockData.ServiceGuyName;
        }
        
        gameObject.SetActive(true);
        
        // Register dynamic mission when unlock spot becomes visible
        if (DynamicMissionManager.Instance != null && unlock.UnlockData != null)
        {
            string serviceGuyId = unlock.UnlockData.ServiceGuyName.Replace(" ", "_");
            string storeName = unlock.LinkedStore != null ? unlock.LinkedStore.StoreName : "Store";
            DynamicMissionManager.Instance.RegisterServiceGuyUnlockMission(serviceGuyId, unlock.UnlockData.ServiceGuyName, storeName);
        }
        
        ShowVisualIcon();
        StartPulseAnimation();
        
        Debug.Log($"[ServiceGuyUnlockSpot] Show called - ServiceGuyUnlock set: {serviceGuyUnlock != null}, isPlayerInRange: {isPlayerInRange}, playerTransform: {playerTransform != null}");
    }
    
    public void Hide()
    {
        StopPulseAnimation();
        HideVisualIcon();
        
        // Remove dynamic mission when unlock spot is hidden (if not completed)
        if (DynamicMissionManager.Instance != null && serviceGuyUnlock != null && serviceGuyUnlock.UnlockData != null && !serviceGuyUnlock.IsUnlocked)
        {
            string serviceGuyId = serviceGuyUnlock.UnlockData.ServiceGuyName.Replace(" ", "_");
            DynamicMissionManager.Instance.RemoveServiceGuyUnlockMission(serviceGuyId);
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
            float progress = (float)currentPayment / totalCost;
            progressFillImage.fillAmount = progress;
        }
    }
    
    private void CompletePayment()
    {
        isPaymentActive = false;
        
        // Complete the dynamic mission
        if (DynamicMissionManager.Instance != null && serviceGuyUnlock != null && serviceGuyUnlock.UnlockData != null)
        {
            string serviceGuyId = serviceGuyUnlock.UnlockData.ServiceGuyName.Replace(" ", "_");
            DynamicMissionManager.Instance.CompleteServiceGuyUnlockMission(serviceGuyId);
        }
        
        if (serviceGuyUnlock != null)
        {
            serviceGuyUnlock.CompleteUnlock();
        }
        
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
        if (isPaymentActive || serviceGuyUnlock == null) 
        {
            Debug.LogWarning($"[ServiceGuyUnlockSpot] Cannot start payment - isPaymentActive: {isPaymentActive}, serviceGuyUnlock null: {serviceGuyUnlock == null}");
            return;
        }
        
        playerTransform = player;
        isPlayerInRange = true;
        isPaymentActive = true;
        paymentTimer = 0f;
        lastMoneyFlowTime = Time.time;
        
        StopPulseAnimation();
        
        Debug.Log($"[ServiceGuyUnlockSpot] Payment started! Player in range: {isPlayerInRange}, Payment active: {isPaymentActive}");
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
        
        Debug.Log("[ServiceGuyUnlockSpot] Payment cancelled!");
    }
    
    private void StartPulseAnimation()
    {
        if (visualIcon == null) return;

        pulseTween?.Kill();
        Transform iconT = visualIcon.transform;
        iconT.localScale = Vector3.one * (1f - 0.05f);

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
            visualIcon.transform.localScale = Vector3.zero;
            showHideTween?.Kill();
            showHideTween = visualIcon.transform
                .DOScale(Vector3.one, showDuration)
                .SetEase(showEase);
        }
    }
    
    private void HideVisualIcon()
    {
        StopPulseAnimation();

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
    
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        
        Debug.Log($"[ServiceGuyUnlockSpot] Player entered - CurrentMoney: {(CurrencyManager.Instance != null ? CurrencyManager.Instance.CurrentMoney.ToString() : "CurrencyManager null")}, ServiceGuyUnlock null: {serviceGuyUnlock == null}");
        
        isPlayerInRange = true;
        playerTransform = other.transform;
        
        // Don't start payment automatically - wait for external call to StartPayment()
        StopPulseAnimation();
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        
        Debug.Log($"[ServiceGuyUnlockSpot] Player exited - Was payment active: {isPaymentActive}");
        
        isPlayerInRange = false;
        playerTransform = null;
        
        if (isPaymentActive)
        {
            CancelPayment();
        }
        
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
