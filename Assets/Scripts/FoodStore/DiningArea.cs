using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages all dining tables in a food store.
/// Helps find available seats for customers.
/// </summary>
public class DiningArea : MonoBehaviour
{
    [Header("Tables")]
    [SerializeField] private List<DiningTable> tables = new List<DiningTable>();
    
    public List<DiningTable> Tables => tables;
    
    /// <summary>
    /// Event fired when any garbage is cleared from any table
    /// </summary>
    public event Action OnSeatBecameAvailable;
    
    private void Awake()
    {
        // Auto-find tables if not assigned
        if (tables.Count == 0)
        {
            tables.AddRange(GetComponentsInChildren<DiningTable>());
        }
        
        // Subscribe to garbage cleared events from all tables
        foreach (var table in tables)
        {
            table.OnGarbageCleared += OnTableGarbageCleared;
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        foreach (var table in tables)
        {
            if (table != null)
            {
                table.OnGarbageCleared -= OnTableGarbageCleared;
            }
        }
    }
    
    private void OnTableGarbageCleared()
    {
        Debug.Log("[DiningArea] Garbage cleared from a table - seat now available");
        OnSeatBecameAvailable?.Invoke();
    }
    
    /// <summary>
    /// Find any available seat across all tables
    /// </summary>
    public DiningSeat FindAvailableSeat()
    {
        foreach (var table in tables)
        {
            DiningSeat seat = table.GetAvailableSeat();
            if (seat != null)
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
        foreach (var table in tables)
        {
            if (table.HasAvailableSeat())
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get count of all available seats
    /// </summary>
    public int GetAvailableSeatCount()
    {
        int count = 0;
        foreach (var table in tables)
        {
            count += table.GetAvailableSeatCount();
        }
        return count;
    }
    
    /// <summary>
    /// Get count of all seats (available or not)
    /// </summary>
    public int GetTotalSeatCount()
    {
        int count = 0;
        foreach (var table in tables)
        {
            count += table.Seats.Count;
        }
        return count;
    }
}
