using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

public class StoreUpgrade : MonoBehaviour
{
    [Serializable]
    public class UpgradeLevelDecorations
    {
        [Tooltip("Which upgrade level this corresponds to (0 = first upgrade, 1 = second, etc.)")]
        public int upgradeLevel;
        
        [Tooltip("GameObjects to activate when this upgrade is purchased")]
        public List<GameObject> decorationObjects = new List<GameObject>();
    }
    
    [Header("Configuration")]
    [SerializeField] private Store parentStore;
    [SerializeField] private StoreUpgradeData upgradeData;
    [SerializeField] private UpgradeSpot upgradeSpot;
    
    [Header("Decoration Objects (Scene References)")]
    [Tooltip("Assign decoration GameObjects for each upgrade level")]
    [SerializeField] private List<UpgradeLevelDecorations> upgradeDecorations = new List<UpgradeLevelDecorations>();
    
    [Header("Save Key")]
    [SerializeField] private string saveKeyOverride;
    
    [Header("Animation Settings")]
    [SerializeField] private float popDuration = 0.5f;
    [SerializeField] private Ease popEase = Ease.OutBack;
    [SerializeField] private float popOvershoot = 1.5f;
    [SerializeField] private float staggerDelay = 0.1f;
    
    [Header("Particle Effect")]
    [Tooltip("Particle system prefab to spawn when upgrade decorations appear")]
    [SerializeField] private GameObject upgradeParticleEffectPrefab;
    
    private int currentUpgradeLevel = 0;
    private string SaveKey => string.IsNullOrEmpty(saveKeyOverride) 
        ? $"StoreUpgrade_{upgradeData?.StoreId ?? gameObject.name}" 
        : saveKeyOverride;
    
    public int CurrentUpgradeLevel => currentUpgradeLevel;
    public int MaxUpgradeLevel => upgradeData != null ? upgradeData.MaxUpgradeLevel : 0;
    public bool IsMaxLevel => currentUpgradeLevel >= MaxUpgradeLevel;
    public StoreUpgradeData.UpgradeLevel NextUpgrade => 
        upgradeData?.GetUpgradeLevel(currentUpgradeLevel);
    public string StoreName => parentStore != null ? parentStore.StoreName : upgradeData?.StoreId ?? "Store";
    public string StoreId => upgradeData?.StoreId ?? (parentStore != null ? parentStore.StoreName.Replace(" ", "_") : gameObject.name);
    public string PaymentProgressKey => $"{SaveKey}_PaymentProgress_Level_{currentUpgradeLevel + 1}";
    
    public event Action<int> OnUpgradeCompleted;
    public event Action OnUpgradeAvailable;
    public event Action OnUpgradeUnavailable;
    
    private void Start()
    {
        if (parentStore == null)
        {
            parentStore = GetComponentInParent<Store>();
        }
        
        LoadUpgradeState();
        ApplyAllCompletedUpgrades();
        
        // Subscribe to level up events
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelUp += OnPlayerLevelUp;
        }
        
