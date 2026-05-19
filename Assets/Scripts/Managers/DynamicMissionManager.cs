using UnityEngine;
using System;
using System.Collections.Generic;

public enum DynamicMissionType
{
    UnlockStore,
    UpgradeStore,
    UnlockJanitor,
    UnlockServiceGuy,
    UnlockTableCleaner,
    UnlockWashroom,
    UnlockCook,
    UnlockSceneObject
}

[Serializable]
public class DynamicMission
{
    public string missionId;
    public DynamicMissionType missionType;
    public string displayText;
    public bool isCompleted;
    
    public DynamicMission(string id, DynamicMissionType type, string text)
    {
        missionId = id;
        missionType = type;
        displayText = text;
        isCompleted = false;
    }
}

[Serializable]
public class DynamicMissionSaveData
{
    public List<DynamicMission> missions = new List<DynamicMission>();
}

/// <summary>
/// Manages dynamic missions that are created at runtime when unlock/upgrade spots become active.
/// These missions are added to the level panel UI and persisted via PlayerPrefs.
/// </summary>
public class DynamicMissionManager : MonoBehaviour
{
    public static DynamicMissionManager Instance { get; private set; }
    
    private const string SAVE_KEY = "DynamicMissions";
    
    private Dictionary<string, DynamicMission> activeMissions = new Dictionary<string, DynamicMission>();
    
