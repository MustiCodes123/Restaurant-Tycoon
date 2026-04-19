using UnityEngine;
using System;
using DG.Tweening;

/// <summary>
/// Handles the unlock logic for service guys. Linked to a specific store.
/// </summary>
public class ServiceGuyUnlock : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private ServiceGuyUnlockData unlockData;
    [SerializeField] private ServiceGuyUnlockSpot unlockSpot;
    
    [Header("Store Link")]
    [Tooltip("The store this service guy belongs to")]
    [SerializeField] private Store linkedStore;
    
    [Header("Spawn Settings")]
    [Tooltip("Where to spawn the service guy when unlocked")]
    [SerializeField] private Transform spawnPoint;
    
    [Header("Idle Spot")]
    [Tooltip("Where the service guy stands when idle")]
    [SerializeField] private ServiceGuyIdleSpot idleSpot;
    
    [Header("Objects to Hide on Unlock")]
    [SerializeField] private GameObject[] objectsToHide;
    
    [Header("Objects to Show on Unlock")]
    [SerializeField] private GameObject[] objectsToShow;
    
    [Header("Save Key")]
    [SerializeField] private string saveKeyOverride;
    
    [Header("Animation Settings")]
    [SerializeField] private float popDuration = 0.5f;
    [SerializeField] private Ease popEase = Ease.OutBack;
    [SerializeField] private float popOvershoot = 1.5f;
    
    private bool isUnlocked = false;
    private ServiceGuyController spawnedServiceGuy;
    
    private string SaveKey => string.IsNullOrEmpty(saveKeyOverride) 
        ? $"ServiceGuyUnlock_{linkedStore?.StoreName ?? gameObject.name}" 
        : saveKeyOverride;
    
    public bool IsUnlocked => isUnlocked;
    public ServiceGuyUnlockData UnlockData => unlockData;
    public int UnlockCost => unlockData != null ? unlockData.UnlockCost : 0;
    public Store LinkedStore => linkedStore;
    public ServiceGuyController SpawnedServiceGuy => spawnedServiceGuy;
    
    public event Action OnServiceGuyUnlocked;
    public event Action OnUnlockAvailable;
    public event Action OnUnlockUnavailable;
    
    private void Start()
    {
        LoadUnlockState();
        ApplyUnlockState();
        
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelUp += OnPlayerLevelUp;
        }
        
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
        
        // Check if linked store is unlocked/active
        if (linkedStore != null && !linkedStore.gameObject.activeInHierarchy)
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
    
    public void CompleteUnlock()
    {
        if (isUnlocked) return;
        
        isUnlocked = true;
        SaveUnlockState();
        
        // Spawn the service guy
        SpawnServiceGuy();
        
        // Hide objects
        if (objectsToHide != null)
        {
            foreach (var obj in objectsToHide)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
        
        // Show additional objects with pop animation
        if (objectsToShow != null)
        {
            foreach (var obj in objectsToShow)
            {
                if (obj != null)
                {
                    ActivateWithPopAnimation(obj);
                }
            }
        }
        
        Debug.Log($"[ServiceGuyUnlock] {unlockData?.ServiceGuyName ?? gameObject.name} has been unlocked for {linkedStore?.StoreName}!");
        
        OnServiceGuyUnlocked?.Invoke();
        
        HideUnlockSpot();
    }
    
    private void SpawnServiceGuy()
    {
        if (unlockData == null || unlockData.ServiceGuyPrefab == null)
        {
            Debug.LogWarning($"[ServiceGuyUnlock] Cannot spawn service guy - missing prefab!");
            return;
        }
        
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        
        GameObject serviceGuyObj = Instantiate(unlockData.ServiceGuyPrefab, spawnPosition, spawnRotation);
        spawnedServiceGuy = serviceGuyObj.GetComponent<ServiceGuyController>();
        
        if (spawnedServiceGuy != null)
        {
            spawnedServiceGuy.Initialize(unlockData, linkedStore, idleSpot);
            ActivateWithPopAnimation(serviceGuyObj);
        }
        
        Debug.Log($"[ServiceGuyUnlock] Spawned service guy: {unlockData.ServiceGuyName} for store: {linkedStore?.StoreName}");
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
            if (spawnedServiceGuy == null)
            {
                SpawnServiceGuy();
            }
            
            if (objectsToHide != null)
            {
                foreach (var obj in objectsToHide)
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                    }
                }
            }
            
            if (objectsToShow != null)
            {
                foreach (var obj in objectsToShow)
                {
                    if (obj != null)
                    {
                        obj.SetActive(true);
                    }
                }
            }
        }
        else
        {
            if (objectsToHide != null)
            {
                foreach (var obj in objectsToHide)
                {
                    if (obj != null)
                    {
                        obj.SetActive(true);
                    }
                }
            }
            
            if (objectsToShow != null)
            {
                foreach (var obj in objectsToShow)
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                    }
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
        isUnlocked = false;
        
        if (spawnedServiceGuy != null)
        {
            Destroy(spawnedServiceGuy.gameObject);
            spawnedServiceGuy = null;
        }
        
        ApplyUnlockState();
        CheckUnlockAvailability();
    }
}
