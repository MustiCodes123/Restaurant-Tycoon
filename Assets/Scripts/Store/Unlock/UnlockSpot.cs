using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UnlockSpot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask playerLayer;
    
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI storeNameText;
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
    
    private StoreUnlock storeUnlock;
    private int currentPayment = 0;
    private int totalCost = 0;
    private bool isPlayerInRange = false;
    private bool isPaymentActive = false;
    private float paymentTimer = 0f;
    private float lastMoneyFlowTime = 0f;
    private Tween pulseTween;
    private Transform playerTransform;
    private Vector3 originalCanvasScale;
    
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
        if (!isPlayerInRange || !isPaymentActive || storeUnlock == null) return;
        
        paymentTimer += Time.deltaTime;
        
        if (paymentTimer >= paymentInterval)
        {
            paymentTimer = 0f;
            ProcessPayment();
        }
    }
    
    private void ProcessPayment()
    {
        if (storeUnlock == null || CurrencyManager.Instance == null) return;
        
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
    
    public void Show(StoreUnlock unlock)
    {
        storeUnlock = unlock;
        totalCost = unlock.UnlockCost;
        currentPayment = 0;
        paymentTimer = 0f;
        
        UpdateUI();
        
        // Update store name if we have the text component
        if (storeNameText != null && unlock.UnlockData != null)
        {
            storeNameText.text = unlock.UnlockData.StoreName;
        }
        
        gameObject.SetActive(true);
        
        // Register dynamic mission when unlock spot becomes visible
        if (DynamicMissionManager.Instance != null && unlock.UnlockData != null)
        {
            string storeId = unlock.UnlockData.StoreName.Replace(" ", "_");
            DynamicMissionManager.Instance.RegisterStoreUnlockMission(storeId, unlock.UnlockData.StoreName);
        }
        
        // Start pulse animation
        StartPulseAnimation();
    }
    
    public void Hide()
    {
        StopPulseAnimation();
        
        // Remove dynamic mission when unlock spot is hidden (if not completed)
        if (DynamicMissionManager.Instance != null && storeUnlock != null && storeUnlock.UnlockData != null && !storeUnlock.IsUnlocked)
        {
            string storeId = storeUnlock.UnlockData.StoreName.Replace(" ", "_");
            DynamicMissionManager.Instance.RemoveStoreUnlockMission(storeId);
        }
        
        gameObject.SetActive(false);
        isPaymentActive = false;
        isPlayerInRange = false;
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
        if (DynamicMissionManager.Instance != null && storeUnlock != null && storeUnlock.UnlockData != null)
        {
            string storeId = storeUnlock.UnlockData.StoreName.Replace(" ", "_");
            DynamicMissionManager.Instance.CompleteStoreUnlockMission(storeId);
        }
        
        // Notify the StoreUnlock component
        if (storeUnlock != null)
        {
            storeUnlock.CompleteUnlock();
        }
        
        // Play completion effect
        if (canvas != null)
        {
            canvas.transform.DOPunchScale(originalCanvasScale * 0.05f, 0.3f, 5, 0.5f);
        }
    }
    
    private void StartPulseAnimation()
    {
        StopPulseAnimation();
        
        if (canvas != null)
        {
            Vector3 pulseTargetScale = originalCanvasScale * (1f + 0.05f); // 5% larger
            pulseTween = canvas.transform
                .DOScale(pulseTargetScale, pulseDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
    }
    
    private void StopPulseAnimation()
    {
        if (pulseTween != null)
        {
            pulseTween.Kill();
            pulseTween = null;
        }
        
        if (canvas != null)
        {
            canvas.transform.localScale = originalCanvasScale;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        
        isPlayerInRange = true;
        isPaymentActive = true;
        paymentTimer = 0f;
        lastMoneyFlowTime = Time.time;
        playerTransform = other.transform;
        
        // Stop pulse when player enters
        StopPulseAnimation();
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        
        isPlayerInRange = false;
        isPaymentActive = false;
        playerTransform = null;
        
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
