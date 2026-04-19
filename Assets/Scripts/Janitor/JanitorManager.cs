using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton manager for tracking all janitors and washrooms in the game.
/// Provides efficient access to stalls needing toilet paper for janitor AI.
/// </summary>
public class JanitorManager : MonoBehaviour
{
    public static JanitorManager Instance { get; private set; }
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private List<JanitorController> activeJanitors = new List<JanitorController>();
    private List<JanitorIdleSpot> registeredIdleSpots = new List<JanitorIdleSpot>();
    private List<Washroom> registeredWashrooms = new List<Washroom>();
    
    public IReadOnlyList<JanitorController> ActiveJanitors => activeJanitors;
    public IReadOnlyList<JanitorIdleSpot> IdleSpots => registeredIdleSpots;
    public IReadOnlyList<Washroom> Washrooms => registeredWashrooms;
    
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
    }
    
    private void Start()
    {
        // Find all existing washrooms and idle spots
        RefreshWashrooms();
        RefreshIdleSpots();
    }
    
    /// <summary>
    /// Register a janitor with the manager
    /// </summary>
    public void RegisterJanitor(JanitorController janitor)
    {
        if (janitor != null && !activeJanitors.Contains(janitor))
        {
            activeJanitors.Add(janitor);
            
            if (showDebugInfo)
            {
                Debug.Log($"[JanitorManager] Registered janitor: {janitor.name}");
            }
        }
    }
    
    /// <summary>
    /// Unregister a janitor from the manager
    /// </summary>
    public void UnregisterJanitor(JanitorController janitor)
    {
        if (activeJanitors.Remove(janitor))
        {
            if (showDebugInfo)
            {
                Debug.Log($"[JanitorManager] Unregistered janitor: {janitor.name}");
            }
        }
    }
    
    /// <summary>
    /// Register a washroom with the manager
    /// </summary>
    public void RegisterWashroom(Washroom washroom)
    {
        if (washroom != null && !registeredWashrooms.Contains(washroom))
        {
            registeredWashrooms.Add(washroom);
            
            if (showDebugInfo)
            {
                Debug.Log($"[JanitorManager] Registered washroom: {washroom.name}");
            }
        }
    }
    
    /// <summary>
    /// Unregister a washroom from the manager
    /// </summary>
    public void UnregisterWashroom(Washroom washroom)
    {
        registeredWashrooms.Remove(washroom);
    }
    
    /// <summary>
    /// Register an idle spot with the manager
    /// </summary>
    public void RegisterIdleSpot(JanitorIdleSpot spot)
    {
        if (spot != null && !registeredIdleSpots.Contains(spot))
        {
            registeredIdleSpots.Add(spot);
        }
    }
    
    /// <summary>
    /// Unregister an idle spot from the manager
    /// </summary>
    public void UnregisterIdleSpot(JanitorIdleSpot spot)
    {
        registeredIdleSpots.Remove(spot);
    }
    
    /// <summary>
    /// Get all stalls that need toilet paper
    /// </summary>
    public List<WashroomStall> GetStallsNeedingToiletPaper()
    {
        List<WashroomStall> needyStalls = new List<WashroomStall>();
        
        // Clean up null references
        registeredWashrooms.RemoveAll(w => w == null);
        
        foreach (var washroom in registeredWashrooms)
        {
            foreach (var stall in washroom.Stalls)
            {
                if (stall != null && stall.NeedsToiletPaper)
                {
                    needyStalls.Add(stall);
                }
            }
        }
        
        return needyStalls;
    }
    
    /// <summary>
    /// Get the nearest stall needing toilet paper to a position
    /// </summary>
    public WashroomStall GetNearestStallNeedingToiletPaper(Vector3 position)
    {
        WashroomStall nearest = null;
        float nearestDistance = float.MaxValue;
        
        var needyStalls = GetStallsNeedingToiletPaper();
        
        foreach (var stall in needyStalls)
        {
            float distance = Vector3.Distance(position, stall.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = stall;
            }
        }
        
        return nearest;
    }
    
    /// <summary>
    /// Get the nearest available idle spot to a position
    /// </summary>
    public JanitorIdleSpot GetNearestAvailableIdleSpot(Vector3 position, JanitorController forJanitor = null)
    {
        JanitorIdleSpot nearest = null;
        float nearestDistance = float.MaxValue;
        
        // Clean up null references
        registeredIdleSpots.RemoveAll(s => s == null);
        
        foreach (var spot in registeredIdleSpots)
        {
            if (!spot.IsOccupied || (forJanitor != null && spot.IsOwnedBy(forJanitor)))
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
    /// Refresh the list of washrooms by finding all in scene
    /// </summary>
    public void RefreshWashrooms()
    {
        registeredWashrooms.Clear();
        registeredWashrooms.AddRange(FindObjectsOfType<Washroom>(true));
        
        if (showDebugInfo)
        {
            Debug.Log($"[JanitorManager] Found {registeredWashrooms.Count} washrooms");
        }
    }
    
    /// <summary>
    /// Refresh the list of idle spots by finding all in scene
    /// </summary>
    public void RefreshIdleSpots()
    {
        registeredIdleSpots.Clear();
        registeredIdleSpots.AddRange(FindObjectsOfType<JanitorIdleSpot>());
        
        if (showDebugInfo)
        {
            Debug.Log($"[JanitorManager] Found {registeredIdleSpots.Count} idle spots");
        }
    }
    
    /// <summary>
    /// Get count of active janitors
    /// </summary>
    public int GetJanitorCount()
    {
        // Clean up destroyed janitors
        activeJanitors.RemoveAll(j => j == null);
        return activeJanitors.Count;
    }
    
    /// <summary>
    /// Get count of stalls needing toilet paper
    /// </summary>
    public int GetStallsNeedingToiletPaperCount()
    {
        int count = 0;
        foreach (var washroom in registeredWashrooms)
        {
            if (washroom != null)
            {
                count += washroom.GetStallsNeedingToiletPaper();
            }
        }
        return count;
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
