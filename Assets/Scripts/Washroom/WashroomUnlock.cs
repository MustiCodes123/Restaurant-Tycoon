using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Handles the unlock logic for washrooms. Level-based unlocking similar to StoreUnlock.
/// </summary>
public class WashroomUnlock : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private WashroomUnlockData unlockData;
    [SerializeField] private WashroomUnlockSpot unlockSpot;
    
    [Header("Washroom Reference")]
    [Tooltip("The washroom GameObject to activate when unlocked")]
    [SerializeField] private GameObject washroomObject;
    
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
        ? $"WashroomUnlock_{unlockData?.WashroomName ?? gameObject.name}" 
        : saveKeyOverride;
    
    public bool IsUnlocked => isUnlocked;
    public WashroomUnlockData UnlockData => unlockData;
    public int UnlockCost => unlockData != null ? unlockData.UnlockCost : 0;
    public string PaymentProgressKey => $"{SaveKey}_PaymentProgress";
    
    public event Action OnWashroomUnlocked;
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
        
        // Activate washroom with pop animation
        if (washroomObject != null)
        {
            ActivateWithPopAnimation(washroomObject);
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
        
        Debug.Log($"[WashroomUnlock] {unlockData?.WashroomName ?? gameObject.name} has been unlocked!");
        
        OnWashroomUnlocked?.Invoke();
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
            if (washroomObject != null)
            {
                washroomObject.SetActive(true);
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
            if (washroomObject != null)
            {
                washroomObject.SetActive(false);
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
    
    private void ActivateWithPopAnimation(GameObject obj)
    {
        if (obj == null) return;
        
        Vector3 originalScale = obj.transform.localScale;
        obj.transform.localScale = Vector3.zero;
        obj.SetActive(true);
        
        obj.transform.DOScale(originalScale, popDuration)
            .SetEase(popEase, popOvershoot)
            .OnComplete(() => 
            {
                obj.transform.DOPunchRotation(new Vector3(0, 0, 5f), 0.3f, 10, 1f);
            });
    }
    
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