        // Initial check for upgrade availability
        CheckUpgradeAvailability();
    }
    
    private void OnDestroy()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelUp -= OnPlayerLevelUp;
        }
    }
    
    private void OnPlayerLevelUp(int newLevel)
    {
        CheckUpgradeAvailability();
    }
    
    /// <summary>
    /// Checks if the next upgrade is available and updates the upgrade spot visibility
    /// </summary>
    public void CheckUpgradeAvailability()
    {
        if (upgradeData == null || IsMaxLevel)
        {
            HideUpgradeSpot();
            return;
        }
        
        int playerLevel = LevelManager.Instance != null 
            ? LevelManager.Instance.CurrentLevel 
            : 1;
        
        var nextUpgrade = NextUpgrade;
        if (nextUpgrade != null && playerLevel >= nextUpgrade.requiredPlayerLevel)
        {
            ShowUpgradeSpot();
            OnUpgradeAvailable?.Invoke();
        }
        else
        {
            HideUpgradeSpot();
            OnUpgradeUnavailable?.Invoke();
        }
    }
    
    /// <summary>
    /// Called when upgrade is completed at the upgrade spot
    /// </summary>
    public void CompleteUpgrade()
    {
        if (upgradeData == null || IsMaxLevel) return;
        
        var upgrade = NextUpgrade;
        if (upgrade == null) return;
        
        // Activate decoration objects for this upgrade level
        ActivateDecorationsForLevel(currentUpgradeLevel);

        ClearPaymentProgress();
        currentUpgradeLevel++;
        SaveUpgradeState();
        
        Debug.Log($"[StoreUpgrade] {parentStore?.StoreName ?? gameObject.name} upgraded to level {currentUpgradeLevel}!");
        
        OnUpgradeCompleted?.Invoke(currentUpgradeLevel);
        
        // Check if next upgrade is available
        CheckUpgradeAvailability();
    }
    
    /// <summary>
    /// Activates decoration objects for a specific upgrade level
    /// </summary>
    private void ActivateDecorationsForLevel(int level)
    {
        Debug.Log($"[StoreUpgrade] ActivateDecorationsForLevel({level}) called. Total decoration entries: {upgradeDecorations.Count}");
        
        bool foundMatch = false;
        foreach (var decoration in upgradeDecorations)
        {
            Debug.Log($"[StoreUpgrade] Checking decoration entry with upgradeLevel={decoration.upgradeLevel}, objects count={decoration.decorationObjects.Count}");
            
            if (decoration.upgradeLevel == level)
            {
                foundMatch = true;
                int index = 0;
                foreach (var obj in decoration.decorationObjects)
                {
                    if (obj != null)
                    {
                        Debug.Log($"[StoreUpgrade] Activating decoration: {obj.name}");
                        ActivateWithPopAnimation(obj, index * staggerDelay);
                        index++;
                    }
                    else
                    {
                        Debug.LogWarning($"[StoreUpgrade] Decoration object is NULL!");
                    }
                }
            }
        }
        
        if (!foundMatch)
        {
            Debug.LogWarning($"[StoreUpgrade] No decoration entry found for level {level}!");
        }
    }
    
    /// <summary>
    /// Activates a GameObject with a pop/wobble animation
    /// </summary>
    private void ActivateWithPopAnimation(GameObject obj, float delay = 0f)
    {
        if (obj == null) return;
        
        // Store original scale
        Vector3 originalScale = obj.transform.localScale;
        
        // Start from zero scale
        obj.transform.localScale = Vector3.zero;
        obj.SetActive(true);
        
        // Spawn particle effect at the object's position
        if (upgradeParticleEffectPrefab != null)
        {
            SpawnUpgradeParticleEffect(obj.transform.position, delay);
        }
        
        // Animate to original scale with overshoot
        obj.transform.DOScale(originalScale, popDuration)
            .SetDelay(delay)
            .SetEase(popEase, popOvershoot)
            .OnComplete(() => 
            {
                // Optional: Add a small wobble at the end
                obj.transform.DOPunchRotation(new Vector3(0, 0, 5f), 0.3f, 10, 1f);
            });
    }
    
    /// <summary>
    /// Spawns a one-shot particle effect at the specified position
    /// </summary>
    private void SpawnUpgradeParticleEffect(Vector3 position, float delay = 0f)
    {
        if (upgradeParticleEffectPrefab == null) return;
        
        // Instantiate the particle effect
        GameObject particleObj = Instantiate(upgradeParticleEffectPrefab, position, Quaternion.identity);
        
        // Get the particle system component
        ParticleSystem ps = particleObj.GetComponent<ParticleSystem>();
        
        if (ps != null)
        {
            // Configure for one-shot playback
            var main = ps.main;
            main.loop = false;
            
            // Play after delay if specified
            if (delay > 0f)
            {
                ps.Stop();
                DOVirtual.DelayedCall(delay, () => 
                {
                    if (ps != null)
                    {
                        ps.Play();
                    }
                });
            }
            else
            {
                ps.Play();
            }
            
            // Destroy the particle object after the particle system duration + lifetime
            float totalDuration = main.duration + main.startLifetime.constantMax;
            Destroy(particleObj, totalDuration + delay + 0.5f);
        }
        else
        {
            Debug.LogWarning($"[StoreUpgrade] Particle prefab '{upgradeParticleEffectPrefab.name}' does not have a ParticleSystem component!");
            Destroy(particleObj);
        }
    }
    
    /// <summary>
    /// Gets the current upgrade cost
    /// </summary>
    public int GetCurrentUpgradeCost()
    {
        var upgrade = NextUpgrade;
        return upgrade?.upgradeCost ?? 0;
    }
    
    /// <summary>
    /// Gets the total bonus money per customer from all completed upgrades
    /// </summary>
    public int GetBonusMoneyPerCustomer()
    {
        if (upgradeData == null) return 0;
        
        int bonus = 0;
        for (int i = 0; i < currentUpgradeLevel; i++)
        {
            var upgrade = upgradeData.GetUpgradeLevel(i);
            if (upgrade != null)
            {
                bonus += upgrade.bonusMoneyPerCustomer;
            }
        }
        return bonus;
    }
    
    private void ShowUpgradeSpot()
    {
        if (upgradeSpot != null)
        {
            upgradeSpot.Show(this);
        }
    }
    
    private void HideUpgradeSpot()
    {
        Debug.Log($"[StoreUpgrade] HideUpgradeSpot called. upgradeSpot is {(upgradeSpot != null ? "assigned" : "NULL")}");
        if (upgradeSpot != null)
        {
            upgradeSpot.Hide();
        }
    }
    
    private void ApplyAllCompletedUpgrades()
    {
        // Apply decorations based on current upgrade level
        foreach (var decoration in upgradeDecorations)
        {
            bool shouldBeActive = decoration.upgradeLevel < currentUpgradeLevel;
            
            foreach (var obj in decoration.decorationObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(shouldBeActive);
                }
            }
        }
    }
    
    private void LoadUpgradeState()
    {
        currentUpgradeLevel = PlayerPrefs.GetInt(SaveKey, 0);
    }
    
    private void SaveUpgradeState()
    {
        PlayerPrefs.SetInt(SaveKey, currentUpgradeLevel);
        PlayerPrefs.Save();
    }

    public int LoadPaymentProgress()
    {
        return PaymentProgressStore.Load(PaymentProgressKey, GetCurrentUpgradeCost());
    }

    public void SavePaymentProgress(int amount)
    {
        PaymentProgressStore.Save(PaymentProgressKey, amount, GetCurrentUpgradeCost());
    }

    public void ClearPaymentProgress()
    {
        PaymentProgressStore.Clear(PaymentProgressKey);
    }
    
    /// <summary>
    /// Resets upgrade state (for testing)
    /// </summary>
    [ContextMenu("Reset Upgrade State")]
    public void ResetUpgradeState()
    {
        ClearPaymentProgress();
        PlayerPrefs.DeleteKey(SaveKey);
        currentUpgradeLevel = 0;
        ClearPaymentProgress();
        ApplyAllCompletedUpgrades();
        CheckUpgradeAvailability();
    }
}
