using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Payment spot for unlocking a washroom. Similar to UnlockSpot for stores.
/// </summary>
public class WashroomUnlockSpot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask playerLayer;
    
    [Header("UI References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI washroomNameText;
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
    
    private WashroomUnlock washroomUnlock;
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
        if (!isPlayerInRange || !isPaymentActive || washroomUnlock == null) return;
        
        paymentTimer += Time.deltaTime;
        
        if (paymentTimer >= paymentInterval)
        {
            paymentTimer = 0f;
            ProcessPayment();
        }
    }
    
    private void ProcessPayment()
    {
        if (washroomUnlock == null || CurrencyManager.Instance == null) return;
        
        int remainingCost = totalCost - currentPayment;
        
        if (remainingCost <= 0)
        {
            CompletePayment();
            return;
        }
        
        int paymentAmount = Mathf.CeilToInt(paymentRate * paymentInterval);
        paymentAmount = Mathf.Min(paymentAmount, remainingCost);
        paymentAmount = Mathf.Min(paymentAmount, CurrencyManager.Instance.CurrentMoney);
        
        if (paymentAmount <= 0) return;
        
        if (CurrencyManager.Instance.SpendMoney(paymentAmount))
        {
            currentPayment += paymentAmount;
            washroomUnlock.SavePaymentProgress(currentPayment);
            
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
    
    public void Show(WashroomUnlock unlock)
    {
        washroomUnlock = unlock;
        totalCost = unlock.UnlockCost;
        currentPayment = unlock.LoadPaymentProgress();
        paymentTimer = 0f;
        
        UpdateUI();
        
        if (washroomNameText != null && unlock.UnlockData != null)
        {
            washroomNameText.text = unlock.UnlockData.WashroomName;
        }
        
        gameObject.SetActive(true);
        
        // Register dynamic mission
        if (DynamicMissionManager.Instance != null && unlock.UnlockData != null)
        {
            string washroomId = unlock.UnlockData.WashroomName.Replace(" ", "_");
            DynamicMissionManager.Instance.RegisterWashroomUnlockMission(washroomId, unlock.UnlockData.WashroomName);
        }
        
        StartPulseAnimation();
    }
    
    public void Hide()
    {
        StopPulseAnimation();
        
        if (DynamicMissionManager.Instance != null && washroomUnlock != null && washroomUnlock.UnlockData != null && !washroomUnlock.IsUnlocked)
        {
            string washroomId = washroomUnlock.UnlockData.WashroomName.Replace(" ", "_");
            DynamicMissionManager.Instance.RemoveWashroomUnlockMission(washroomId);
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
            float progress = totalCost > 0 ? (float)currentPayment / totalCost : 0f;
            progressFillImage.fillAmount = progress;
        }
    }
    
    private void CompletePayment()
    {
        isPaymentActive = false;
        
        if (DynamicMissionManager.Instance != null && washroomUnlock != null && washroomUnlock.UnlockData != null)
        {
            string washroomId = washroomUnlock.UnlockData.WashroomName.Replace(" ", "_");
            DynamicMissionManager.Instance.CompleteWashroomUnlockMission(washroomId);
        }
        
        if (washroomUnlock != null)
        {
            washroomUnlock.ClearPaymentProgress();
            washroomUnlock.CompleteUnlock();
        }
        
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
            Vector3 pulseTargetScale = originalCanvasScale * (1f + 0.05f);
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
        
        StopPulseAnimation();
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        
        isPlayerInRange = false;
        isPaymentActive = false;
        playerTransform = null;
        
        if (gameObject.activeInHierarchy)
        {
            StartPulseAnimation();
        }
    }
}
