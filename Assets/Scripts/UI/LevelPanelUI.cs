using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public class LevelPanelUI : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private RectTransform panelTransform;
    [SerializeField] private TextMeshProUGUI levelTitleText;
    [SerializeField] private Transform missionContainer;
    [SerializeField] private GameObject missionUIPrefab;
    
    [Header("Dynamic Mission Settings")]
    [Tooltip("Prefab for dynamic missions (unlock/upgrade). If not set, will use missionUIPrefab.")]
    [SerializeField] private GameObject dynamicMissionUIPrefab;
    
    [Header("Animation Settings")]
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private Ease slideInEase = Ease.OutBack;
    [SerializeField] private Ease slideOutEase = Ease.InBack;
    [SerializeField] private float hidePositionY = 300f;
    
    private Vector2 showPosition;
    private Vector2 hidePosition;
    private List<MissionUI> activeMissionUIs = new List<MissionUI>();
    private Dictionary<string, DynamicMissionUI> activeDynamicMissionUIs = new Dictionary<string, DynamicMissionUI>();
    private Tween currentTween;
    private bool isPanelVisible = false;
    private LevelData currentLevelData;
    private bool isInitialized = false;
    
    /// <summary>
    /// Call this after the GameObject is enabled to setup initial state
    /// </summary>
    public void Initialize()
    {
        if (isInitialized) return;
        isInitialized = true;
        
        showPosition = panelTransform.anchoredPosition;
        hidePosition = new Vector2(showPosition.x, showPosition.y + hidePositionY);
        
        // Start hidden
        panelTransform.anchoredPosition = hidePosition;
        isPanelVisible = false;
        
        // Subscribe to dynamic mission events
        if (DynamicMissionManager.Instance != null)
        {
            DynamicMissionManager.Instance.OnMissionAdded += OnDynamicMissionAdded;
            DynamicMissionManager.Instance.OnMissionCompleted += OnDynamicMissionCompleted;
            DynamicMissionManager.Instance.OnMissionRemoved += OnDynamicMissionRemoved;
        }
    }
    
    private void OnDestroy()
    {
        currentTween?.Kill();
        
        // Unsubscribe from dynamic mission events
        if (DynamicMissionManager.Instance != null)
        {
            DynamicMissionManager.Instance.OnMissionAdded -= OnDynamicMissionAdded;
            DynamicMissionManager.Instance.OnMissionCompleted -= OnDynamicMissionCompleted;
            DynamicMissionManager.Instance.OnMissionRemoved -= OnDynamicMissionRemoved;
        }
    }
    
    private int displayedLevelNumber = 1;
    
    /// <summary>
    /// Sets up and shows the level missions with a slide-down animation
    /// </summary>
    public void ShowLevel(LevelData levelData, int levelNumber = -1)
    {
        // Use passed level number, or fall back to ScriptableObject field
        displayedLevelNumber = levelNumber > 0 ? levelNumber : levelData.levelNumber;
        
        Debug.Log($"[LevelPanelUI] ShowLevel called for Level {displayedLevelNumber} with {levelData.missions?.Count ?? 0} missions");
        
        currentLevelData = levelData;
        
        // Clear old missions - destroy all children in container
        ClearMissions();
        
        // Set title
        if (levelTitleText != null)
            levelTitleText.text = $"Level {displayedLevelNumber}";
        
        // Spawn mission UIs
        if (levelData.missions != null)
        {
            foreach (var mission in levelData.missions)
            {
                if (mission == null) continue;
                
                GameObject missionObj = Instantiate(missionUIPrefab, missionContainer);
                missionObj.SetActive(true); // Ensure it's active
                MissionUI missionUI = missionObj.GetComponent<MissionUI>();
                if (missionUI != null)
                {
                    missionUI.Setup(mission);
                    missionUI.OnRemoved += OnMissionUIRemoved;
                    activeMissionUIs.Add(missionUI);
                    Debug.Log($"[LevelPanelUI] Created mission UI for: {mission.missionType} - Target: {mission.targetValue}");
                }
            }
        }
        
        Debug.Log($"[LevelPanelUI] Setup complete. Active mission UIs: {activeMissionUIs.Count}");
        
        // Update progress immediately for the new missions
        UpdateAllMissions();
        
        // Load any existing dynamic missions
        LoadExistingDynamicMissions();
        
        // Auto slide in when level starts
        SlideIn();
    }
    
    /// <summary>
    /// Loads any existing dynamic missions from the manager
    /// </summary>
    private void LoadExistingDynamicMissions()
    {
        if (DynamicMissionManager.Instance == null) return;
        
        var existingMissions = DynamicMissionManager.Instance.GetActiveMissions();
        foreach (var mission in existingMissions)
        {
            AddDynamicMissionUI(mission);
        }
        
        Debug.Log($"[LevelPanelUI] Loaded {existingMissions.Count} existing dynamic missions");
    }
    
    /// <summary>
    /// Hides the panel with a slide-up animation (called when level completes)
    /// </summary>
    public void Hide(System.Action onComplete = null)
    {
        if (!isPanelVisible)
        {
            onComplete?.Invoke();
            return;
        }
        
        SlideOut(onComplete);
    }
    
    public void UpdateAllMissions()
    {
        foreach (var missionUI in activeMissionUIs)
        {
            int currentValue = GetValueForMission(missionUI.MissionData);
            missionUI.UpdateProgress(currentValue);
        }
    }
    
    public bool AreAllMissionsCompleted()
    {
        // Check regular missions
        foreach (var missionUI in activeMissionUIs)
        {
            if (!missionUI.IsCompleted)
                return false;
        }
        
        // Check dynamic missions
        foreach (var dynamicMissionUI in activeDynamicMissionUIs.Values)
        {
            if (!dynamicMissionUI.IsCompleted)
                return false;
        }
        
        // Must have at least one mission (regular or dynamic)
        return activeMissionUIs.Count > 0 || activeDynamicMissionUIs.Count > 0;
    }
    
    private int GetValueForMission(MissionData data)
    {
        if (data == null || LevelManager.Instance == null) return 0;
        
        switch (data.missionType)
        {
            case MissionType.ServeCustomers:
                return GetLevelCustomersServedForMission(data);
            
            case MissionType.TotalEarnings:
                // Now uses level-based earnings (earned since level started)
                return LevelManager.Instance.GetLevelEarnings();
            
            default:
                return 0;
        }
    }
    
    private int GetLevelCustomersServedForMission(MissionData data)
    {
        if (LevelManager.Instance == null) return 0;
        
        switch (data.serviceLocation)
        {
            case ServiceLocation.Store:
                if (!string.IsNullOrEmpty(data.specificStoreName))
                    return LevelManager.Instance.GetLevelCustomersServedAtStore(data.specificStoreName);
                // For "any store", just use total customers served
                return LevelManager.Instance.GetLevelCustomersServed();
            
            case ServiceLocation.Any:
            default:
                return LevelManager.Instance.GetLevelCustomersServed();
        }
    }
    
    private void OnMissionUIRemoved(MissionUI missionUI)
    {
        if (missionUI != null)
        {
            missionUI.OnRemoved -= OnMissionUIRemoved;
            activeMissionUIs.Remove(missionUI);
            Debug.Log($"[LevelPanelUI] Mission UI removed. Remaining: {activeMissionUIs.Count}");
        }
    }
    
    private void ClearMissions()
    {
        // Unsubscribe from events before clearing
        foreach (var missionUI in activeMissionUIs)
        {
            if (missionUI != null)
            {
                missionUI.OnRemoved -= OnMissionUIRemoved;
            }
        }
        
        // Clear regular missions list
        activeMissionUIs.Clear();
        
        // Clear dynamic missions list
        activeDynamicMissionUIs.Clear();
        
        // Destroy all children in the container
        // Set inactive immediately so they're hidden, then destroy
        for (int i = missionContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child = missionContainer.GetChild(i).gameObject;
            child.SetActive(false); // Hide immediately
            Destroy(child);
        }
        
        Debug.Log($"[LevelPanelUI] Cleared missions. Container now has {missionContainer.childCount} children (pending destroy)");
    }
    
    #region Dynamic Mission Handling
    
    private void OnDynamicMissionAdded(DynamicMission mission)
    {
        AddDynamicMissionUI(mission);
    }
    
    private void OnDynamicMissionCompleted(DynamicMission mission)
    {
        if (activeDynamicMissionUIs.TryGetValue(mission.missionId, out var missionUI))
        {
            missionUI.MarkCompleted();
            
            // Check if all missions are now complete
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.CheckMissionProgress();
            }
        }
    }
    
    private void OnDynamicMissionRemoved(string missionId)
    {
        RemoveDynamicMissionUI(missionId);
    }
    
    private void AddDynamicMissionUI(DynamicMission mission)
    {
        // Don't add if already exists
        if (activeDynamicMissionUIs.ContainsKey(mission.missionId))
        {
            Debug.Log($"[LevelPanelUI] Dynamic mission UI already exists: {mission.missionId}");
            return;
        }
        
        // Use dynamic prefab if set, otherwise use regular mission prefab
        GameObject prefab = dynamicMissionUIPrefab != null ? dynamicMissionUIPrefab : missionUIPrefab;
        
        if (prefab == null)
        {
            Debug.LogError("[LevelPanelUI] No mission UI prefab available!");
            return;
        }
        
        GameObject missionObj = Instantiate(prefab, missionContainer);
        missionObj.SetActive(true);
        
        // Try to get DynamicMissionUI component first
        DynamicMissionUI dynamicUI = missionObj.GetComponent<DynamicMissionUI>();
        if (dynamicUI != null)
        {
            dynamicUI.Setup(mission);
            activeDynamicMissionUIs[mission.missionId] = dynamicUI;
            Debug.Log($"[LevelPanelUI] Added dynamic mission UI: {mission.displayText}");
        }
        else
        {
            // Fallback: If no DynamicMissionUI, add one
            dynamicUI = missionObj.AddComponent<DynamicMissionUI>();
            dynamicUI.Setup(mission);
            activeDynamicMissionUIs[mission.missionId] = dynamicUI;
            Debug.Log($"[LevelPanelUI] Added dynamic mission UI (with added component): {mission.displayText}");
        }
    }
    
    private void RemoveDynamicMissionUI(string missionId)
    {
        if (activeDynamicMissionUIs.TryGetValue(missionId, out var missionUI))
        {
            if (missionUI != null && missionUI.gameObject != null)
            {
                missionUI.gameObject.SetActive(false);
                Destroy(missionUI.gameObject);
            }
            activeDynamicMissionUIs.Remove(missionId);
            Debug.Log($"[LevelPanelUI] Removed dynamic mission UI: {missionId}");
        }
    }
    
    #endregion
    
    private void SlideIn()
    {
        currentTween?.Kill();
        isPanelVisible = true;
        currentTween = panelTransform
            .DOAnchorPos(showPosition, slideDuration)
            .SetEase(slideInEase);
    }
    
    private void SlideOut(System.Action onComplete = null)
    {
        currentTween?.Kill();
        isPanelVisible = false;
        currentTween = panelTransform
            .DOAnchorPos(hidePosition, slideDuration)
            .SetEase(slideOutEase)
            .OnComplete(() => onComplete?.Invoke());
    }
}
