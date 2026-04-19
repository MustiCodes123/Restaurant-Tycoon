using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Handles the unlock logic for janitors. Similar to StoreUnlock.
/// Attach to a GameObject in the scene and configure with JanitorUnlockData.
/// </summary>
public class JanitorUnlock : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private JanitorUnlockData unlockData;
    [SerializeField] private JanitorUnlockSpot unlockSpot;
    
    [Header("Spawn Settings")]
    [Tooltip("Where to spawn the janitor when unlocked")]
    [SerializeField] private Transform spawnPoint;
    
    [Header("Idle Spots")]
    [Tooltip("Available idle spots for the spawned janitor")]
    [SerializeField] private List<JanitorIdleSpot> idleSpots = new List<JanitorIdleSpot>();
    
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
    private JanitorController spawnedJanitor;
    
    private string SaveKey => string.IsNullOrEmpty(saveKeyOverride) 
        ? $"JanitorUnlock_{unlockData?.JanitorName ?? gameObject.name}" 
        : saveKeyOverride;
    
    public bool IsUnlocked => isUnlocked;
    public JanitorUnlockData UnlockData => unlockData;
    public int UnlockCost => unlockData != null ? unlockData.UnlockCost : 0;
    public JanitorController SpawnedJanitor => spawnedJanitor;
    
    public event Action OnJanitorUnlocked;
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
        SaveUnlockState();
        
        // Spawn the janitor
        SpawnJanitor();
        
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
        
        Debug.Log($"[JanitorUnlock] {unlockData?.JanitorName ?? gameObject.name} has been unlocked!");
        
        OnJanitorUnlocked?.Invoke();
        
        // Hide the unlock spot
        HideUnlockSpot();
    }
    
    private void SpawnJanitor()
    {
        if (unlockData == null || unlockData.JanitorPrefab == null)
        {
            Debug.LogWarning($"[JanitorUnlock] Cannot spawn janitor - missing prefab!");
            return;
        }
        
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        
        GameObject janitorObj = Instantiate(unlockData.JanitorPrefab, spawnPosition, spawnRotation);
        spawnedJanitor = janitorObj.GetComponent<JanitorController>();
        
        if (spawnedJanitor != null)
        {
            // Initialize with settings and idle spots
            spawnedJanitor.Initialize(unlockData, idleSpots);
            
            // Pop animation on spawn
            ActivateWithPopAnimation(janitorObj);
        }
        
        Debug.Log($"[JanitorUnlock] Spawned janitor: {unlockData.JanitorName}");
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
            // Janitor is unlocked - spawn janitor if not already present
            if (spawnedJanitor == null)
            {
                SpawnJanitor();
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
            // Janitor is locked - hide objects to show
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
        isUnlocked = false;
        
        // Destroy spawned janitor if exists
        if (spawnedJanitor != null)
        {
            Destroy(spawnedJanitor.gameObject);
            spawnedJanitor = null;
        }
        
        ApplyUnlockState();
        CheckUnlockAvailability();
    }
}
