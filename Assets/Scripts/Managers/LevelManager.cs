using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    
    [Header("Level Data")]
    [SerializeField] private List<LevelData> allLevels = new List<LevelData>();
    
    [Header("UI Panels")]
    [SerializeField] private GameObject missionsPanelObject;
    [SerializeField] private GameObject levelCompletePanelObject;
    
    [Header("UI Scripts")]
    [SerializeField] private LevelPanelUI levelPanelUI;
    [SerializeField] private LevelCompleteUI levelCompleteUI;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private Button debugCompleteLevelButton;

    [SerializeField] private TextMeshProUGUI levelText;
    
    private LevelData currentLevelData;
    private int currentLevelIndex;
    private bool allLevelsCompleted = false;
    private bool isCompletingLevel = false; // Prevents multiple level completions
    
    // Baseline values captured when level starts (for level-based progress tracking)
    private int baselineEarnings;
    private int baselineTotalCustomersServed;
    private Dictionary<string, int> baselineCustomersServedAtStores = new Dictionary<string, int>();
    private string LevelBaselinePrefix => $"Level_{CurrentLevel}_Baseline_";
    
    public event Action<int> OnLevelUp;
    public event Action OnMissionProgressUpdated;
    
    public int CurrentLevel => DataManager.Instance != null ? DataManager.Instance.CurrentLevel : 1;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        // Enable panels (they can be disabled in hierarchy)
        if (missionsPanelObject != null)
            missionsPanelObject.SetActive(true);
        
        if (levelCompletePanelObject != null)
            levelCompletePanelObject.SetActive(true);
        
        // Initialize UI scripts after enabling
        if (levelPanelUI != null)
            levelPanelUI.Initialize();
        
        if (levelCompleteUI != null)
            levelCompleteUI.Initialize();
        
        // Setup debug button
        if (debugCompleteLevelButton != null)
        {
            debugCompleteLevelButton.onClick.AddListener(DebugCompleteLevel);
        }
        
        LoadCurrentLevel();
    }
    
    private void OnDestroy()
    {
        if (debugCompleteLevelButton != null)
        {
            debugCompleteLevelButton.onClick.RemoveListener(DebugCompleteLevel);
        }
    }
    
    private void LoadCurrentLevel()
    {
        // Reset the completing flag
        isCompletingLevel = false;
        
        int savedLevel = DataManager.Instance != null ? DataManager.Instance.CurrentLevel : 1;
        currentLevelIndex = savedLevel - 1;
        
        // Capture baseline values when level starts
        CaptureBaselineValues();
        
        if (currentLevelIndex >= 0 && currentLevelIndex < allLevels.Count)
        {
            allLevelsCompleted = false;
            currentLevelData = allLevels[currentLevelIndex];
            
            if (levelPanelUI != null)
                levelPanelUI.ShowLevel(currentLevelData, savedLevel);
            
            if (levelText != null)
                levelText.text = $"Level {savedLevel}";
            
            Log($"Loaded Level {savedLevel}: {currentLevelData.levelName}");
        }
        else
        {
            allLevelsCompleted = true;
            currentLevelData = null;
            if (levelText != null)
                levelText.text = "Complete";
            Log("All levels completed or no levels configured!");
        }
    }
    
    public void RegisterCustomerServedAtStore(string storeName)
    {
        if (DataManager.Instance == null) return;
        
        DataManager.Instance.TotalCustomersServed++;
        DataManager.Instance.IncrementCustomersServedAtStore(storeName);
        Log($"Customer served at {storeName}! Total: {DataManager.Instance.TotalCustomersServed}, At {storeName}: {DataManager.Instance.GetCustomersServedAtStore(storeName)}");
        
        UpdateMissionProgress();
    }
    
    // Keep for backwards compatibility
    public void RegisterCustomerServed()
    {
        if (DataManager.Instance == null) return;
        
        DataManager.Instance.TotalCustomersServed++;
        Log($"Customer served! Total: {DataManager.Instance.TotalCustomersServed}");
        
        UpdateMissionProgress();
    }
    
    public void RegisterMoneyEarned(int amount)
    {
        if (DataManager.Instance == null) return;
        
        DataManager.Instance.AddMoney(amount);
        Log($"Money earned: ${amount}. Total earnings: ${DataManager.Instance.TotalEarnings}");
        
        UpdateMissionProgress();
    }
    
    private void UpdateMissionProgress()
    {
        // Don't process if all levels are completed or currently completing a level
        if (allLevelsCompleted || currentLevelData == null || isCompletingLevel)
            return;
        
        OnMissionProgressUpdated?.Invoke();
        
        if (levelPanelUI != null)
        {
            levelPanelUI.UpdateAllMissions();
            
            if (levelPanelUI.AreAllMissionsCompleted())
            {
                CompleteLevel();
            }
        }
    }
    
    /// <summary>
    /// Public method to check mission progress (called by dynamic missions)
    /// </summary>
    public void CheckMissionProgress()
    {
        UpdateMissionProgress();
    }
    
    private void CompleteLevel()
    {
        // Prevent re-entry while completing
        if (isCompletingLevel) return;
        isCompletingLevel = true;
        
        int completedLevel = CurrentLevel;
        Log($"Level {completedLevel} Complete!");
        
        // Clear current level data immediately to prevent re-processing
        currentLevelData = null;
        
        // Advance to next level
        if (DataManager.Instance != null)
            DataManager.Instance.CurrentLevel++;
        ClearBaselineValues(completedLevel);
        
        OnLevelUp?.Invoke(CurrentLevel);
        
        // Close the missions panel if open
        if (levelPanelUI != null)
        {
            levelPanelUI.Hide();
        }
        
        // Show level complete popup
        if (levelCompleteUI != null)
        {
            levelCompleteUI.ShowLevelComplete(completedLevel, () =>
            {
                // After popup closes, load next level
                LoadCurrentLevel();
            });
        }
        else
        {
            // If no popup UI, just load next level
            LoadCurrentLevel();
        }
    }
    
    private void CaptureBaselineValues()
    {
        if (DataManager.Instance == null) return;
        
        baselineEarnings = LoadOrCreateBaseline("Earnings", DataManager.Instance.TotalEarnings);
        baselineTotalCustomersServed = LoadOrCreateBaseline("TotalCustomersServed", DataManager.Instance.TotalCustomersServed);
        baselineCustomersServedAtStores.Clear();
        
        Log($"Captured baselines - Earnings: {baselineEarnings}, Customers: {baselineTotalCustomersServed}");
    }
    
    /// <summary>
    /// Gets level-based earnings (earned since level started)
    /// </summary>
    public int GetLevelEarnings()
    {
        if (DataManager.Instance == null) return 0;
        return DataManager.Instance.TotalEarnings - baselineEarnings;
    }
    
    /// <summary>
    /// Gets level-based total customers served (served since level started)
    /// </summary>
    public int GetLevelCustomersServed()
    {
        if (DataManager.Instance == null) return 0;
        return DataManager.Instance.TotalCustomersServed - baselineTotalCustomersServed;
    }
    
    /// <summary>
    /// Gets level-based customers served at a specific store (served since level started)
    /// </summary>
    public int GetLevelCustomersServedAtStore(string storeName)
    {
        if (DataManager.Instance == null) return 0;
        
        int baseline = 0;
        if (!baselineCustomersServedAtStores.TryGetValue(storeName, out baseline))
        {
            // First time checking this store in this level, capture baseline now
            baseline = LoadOrCreateBaseline($"Store_{storeName}", DataManager.Instance.GetCustomersServedAtStore(storeName));
            baselineCustomersServedAtStores[storeName] = baseline;
        }
        
        return DataManager.Instance.GetCustomersServedAtStore(storeName) - baseline;
    }
    
    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[LevelManager] {message}");
    }

    private int LoadOrCreateBaseline(string keySuffix, int currentValue)
    {
        string key = LevelBaselinePrefix + keySuffix;
        if (PlayerPrefs.HasKey(key))
        {
            return PlayerPrefs.GetInt(key, currentValue);
        }

        PlayerPrefs.SetInt(key, currentValue);
        PlayerPrefs.Save();
        return currentValue;
    }

    private void ClearBaselineValues(int level)
    {
        string prefix = $"Level_{level}_Baseline_";
        PlayerPrefs.DeleteKey(prefix + "Earnings");
        PlayerPrefs.DeleteKey(prefix + "TotalCustomersServed");

        foreach (string storeName in baselineCustomersServedAtStores.Keys)
        {
            PlayerPrefs.DeleteKey(prefix + $"Store_{storeName}");
        }

        PlayerPrefs.Save();
    }
    
    #region Debug Methods
    
    /// <summary>
    /// Debug method to auto-complete the current level.
    /// Simulates actual gameplay: deducts costs for unlocks/upgrades, adds money for earnings/customers.
    /// </summary>
    [ContextMenu("Debug Complete Level")]
    public void DebugCompleteLevel()
    {
        if (allLevelsCompleted || currentLevelData == null)
        {
            Log("Cannot debug complete - no active level!");
            return;
        }
        
        Log("=== DEBUG: Auto-completing level (simulating gameplay) ===");
        
        // First, complete all dynamic missions (unlocks/upgrades) - these cost money
        DebugCompleteDynamicMissions();
        
        // Then, complete regular missions - these may add money
        if (currentLevelData.missions != null)
        {
            foreach (var mission in currentLevelData.missions)
            {
                if (mission == null) continue;
                DebugCompleteMission(mission);
            }
        }
        
        // Trigger level completion check
        UpdateMissionProgress();
    }
    
    private void DebugCompleteDynamicMissions()
    {
        // Find and complete all active unlock/upgrade spots in the scene
        
        // 1. Store Unlocks
        var storeUnlocks = FindObjectsByType<StoreUnlock>(FindObjectsSortMode.None);
        foreach (var storeUnlock in storeUnlocks)
        {
            if (!storeUnlock.IsUnlocked && storeUnlock.gameObject.activeInHierarchy)
            {
                // Check if unlock spot is active (meaning it's available)
                var unlockSpot = storeUnlock.GetComponentInChildren<UnlockSpot>(true);
                if (unlockSpot != null && unlockSpot.gameObject.activeInHierarchy)
                {
                    int cost = storeUnlock.UnlockCost;
                    string storeName = storeUnlock.UnlockData?.StoreName ?? "Store";
                    
                    // Deduct cost from player money
                    if (CurrencyManager.Instance != null)
                    {
                        CurrencyManager.Instance.SpendMoney(cost);
                        Log($"Debug: Spent ${cost} to unlock {storeName}");
                    }
                    
                    // Complete the unlock
                    storeUnlock.CompleteUnlock();
                }
            }
        }
        
        // 2. Store Upgrades
        var storeUpgrades = FindObjectsByType<StoreUpgrade>(FindObjectsSortMode.None);
        foreach (var storeUpgrade in storeUpgrades)
        {
            if (!storeUpgrade.IsMaxLevel && storeUpgrade.gameObject.activeInHierarchy)
            {
                var upgradeSpot = storeUpgrade.GetComponentInChildren<UpgradeSpot>(true);
                if (upgradeSpot != null && upgradeSpot.gameObject.activeInHierarchy)
                {
                    var nextUpgrade = storeUpgrade.NextUpgrade;
                    if (nextUpgrade != null)
                    {
                        int cost = nextUpgrade.upgradeCost;
                        string storeName = storeUpgrade.StoreName;
                        
                        // Deduct cost from player money
                        if (CurrencyManager.Instance != null)
                        {
                            CurrencyManager.Instance.SpendMoney(cost);
                            Log($"Debug: Spent ${cost} to upgrade {storeName}");
                        }
                        
                        // Complete the upgrade
                        storeUpgrade.CompleteUpgrade();
                    }
                }
            }
        }
        
        // 3. Janitor Unlocks
        var janitorUnlocks = FindObjectsByType<JanitorUnlock>(FindObjectsSortMode.None);
        foreach (var janitorUnlock in janitorUnlocks)
        {
            if (!janitorUnlock.IsUnlocked && janitorUnlock.gameObject.activeInHierarchy)
            {
                var unlockSpot = janitorUnlock.GetComponentInChildren<JanitorUnlockSpot>(true);
                if (unlockSpot != null && unlockSpot.gameObject.activeInHierarchy)
                {
                    int cost = janitorUnlock.UnlockCost;
                    string janitorName = janitorUnlock.UnlockData?.JanitorName ?? "Janitor";
                    
                    // Deduct cost from player money
                    if (CurrencyManager.Instance != null)
                    {
                        CurrencyManager.Instance.SpendMoney(cost);
                        Log($"Debug: Spent ${cost} to unlock {janitorName}");
                    }
                    
                    // Complete the unlock
                    janitorUnlock.CompleteUnlock();
                }
            }
        }
        
        // 4. Service Guy Unlocks
        var serviceGuyUnlocks = FindObjectsByType<ServiceGuyUnlock>(FindObjectsSortMode.None);
        foreach (var serviceGuyUnlock in serviceGuyUnlocks)
        {
            if (!serviceGuyUnlock.IsUnlocked && serviceGuyUnlock.gameObject.activeInHierarchy)
            {
                var unlockSpot = serviceGuyUnlock.GetComponentInChildren<ServiceGuyUnlockSpot>(true);
                if (unlockSpot != null && unlockSpot.gameObject.activeInHierarchy)
                {
                    int cost = serviceGuyUnlock.UnlockCost;
                    string serviceGuyName = serviceGuyUnlock.UnlockData?.ServiceGuyName ?? "Service Guy";
                    string storeName = serviceGuyUnlock.LinkedStore?.StoreName ?? "Store";
                    
                    // Deduct cost from player money
                    if (CurrencyManager.Instance != null)
                    {
                        CurrencyManager.Instance.SpendMoney(cost);
                        Log($"Debug: Spent ${cost} to unlock {serviceGuyName} for {storeName}");
                    }
                    
                    // Complete the unlock
                    serviceGuyUnlock.CompleteUnlock();
                }
            }
        }
    }
    
    private void DebugCompleteMission(MissionData mission)
    {
        if (DataManager.Instance == null || CurrencyManager.Instance == null) return;
        
        switch (mission.missionType)
        {
            case MissionType.ServeCustomers:
                // For serve customers, add $10 per customer needed to main cash
                int currentServed = 0;
                
                if (mission.serviceLocation == ServiceLocation.Store && !string.IsNullOrEmpty(mission.specificStoreName))
                {
                    currentServed = GetLevelCustomersServedAtStore(mission.specificStoreName);
                }
                else
                {
                    currentServed = GetLevelCustomersServed();
                }
                
                int customersNeeded = mission.targetValue - currentServed;
                
                if (customersNeeded > 0)
                {
                    // Add $10 per customer to main cash
                    int moneyToAdd = customersNeeded * 10;
                    CurrencyManager.Instance.AddMoney(moneyToAdd);
                    
                    // Track earnings in DataManager
                    DataManager.Instance.AddMoney(moneyToAdd);
                    
                    // Add the customer count
                    DataManager.Instance.TotalCustomersServed += customersNeeded;
                    
                    // If specific store, add to that store's count
                    if (mission.serviceLocation == ServiceLocation.Store && !string.IsNullOrEmpty(mission.specificStoreName))
                    {
                        for (int i = 0; i < customersNeeded; i++)
                        {
                            DataManager.Instance.IncrementCustomersServedAtStore(mission.specificStoreName);
                        }
                    }
                    
                    Log($"Debug: Served {customersNeeded} customers, earned ${moneyToAdd}");
                }
                break;
                
            case MissionType.TotalEarnings:
                int currentEarnings = GetLevelEarnings();
                int earningsNeeded = mission.targetValue - currentEarnings;
                
                if (earningsNeeded > 0)
                {
                    // Add money to main cash
                    CurrencyManager.Instance.AddMoney(earningsNeeded);
                    
                    // Track in DataManager
                    DataManager.Instance.AddMoney(earningsNeeded);
                    
                    Log($"Debug: Earned ${earningsNeeded} to complete earnings mission");
                }
                break;
        }
    }
    
    #endregion
}
