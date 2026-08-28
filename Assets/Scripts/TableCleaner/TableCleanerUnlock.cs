using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Handles the unlock logic for table cleaners. Similar to JanitorUnlock.
/// Attach to a GameObject in the scene and configure with TableCleanerUnlockData.
/// </summary>
public class TableCleanerUnlock : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private TableCleanerUnlockData unlockData;
    [SerializeField] private TableCleanerUnlockSpot unlockSpot;
    
    [Header("Spawn Settings")]
    [Tooltip("Where to spawn the table cleaner when unlocked")]
    [SerializeField] private Transform spawnPoint;
    
    [Header("Idle Spots")]
    [Tooltip("Available idle spots for the spawned table cleaner")]
    [SerializeField] private List<TableCleanerIdleSpot> idleSpots = new List<TableCleanerIdleSpot>();
    
    [Header("Dining Areas")]
    [Tooltip("Dining areas this cleaner will service")]
    [SerializeField] private List<DiningArea> diningAreas = new List<DiningArea>();
    
    [Header("Garbage Bins")]
    [Tooltip("Garbage bins where cleaner disposes trays")]
    [SerializeField] private List<GarbageBin> garbageBins = new List<GarbageBin>();
    
    [Header("Objects to Hide on Unlock")]
    [Tooltip("GameObjects to deactivate when unlocked (e.g., placeholder visuals)")]
    [SerializeField] private List<GameObject> objectsToHide = new List<GameObject>();
    
    [Header("Objects to Show on Unlock")]
    [Tooltip("Additional GameObjects to activate when unlocked")]
    [SerializeField] private List<GameObject> objectsToShow = new List<GameObject>();
    
    [Header("Save Key")]
    [SerializeField] private string saveKeyOverride;
    
    [Header("Animation Settings")]
    [SerializeField] private float popDuration = 0.5f;
    [SerializeField] private Ease popEase = Ease.OutBack;
    [SerializeField] private float popOvershoot = 1.5f;
    
    private bool isUnlocked = false;
    private TableCleanerController spawnedCleaner;
    
    private string SaveKey => string.IsNullOrEmpty(saveKeyOverride) 
        ? $"TableCleanerUnlock_{unlockData?.CleanerName ?? gameObject.name}" 
        : saveKeyOverride;
    
    public bool IsUnlocked => isUnlocked;
    public TableCleanerUnlockData UnlockData => unlockData;
    public int UnlockCost => unlockData != null ? unlockData.UnlockCost : 0;
    public TableCleanerController SpawnedCleaner => spawnedCleaner;
    public string PaymentProgressKey => $"{SaveKey}_PaymentProgress";
    
    public event Action OnTableCleanerUnlocked;
    public event Action OnUnlockAvailable;
    public event Action OnUnlockUnavailable;
    
    private void Start()
    {
        LoadUnlockState();
        ApplyUnlockState();
        
        // Subscribe to level up events
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelUp += OnPlayerLevelUp;
        }
        
        // Initial check for unlock availability
        CheckUnlockAvailability();
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
        CheckUnlockAvailability();
    }
    
    /// <summary>
    /// Checks if unlock is available and updates the unlock spot visibility
    /// </summary>
    public void CheckUnlockAvailability()
    {
        // Already unlocked - hide the spot
        if (isUnlocked)
        {
            HideUnlockSpot();
            return;
        }
        
        if (unlockData == null)
        {
            HideUnlockSpot();
            return;
        }
        
        int playerLevel = LevelManager.Instance != null 
            ? LevelManager.Instance.CurrentLevel 
            : 1;
        
        if (playerLevel >= unlockData.RequiredPlayerLevel)
        {
            ShowUnlockSpot();
            OnUnlockAvailable?.Invoke();
        }
        else
        {
            HideUnlockSpot();
            OnUnlockUnavailable?.Invoke();
        }
    }
    
    /// <summary>
    /// Called when unlock payment is completed at the unlock spot
    /// </summary>
    public void CompleteUnlock()
    {
        if (isUnlocked) return;
        
        isUnlocked = true;
        ClearPaymentProgress();
        SaveUnlockState();
        
        // Spawn the table cleaner
        SpawnTableCleaner();
        
        // Hide objects
        foreach (var obj in objectsToHide)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
        
        // Show additional objects with pop animation
        foreach (var obj in objectsToShow)
        {
            if (obj != null)
            {
                ActivateWithPopAnimation(obj);
            }
        }
        
        Debug.Log($"[TableCleanerUnlock] {unlockData?.CleanerName ?? gameObject.name} has been unlocked!");
        
        OnTableCleanerUnlocked?.Invoke();
        
        // Hide the unlock spot
        HideUnlockSpot();
    }
    
    private void SpawnTableCleaner()
    {
        if (unlockData == null || unlockData.CleanerPrefab == null)
        {
            Debug.LogWarning($"[TableCleanerUnlock] Cannot spawn table cleaner - missing prefab!");
            return;
        }
        
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        
        GameObject cleanerObj = Instantiate(unlockData.CleanerPrefab, spawnPosition, spawnRotation);
        spawnedCleaner = cleanerObj.GetComponent<TableCleanerController>();
        
        if (spawnedCleaner != null)
        {
            // Initialize with settings, idle spots, dining areas, and garbage bins
            spawnedCleaner.Initialize(unlockData, idleSpots, diningAreas, garbageBins);
            
            // Pop animation on spawn
            ActivateWithPopAnimation(cleanerObj);
        }
        
        Debug.Log($"[TableCleanerUnlock] Spawned table cleaner: {unlockData.CleanerName}");
    }
    
    private void ShowUnlockSpot()
    {
        if (unlockSpot != null)
        {
            unlockSpot.Show(this);
        }
    }
    
    private void HideUnlockSpot()
    {
        if (unlockSpot != null)
        {
            unlockSpot.Hide();
        }
    }
    
    private void ApplyUnlockState()
    {
        if (isUnlocked)
        {
            // Table cleaner is unlocked - spawn cleaner if not already present
            if (spawnedCleaner == null)
            {
                SpawnTableCleaner();
            }
            
            foreach (var obj in objectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
            
            foreach (var obj in objectsToShow)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }
        else
        {
            // Table cleaner is locked - hide objects to show
            foreach (var obj in objectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
            
            foreach (var obj in objectsToShow)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    }
    
    private void LoadUnlockState()
    {
        isUnlocked = PlayerPrefs.GetInt(SaveKey, 0) == 1;
    }
    
    private void SaveUnlockState()
    {
        PlayerPrefs.SetInt(SaveKey, isUnlocked ? 1 : 0);
        PlayerPrefs.Save();
    }

    public int LoadPaymentProgress()
    {
        return PaymentProgressStore.Load(PaymentProgressKey, UnlockCost);
    }

    public void SavePaymentProgress(int amount)
    {
        PaymentProgressStore.Save(PaymentProgressKey, amount, UnlockCost);
    }

    public void ClearPaymentProgress()
    {
        PaymentProgressStore.Clear(PaymentProgressKey);
    }
    
    /// <summary>
    /// Activates a GameObject with a pop/wobble animation
    /// </summary>
    private void ActivateWithPopAnimation(GameObject obj)
    {
        if (obj == null) return;
        
        // Store original scale
        Vector3 originalScale = obj.transform.localScale;
        
        // Start from zero scale
        obj.transform.localScale = Vector3.zero;
        obj.SetActive(true);
        
        // Animate to original scale with overshoot
        obj.transform.DOScale(originalScale, popDuration)
            .SetEase(popEase, popOvershoot)
            .OnComplete(() => 
            {
                // Optional: Add a small wobble at the end
                obj.transform.DOPunchRotation(new Vector3(0, 0, 5f), 0.3f, 10, 1f);
            });
    }
    
    /// <summary>
    /// Resets unlock state (for testing)
    /// </summary>
    [ContextMenu("Reset Unlock State")]
    public void ResetUnlockState()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        ClearPaymentProgress();
        isUnlocked = false;
        
        // Destroy spawned cleaner if exists
        if (spawnedCleaner != null)
        {
            Destroy(spawnedCleaner.gameObject);
            spawnedCleaner = null;
        }
        
        ApplyUnlockState();
        CheckUnlockAvailability();
    }
}