    public event Action<DynamicMission> OnMissionAdded;
    public event Action<DynamicMission> OnMissionCompleted;
    public event Action<string> OnMissionRemoved;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        LoadMissions();
    }
    
    /// <summary>
    /// Registers a new dynamic mission for store unlock
    /// </summary>
    public void RegisterStoreUnlockMission(string storeId, string storeName)
    {
        string missionId = $"UnlockStore_{storeId}";
        
        // Check if mission already exists
        if (activeMissions.ContainsKey(missionId))
        {
            Log($"Mission already exists: {missionId}");
            return;
        }
        
        string displayText = $"Unlock {storeName}";
        var mission = new DynamicMission(missionId, DynamicMissionType.UnlockStore, displayText);
        
        activeMissions[missionId] = mission;
        SaveMissions();
        
        Log($"Registered store unlock mission: {displayText}");
        OnMissionAdded?.Invoke(mission);
    }
    
    /// <summary>
    /// Registers a new dynamic mission for store upgrade
    /// </summary>
    public void RegisterStoreUpgradeMission(string storeId, string storeName, int targetLevel)
    {
        string missionId = $"UpgradeStore_{storeId}_Lvl{targetLevel}";
        
        // Check if mission already exists
        if (activeMissions.ContainsKey(missionId))
        {
            Log($"Mission already exists: {missionId}");
            return;
        }
        
        string displayText = $"Upgrade {storeName} to Level {targetLevel}";
        var mission = new DynamicMission(missionId, DynamicMissionType.UpgradeStore, displayText);
        
        activeMissions[missionId] = mission;
        SaveMissions();
        
        Log($"Registered store upgrade mission: {displayText}");
        OnMissionAdded?.Invoke(mission);
    }
    
    /// <summary>
    /// Registers a new dynamic mission for janitor unlock
    /// </summary>
    public void RegisterJanitorUnlockMission(string janitorId, string janitorName)
    {
        string missionId = $"UnlockJanitor_{janitorId}";
        
        // Check if mission already exists
        if (activeMissions.ContainsKey(missionId))
        {
            Log($"Mission already exists: {missionId}");
            return;
        }
        
        string displayText = $"Unlock {janitorName}";
        var mission = new DynamicMission(missionId, DynamicMissionType.UnlockJanitor, displayText);
        
        activeMissions[missionId] = mission;
        SaveMissions();
        
        Log($"Registered janitor unlock mission: {displayText}");
        OnMissionAdded?.Invoke(mission);
    }
    
    /// <summary>
    /// Registers a new dynamic mission for service guy unlock
    /// </summary>
    public void RegisterServiceGuyUnlockMission(string serviceGuyId, string serviceGuyName, string storeName)
    {
        string missionId = $"UnlockServiceGuy_{serviceGuyId}";
        
        // Check if mission already exists
        if (activeMissions.ContainsKey(missionId))
        {
            Log($"Mission already exists: {missionId}");
            return;
        }
        
        string displayText = $"Unlock {serviceGuyName} for {storeName}";
        var mission = new DynamicMission(missionId, DynamicMissionType.UnlockServiceGuy, displayText);
        
        activeMissions[missionId] = mission;
        SaveMissions();
        
        Log($"Registered service guy unlock mission: {displayText}");
        OnMissionAdded?.Invoke(mission);
    }
    
    /// <summary>
    /// Registers a new dynamic mission for table cleaner unlock
    /// </summary>
    public void RegisterTableCleanerUnlockMission(string cleanerId, string cleanerName)
    {
        string missionId = $"UnlockTableCleaner_{cleanerId}";
        
        // Check if mission already exists
        if (activeMissions.ContainsKey(missionId))
        {
            Log($"Mission already exists: {missionId}");
            return;
        }
        
        string displayText = $"Unlock {cleanerName}";
        var mission = new DynamicMission(missionId, DynamicMissionType.UnlockTableCleaner, displayText);
        
        activeMissions[missionId] = mission;
        SaveMissions();
        
        Log($"Registered table cleaner unlock mission: {displayText}");
        OnMissionAdded?.Invoke(mission);
    }
    
    /// <summary>
    /// Marks a store unlock mission as completed
    /// </summary>
    public void CompleteStoreUnlockMission(string storeId)
    {
        string missionId = $"UnlockStore_{storeId}";
        CompleteMission(missionId);
    }
    
    /// <summary>
    /// Marks a store upgrade mission as completed
    /// </summary>
    public void CompleteStoreUpgradeMission(string storeId, int completedLevel)
    {
        string missionId = $"UpgradeStore_{storeId}_Lvl{completedLevel}";
        CompleteMission(missionId);
    }
    
    /// <summary>
    /// Marks a janitor unlock mission as completed
    /// </summary>
    public void CompleteJanitorUnlockMission(string janitorId)
    {
        string missionId = $"UnlockJanitor_{janitorId}";
        CompleteMission(missionId);
    }
    
    /// <summary>
    /// Marks a service guy unlock mission as completed
    /// </summary>
    public void CompleteServiceGuyUnlockMission(string serviceGuyId)
    {
        string missionId = $"UnlockServiceGuy_{serviceGuyId}";
        CompleteMission(missionId);
    }
    
    /// <summary>
    /// Marks a table cleaner unlock mission as completed
    /// </summary>
    public void CompleteTableCleanerUnlockMission(string cleanerId)
    {
        string missionId = $"UnlockTableCleaner_{cleanerId}";
        CompleteMission(missionId);
    }
    
    private void CompleteMission(string missionId)
    {
        if (!activeMissions.TryGetValue(missionId, out var mission))
        {
            Log($"Mission not found to complete: {missionId}");
            return;
        }
        
        if (mission.isCompleted)
        {
            Log($"Mission already completed: {missionId}");
            return;
        }
        
        mission.isCompleted = true;
        SaveMissions();
        
        Log($"Completed mission: {mission.displayText}");
        OnMissionCompleted?.Invoke(mission);
        
        // Remove completed mission from active list after a short delay
        // to allow UI to show completion state
        StartCoroutine(RemoveMissionAfterDelay(missionId, 0.5f));
    }
    
    /// <summary>
    /// Directly completes a mission by ID (used for debug purposes)
    /// </summary>
    public void CompleteMissionDirect(string missionId)
    {
        CompleteMission(missionId);
    }
    
    private System.Collections.IEnumerator RemoveMissionAfterDelay(string missionId, float delay)
    {
        yield return new WaitForSeconds(delay);
        RemoveMission(missionId);
    }
    
    /// <summary>
    /// Removes a mission (called when unlock/upgrade spot is hidden or completed)
    /// </summary>
    public void RemoveMission(string missionId)
    {
        if (activeMissions.Remove(missionId))
        {
            SaveMissions();
            Log($"Removed mission: {missionId}");
            OnMissionRemoved?.Invoke(missionId);
        }
    }
    
    /// <summary>
    /// Removes a store unlock mission
    /// </summary>
    public void RemoveStoreUnlockMission(string storeId)
    {
        string missionId = $"UnlockStore_{storeId}";
        RemoveMission(missionId);
    }
    
    /// <summary>
    /// Removes a store upgrade mission
    /// </summary>
    public void RemoveStoreUpgradeMission(string storeId, int targetLevel)
    {
        string missionId = $"UpgradeStore_{storeId}_Lvl{targetLevel}";
        RemoveMission(missionId);
    }
    
    /// <summary>
    /// Removes a janitor unlock mission
    /// </summary>
    public void RemoveJanitorUnlockMission(string janitorId)
    {
        string missionId = $"UnlockJanitor_{janitorId}";
        RemoveMission(missionId);
    }
    
    /// <summary>
    /// Removes a service guy unlock mission
    /// </summary>
    public void RemoveServiceGuyUnlockMission(string serviceGuyId)
    {
        string missionId = $"UnlockServiceGuy_{serviceGuyId}";
        RemoveMission(missionId);
    }
    
    /// <summary>
    /// Removes a table cleaner unlock mission
    /// </summary>
    public void RemoveTableCleanerUnlockMission(string cleanerId)
    {
        string missionId = $"UnlockTableCleaner_{cleanerId}";
        RemoveMission(missionId);
    }
    
    /// <summary>
    /// Registers a new dynamic mission for washroom unlock
    /// </summary>
    public void RegisterWashroomUnlockMission(string washroomId, string washroomName)
    {
        string missionId = $"UnlockWashroom_{washroomId}";
        
        if (activeMissions.ContainsKey(missionId))
        {
            Log($"Mission already exists: {missionId}");
            return;
        }
        
        string displayText = $"Unlock {washroomName}";
        var mission = new DynamicMission(missionId, DynamicMissionType.UnlockWashroom, displayText);
        
        activeMissions[missionId] = mission;
        SaveMissions();
        
        Log($"Registered washroom unlock mission: {displayText}");
        OnMissionAdded?.Invoke(mission);
    }
    
    /// <summary>
    /// Marks a washroom unlock mission as completed
    /// </summary>
    public void CompleteWashroomUnlockMission(string washroomId)
    {
        string missionId = $"UnlockWashroom_{washroomId}";
        CompleteMission(missionId);
    }
    
    /// <summary>
    /// Removes a washroom unlock mission
    /// </summary>
    public void RemoveWashroomUnlockMission(string washroomId)
    {
        string missionId = $"UnlockWashroom_{washroomId}";
        RemoveMission(missionId);
    }

    // ── Cook ─────────────────────────────────────────────────────────────────

    public void RegisterCookUnlockMission(string cookId, string cookName)
    {
        string missionId = $"UnlockCook_{cookId}";
        if (activeMissions.ContainsKey(missionId)) return;

        var mission = new DynamicMission(missionId, DynamicMissionType.UnlockCook, $"Hire {cookName}");
        activeMissions[missionId] = mission;
        SaveMissions();
        Log($"Registered cook unlock mission: {mission.displayText}");
        OnMissionAdded?.Invoke(mission);
    }

    public void CompleteCookUnlockMission(string cookId)
    {
        CompleteMission($"UnlockCook_{cookId}");
    }

    public void RemoveCookUnlockMission(string cookId)
    {
        RemoveMission($"UnlockCook_{cookId}");
    }

    // ── Scene Object ─────────────────────────────────────────────────────────

    public void RegisterSceneObjectUnlockMission(string objectId, string objectName)
    {
        string missionId = $"UnlockSceneObject_{objectId}";
        if (activeMissions.ContainsKey(missionId)) return;

        var mission = new DynamicMission(missionId, DynamicMissionType.UnlockSceneObject, $"Unlock {objectName}");
        activeMissions[missionId] = mission;
        SaveMissions();
        Log($"Registered scene object unlock mission: {mission.displayText}");
        OnMissionAdded?.Invoke(mission);
    }

    public void CompleteSceneObjectUnlockMission(string objectId)
    {
        CompleteMission($"UnlockSceneObject_{objectId}");
    }

    public void RemoveSceneObjectUnlockMission(string objectId)
    {
        RemoveMission($"UnlockSceneObject_{objectId}");
    }
    
    /// <summary>
    /// Gets all active (non-completed) missions
    /// </summary>
    public List<DynamicMission> GetActiveMissions()
    {
        var result = new List<DynamicMission>();
        foreach (var mission in activeMissions.Values)
        {
            if (!mission.isCompleted)
            {
                result.Add(mission);
            }
        }
        return result;
    }
    
    /// <summary>
    /// Gets a specific mission by ID
    /// </summary>
    public DynamicMission GetMission(string missionId)
    {
        activeMissions.TryGetValue(missionId, out var mission);
        return mission;
    }
    
    /// <summary>
    /// Checks if a mission exists and is not completed
    /// </summary>
    public bool HasActiveMission(string missionId)
    {
        return activeMissions.TryGetValue(missionId, out var mission) && !mission.isCompleted;
    }
    
    private void SaveMissions()
    {
        var saveData = new DynamicMissionSaveData();
        saveData.missions = new List<DynamicMission>(activeMissions.Values);
        
        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
        
        Log($"Saved {saveData.missions.Count} dynamic missions");
    }
    
    private void LoadMissions()
    {
        activeMissions.Clear();
        
        if (!PlayerPrefs.HasKey(SAVE_KEY))
        {
            Log("No saved dynamic missions found");
            return;
        }
        
        string json = PlayerPrefs.GetString(SAVE_KEY);
        var saveData = JsonUtility.FromJson<DynamicMissionSaveData>(json);
        
        if (saveData != null && saveData.missions != null)
        {
            foreach (var mission in saveData.missions)
            {
                // Only load non-completed missions
                if (!mission.isCompleted)
                {
                    activeMissions[mission.missionId] = mission;
                }
            }
            Log($"Loaded {activeMissions.Count} dynamic missions");
        }
    }
    
    /// <summary>
    /// Clears all dynamic missions (for testing/reset)
    /// </summary>
    public void ClearAllMissions()
    {
        activeMissions.Clear();
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
        Log("Cleared all dynamic missions");
    }
    
    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[DynamicMissionManager] {message}");
    }
}
