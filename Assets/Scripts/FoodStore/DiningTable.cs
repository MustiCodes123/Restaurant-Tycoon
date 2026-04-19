using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// A dining table with seats where customers eat.
/// Has a garbage spawn point for when customers finish eating.
/// </summary>
public class DiningTable : MonoBehaviour
{
    [Header("Seats")]
    [SerializeField] private List<DiningSeat> seats = new List<DiningSeat>();
    
    [Header("Tray Placement")]
    [SerializeField] private Transform trayPlacementPoint;
    
    [Header("Garbage")]
    [SerializeField] private Transform garbageSpawnPoint;
    [SerializeField] private GameObject garbagePrefab;
    
    [Header("Garbage UI")]
    [Tooltip("Canvas that shows when table has garbage on it")]
    [SerializeField] private Canvas garbageUICanvas;
    
    private Garbage currentGarbage;
    
    public List<DiningSeat> Seats => seats;
    public Transform TrayPlacementPoint => trayPlacementPoint;
    public Transform GarbageSpawnPoint => garbageSpawnPoint;
    public bool HasGarbage => currentGarbage != null;
    
    /// <summary>
    /// Event fired when garbage is picked up from this table
    /// </summary>
    public event Action OnGarbageCleared;
    
    private void Awake()
    {
        // Auto-find seats if not assigned
        if (seats.Count == 0)
        {
            seats.AddRange(GetComponentsInChildren<DiningSeat>());
        }
        
        // Initialize seats with table reference
        foreach (var seat in seats)
        {
            seat.Initialize(this);
        }
        
        // Hide garbage UI initially
        HideGarbageUI();
    }
    
    /// <summary>
    /// Get an available seat (not occupied and no garbage blocking)
    /// </summary>
    public DiningSeat GetAvailableSeat()
    {
        // If table has garbage, no seats are available
        if (HasGarbage) return null;
        
        foreach (var seat in seats)
        {
            if (seat.IsAvailable)
            {
                return seat;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Check if any seat is available
    /// </summary>
    public bool HasAvailableSeat()
    {
        if (HasGarbage) return false;
        
        foreach (var seat in seats)
        {
            if (seat.IsAvailable)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get count of available seats
    /// </summary>
    public int GetAvailableSeatCount()
    {
        if (HasGarbage) return 0;
        
        int count = 0;
        foreach (var seat in seats)
        {
            if (seat.IsAvailable) count++;
        }
        return count;
    }
    
    /// <summary>
    /// Spawn garbage on the table (called when customer finishes eating)
    /// </summary>
    public void SpawnGarbage()
    {
        if (garbagePrefab == null || garbageSpawnPoint == null)
        {
            Debug.LogWarning("[DiningTable] Cannot spawn garbage - missing prefab or spawn point");
            return;
        }
        
        if (currentGarbage != null)
        {
            Debug.LogWarning("[DiningTable] Garbage already exists on table");
            return;
        }
        
        GameObject garbageObj = Instantiate(garbagePrefab, garbageSpawnPoint.position, garbageSpawnPoint.rotation);
        currentGarbage = garbageObj.GetComponent<Garbage>();
        
        if (currentGarbage != null)
        {
            currentGarbage.Initialize(this);
        }
        
        // Show garbage UI
        ShowGarbageUI();
        
        Debug.Log("[DiningTable] Garbage spawned on table");
    }
    
    /// <summary>
    /// Called when garbage is picked up from this table
    /// </summary>
    public void OnGarbagePickedUp()
    {
        currentGarbage = null;
        
        // Hide garbage UI
        HideGarbageUI();
        
        Debug.Log("[DiningTable] Garbage picked up, table is now clean");
        
        // Notify listeners that seat is now available
        OnGarbageCleared?.Invoke();
    }
    
    private void ShowGarbageUI()
    {
        if (garbageUICanvas != null)
        {
            garbageUICanvas.enabled = true;
        }
    }
    
    private void HideGarbageUI()
    {
        if (garbageUICanvas != null)
        {
            garbageUICanvas.enabled = false;
        }
    }
    
    private void OnDrawGizmos()
    {
        // Draw table center
        Gizmos.color = HasGarbage ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 0.1f, 1f));
        
        // Draw tray placement
        if (trayPlacementPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(trayPlacementPoint.position, new Vector3(0.3f, 0.05f, 0.3f));
        }
        
        // Draw garbage spawn point
        if (garbageSpawnPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(garbageSpawnPoint.position, 0.15f);
        }
    }
}
