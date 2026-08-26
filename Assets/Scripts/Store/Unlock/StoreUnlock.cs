using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

public class StoreUnlock : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private StoreUnlockData unlockData;
    [SerializeField] private UnlockSpot unlockSpot;
    
    [Header("Store Reference")]
    [Tooltip("The store GameObject to activate when unlocked")]
    [SerializeField] private GameObject storeObject;
    
    [Header("Objects to Hide on Unlock")]
    [Tooltip("GameObjects to deactivate when unlocked (e.g., walls, barriers)")]
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
    private string SaveKey => string.IsNullOrEmpty(saveKeyOverride) 
        ? $"StoreUnlock_{unlockData?.StoreName ?? gameObject.name}" 
        : saveKeyOverride;
    
    public bool IsUnlocked => isUnlocked;
    public StoreUnlockData UnlockData => unlockData;
    public int UnlockCost => unlockData != null ? unlockData.UnlockCost : 0;
    public string PaymentProgressKey => $"{SaveKey}_PaymentProgress";
    
    public event Action OnStoreUnlocked;
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
        
        // Activate store with pop animation
        if (storeObject != null)
        {
            ActivateWithPopAnimation(storeObject);
        }
        
        // Hide objects (walls, barriers, etc.)
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
        
        Debug.Log($"[StoreUnlock] {unlockData?.StoreName ?? gameObject.name} has been unlocked!");
        
        OnStoreUnlocked?.Invoke();
        
        // Hide the unlock spot
        HideUnlockSpot();
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
            // Store is unlocked - show store, hide barriers
            if (storeObject != null)
            {
                storeObject.SetActive(true);
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
            // Store is locked - hide store, show barriers
            if (storeObject != null)
            {
                storeObject.SetActive(false);
            }
            
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
        ApplyUnlockState();
        CheckUnlockAvailability();
    }
}
