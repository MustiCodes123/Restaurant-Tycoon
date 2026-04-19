using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages all table cleaners in the scene. Singleton pattern similar to JanitorManager.
/// Helps coordinate cleaners and find garbage/idle spots efficiently.
/// </summary>
public class TableCleanerManager : MonoBehaviour
{
    public static TableCleanerManager Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private List<TableCleanerIdleSpot> allIdleSpots = new List<TableCleanerIdleSpot>();
    [SerializeField] private List<DiningArea> allDiningAreas = new List<DiningArea>();
    [SerializeField] private List<GarbageBin> allGarbageBins = new List<GarbageBin>();
    
    private List<TableCleanerController> registeredCleaners = new List<TableCleanerController>();
    
    public List<TableCleanerController> RegisteredCleaners => registeredCleaners;
    public List<TableCleanerIdleSpot> AllIdleSpots => allIdleSpots;
    public List<DiningArea> AllDiningAreas => allDiningAreas;
    public List<GarbageBin> AllGarbageBins => allGarbageBins;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Find all idle spots if not assigned
        if (allIdleSpots.Count == 0)
        {
            allIdleSpots.AddRange(FindObjectsOfType<TableCleanerIdleSpot>());
        }
        
        // Find all dining areas if not assigned
        if (allDiningAreas.Count == 0)
        {
            allDiningAreas.AddRange(FindObjectsOfType<DiningArea>());
        }
        
        // Find all garbage bins if not assigned
        if (allGarbageBins.Count == 0)
        {
            allGarbageBins.AddRange(FindObjectsOfType<GarbageBin>());
        }
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    /// <summary>
    /// Register a table cleaner with the manager
    /// </summary>
    public void RegisterCleaner(TableCleanerController cleaner)
    {
        if (!registeredCleaners.Contains(cleaner))
        {
            registeredCleaners.Add(cleaner);
            Debug.Log($"[TableCleanerManager] Registered cleaner. Total: {registeredCleaners.Count}");
        }
    }
    
    /// <summary>
    /// Unregister a table cleaner from the manager
    /// </summary>
    public void UnregisterCleaner(TableCleanerController cleaner)
    {
        if (registeredCleaners.Contains(cleaner))
        {
            registeredCleaners.Remove(cleaner);
            Debug.Log($"[TableCleanerManager] Unregistered cleaner. Total: {registeredCleaners.Count}");
        }
    }
    
    /// <summary>
    /// Find the nearest available idle spot for a cleaner
    /// </summary>
    public TableCleanerIdleSpot GetNearestAvailableIdleSpot(Vector3 position, TableCleanerController requester)
    {
        TableCleanerIdleSpot nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var spot in allIdleSpots)
        {
            if (spot == null) continue;
            
            // Check if spot is not occupied, or if it's owned by the requester
            if (!spot.IsOccupied || spot.IsOwnedBy(requester))
            {
                float distance = Vector3.Distance(position, spot.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = spot;
                }
            }
        }
        
        return nearest;
    }
    
    /// <summary>
    /// Find the nearest garbage bin
    /// </summary>
    public GarbageBin GetNearestGarbageBin(Vector3 position)
    {
        GarbageBin nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var bin in allGarbageBins)
        {
            if (bin == null) continue;
            
            float distance = Vector3.Distance(position, bin.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = bin;
            }
        }
        
        return nearest;
    }
    
    /// <summary>
    /// Find the nearest garbage on any table
    /// </summary>
    public Garbage GetNearestGarbage(Vector3 position)
    {
        Garbage nearest = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var diningArea in allDiningAreas)
        {
            if (diningArea == null) continue;
            
            foreach (var table in diningArea.Tables)
            {
                if (table == null || !table.HasGarbage) continue;
                
                Transform garbageSpawnPoint = table.GarbageSpawnPoint;
                if (garbageSpawnPoint == null) continue;
                
                // Find garbage in children or nearby
                Garbage garbage = garbageSpawnPoint.GetComponentInChildren<Garbage>();
                if (garbage == null)
                {
                    Collider[] nearby = Physics.OverlapSphere(garbageSpawnPoint.position, 0.5f);
                    foreach (var col in nearby)
                    {
                        garbage = col.GetComponent<Garbage>();
                        if (garbage != null && !garbage.IsPickedUp) break;
                    }
                }
                
                if (garbage != null && !garbage.IsPickedUp && garbage.gameObject.activeInHierarchy)
                {
                    float distance = Vector3.Distance(position, garbage.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = garbage;
                    }
                }
            }
        }
        
        return nearest;
    }
    
    /// <summary>
    /// Add an idle spot to the manager
    /// </summary>
    public void AddIdleSpot(TableCleanerIdleSpot spot)
    {
        if (!allIdleSpots.Contains(spot))
        {
            allIdleSpots.Add(spot);
        }
    }
    
    /// <summary>
    /// Add a dining area to the manager
    /// </summary>
    public void AddDiningArea(DiningArea area)
    {
        if (!allDiningAreas.Contains(area))
        {
            allDiningAreas.Add(area);
        }
    }
    
    /// <summary>
    /// Add a garbage bin to the manager
    /// </summary>
    public void AddGarbageBin(GarbageBin bin)
    {
        if (!allGarbageBins.Contains(bin))
        {
            allGarbageBins.Add(bin);
        }
    }
}
