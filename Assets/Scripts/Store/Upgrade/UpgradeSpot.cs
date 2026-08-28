using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class UpgradeSpot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Canvas worldCanvas;
    
    [Header("UI Elements")]
    [SerializeField] private GameObject upgradeUIRoot;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image progressFillImage;
    
    [Header("Money Flow Effect")]
    [SerializeField] private MoneyFlowEffect moneyFlowEffect;
    [SerializeField] private Transform moneyTargetPoint;
    
    [Header("Animation Settings")]
    [SerializeField] private float showDuration = 0.3f;
    [SerializeField] private float hideDuration = 0.2f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;
    
    [Header("Upgrade Settings")]
    [SerializeField] private float paymentRate = 50f; // Money paid per second while standing
    [SerializeField] private float paymentInterval = 0.1f; // How often to deduct money
    
    [Header("Pulse Animation")]
    [SerializeField] private float pulseMinScale = 0.9f;
    [SerializeField] private float pulseMaxScale = 1.1f;
    [SerializeField] private float pulseDuration = 0.6f;
    
    private StoreUpgrade currentStoreUpgrade;
    private bool isVisible = false;
    private bool playerInRange = false;
    private bool isUpgrading = false;
    
    private int totalUpgradeCost;
    private int remainingCost;
    private int currentTargetLevel; // Target level for current upgrade (used for mission tracking)
    private float paymentTimer = 0f;
    
    private Tween showHideTween;
    private Tween pulseTween;
    private Transform playerTransform;
    
    public bool IsUpgrading => isUpgrading;
    public bool PlayerInRange => playerInRange;
    public Transform MoneyTargetPoint => moneyTargetPoint != null ? moneyTargetPoint : transform;
    
    public event Action OnUpgradeStarted;
    public event Action OnUpgradeCompleted;
    public event Action OnUpgradeCancelled;
    public event Action<int> OnPaymentMade; // Amount paid this tick
    
    private void Awake()
    {
        // Start with everything hidden
        gameObject.SetActive(false);
        isVisible = false;
        
        if (upgradeUIRoot != null)
        {
            upgradeUIRoot.SetActive(false);
        }
        
        if (worldCanvas != null)
        {
            worldCanvas.enabled = false;
        }
        
        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = 0f;
        }
    }
    
    private void Update()
    {
        if (!isUpgrading || !playerInRange) return;
        
        paymentTimer += Time.deltaTime;
        
        if (paymentTimer >= paymentInterval)
        {
            paymentTimer = 0f;
            ProcessPayment();
        }
    }
    
    private void ProcessPayment()
    {
        if (currentStoreUpgrade == null || CurrencyManager.Instance == null) return;
        
        int paymentAmount = Mathf.CeilToInt(paymentRate * paymentInterval);
        paymentAmount = Mathf.Min(paymentAmount, remainingCost);
        paymentAmount = Mathf.Min(paymentAmount, CurrencyManager.Instance.CurrentMoney);
        
        if (paymentAmount <= 0)
        {
            // Player has no money, pause the upgrade
            return;
        }
        
        // Deduct money from player
        if (CurrencyManager.Instance.SpendMoney(paymentAmount))
        {
            remainingCost -= paymentAmount;
            currentStoreUpgrade.SavePaymentProgress(totalUpgradeCost - remainingCost);
            
            // Trigger money flow animation from player to upgrade spot
            if (moneyFlowEffect != null && playerTransform != null)
            {
                moneyFlowEffect.SpawnMoneyToTarget(playerTransform.position, MoneyTargetPoint.position, paymentAmount);
            }
            
            OnPaymentMade?.Invoke(paymentAmount);
            
            // Update UI
            UpdateUI();
            
            // Check if upgrade is complete
            if (remainingCost <= 0)
            {
                CompleteUpgrade();
            }
        }
    }
    
    private void UpdateUI()
    {
        if (costText != null)
        {
            costText.text = $"${remainingCost}";
        }
        
        if (progressFillImage != null)
        {
            float progress = 1f - ((float)remainingCost / totalUpgradeCost);
            progressFillImage.fillAmount = progress;
        }
    }
    
    /// <summary>
    /// Shows the upgrade spot for a specific store upgrade
    /// </summary>
    public void Show(StoreUpgrade storeUpgrade)
    {
        if (isVisible && currentStoreUpgrade == storeUpgrade) return;
        
        // Clean up any leftover money prefabs from previous upgrade
        if (moneyFlowEffect != null)
        {
            moneyFlowEffect.ForceCleanup();
        }
        
        currentStoreUpgrade = storeUpgrade;
        
        // Setup upgrade info
        var nextUpgrade = storeUpgrade.NextUpgrade;
        if (nextUpgrade != null)
        {
            totalUpgradeCost = nextUpgrade.upgradeCost;
            int savedPayment = storeUpgrade.LoadPaymentProgress();
            remainingCost = Mathf.Max(0, totalUpgradeCost - savedPayment);
            
            if (levelText != null)
            {
                levelText.text = $"Lvl {storeUpgrade.CurrentUpgradeLevel + 1}";
            }
            
            UpdateUI();
            
            // Store target level for mission tracking
            currentTargetLevel = storeUpgrade.CurrentUpgradeLevel + 1;
            
            // Register dynamic mission when upgrade spot becomes visible
            if (DynamicMissionManager.Instance != null)
            {
                DynamicMissionManager.Instance.RegisterStoreUpgradeMission(
                    storeUpgrade.StoreId, 
                    storeUpgrade.StoreName, 
                    currentTargetLevel
                );
            }
        }
        
        isVisible = true;
        gameObject.SetActive(true);
        
        if (worldCanvas != null)
        {
            worldCanvas.enabled = true;
        }
        
        if (upgradeUIRoot != null)
        {
            upgradeUIRoot.SetActive(true);
            upgradeUIRoot.transform.localScale = Vector3.zero;
            showHideTween?.Kill();
            showHideTween = upgradeUIRoot.transform
                .DOScale(Vector3.one, showDuration)
                .SetEase(showEase)
                .OnComplete(() => StartPulseAnimation());
        }
        
        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = 0f;
        }
    }
    
    /// <summary>
    /// Hides the upgrade spot
    /// </summary>
    public void Hide()
    {
        if (!isVisible) return;
        
        // Clean up any leftover money prefabs
        if (moneyFlowEffect != null)
        {
            moneyFlowEffect.ForceCleanup();
        }
        
        StopPulseAnimation();
        
        // Remove dynamic mission if upgrade was not completed
        if (DynamicMissionManager.Instance != null && currentStoreUpgrade != null && !isUpgrading)
        {
            DynamicMissionManager.Instance.RemoveStoreUpgradeMission(currentStoreUpgrade.StoreId, currentTargetLevel);
        }
        
        if (upgradeUIRoot != null)
        {
            showHideTween?.Kill();
            showHideTween = upgradeUIRoot.transform
                .DOScale(Vector3.zero, hideDuration)
                .SetEase(hideEase)
                .OnComplete(() =>
                {
                    upgradeUIRoot.SetActive(false);
                    if (worldCanvas != null)
                    {
                        worldCanvas.enabled = false;
                    }
                    gameObject.SetActive(false);
                });
        }
        else
        {
            gameObject.SetActive(false);
        }
        
        isVisible = false;
        currentStoreUpgrade = null;
    }
    
    /// <summary>
    /// Starts the upgrade process when player enters
    /// </summary>
    public void StartUpgrade(Transform player)
    {
        if (isUpgrading || currentStoreUpgrade == null) return;
        
        playerTransform = player;
        isUpgrading = true;
        paymentTimer = 0f;
        
        StopPulseAnimation();
        OnUpgradeStarted?.Invoke();
        
        Debug.Log("[UpgradeSpot] Upgrade started!");
    }
    
    /// <summary>
    /// Cancels the upgrade process when player exits
    /// </summary>
    public void CancelUpgrade()
    {
        if (!isUpgrading) return;
        
        isUpgrading = false;
        playerTransform = null;
        
        StartPulseAnimation();
        OnUpgradeCancelled?.Invoke();
        
        Debug.Log("[UpgradeSpot] Upgrade cancelled!");
    }
    
    private void CompleteUpgrade()
    {
        isUpgrading = false;
        
        // Clean up any leftover money prefabs
        if (moneyFlowEffect != null)
        {
            moneyFlowEffect.ForceCleanup();
        }
        
        // Complete the dynamic mission
        if (DynamicMissionManager.Instance != null && currentStoreUpgrade != null)
        {
            DynamicMissionManager.Instance.CompleteStoreUpgradeMission(currentStoreUpgrade.StoreId, currentTargetLevel);
        }
        
        OnUpgradeCompleted?.Invoke();
        
        // Notify the store upgrade component
        if (currentStoreUpgrade != null)
        {
            currentStoreUpgrade.CompleteUpgrade();
        }
        
        Debug.Log("[UpgradeSpot] Upgrade completed!");
        
        // Hide will be called by StoreUpgrade.CheckUpgradeAvailability
    }
    
    private void StartPulseAnimation()
    {
        if (upgradeUIRoot == null) return;
        
        pulseTween?.Kill();
        upgradeUIRoot.transform.localScale = Vector3.one;
        
        pulseTween = upgradeUIRoot.transform
            .DOScale(Vector3.one * pulseMaxScale, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }
    
    private void StopPulseAnimation()
    {
        pulseTween?.Kill();
        if (upgradeUIRoot != null)
        {
            upgradeUIRoot.transform.localScale = Vector3.one;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = true;
            playerTransform = other.transform;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = false;
            if (isUpgrading)
            {
                CancelUpgrade();
            }
        }
    }
    
    private void OnDestroy()
    {
        showHideTween?.Kill();
        pulseTween?.Kill();
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = isVisible ? Color.yellow : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        if (moneyTargetPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(moneyTargetPoint.position, 0.2f);
            Gizmos.DrawLine(transform.position, moneyTargetPoint.position);
        }
    }
}
