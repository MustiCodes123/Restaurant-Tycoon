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
        RefreshTables();
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
        RefreshTables();

        foreach (var table in tables)
        {
            if (table == null || !table.gameObject.activeInHierarchy) continue;

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
        RefreshTables();

        foreach (var table in tables)
        {
            if (table != null && table.gameObject.activeInHierarchy && table.HasAvailableSeat())
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
        RefreshTables();

        int count = 0;
        foreach (var table in tables)
        {
            if (table != null && table.gameObject.activeInHierarchy)
            {
                count += table.GetAvailableSeatCount();
            }
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
            if (table != null)
            {
                count += table.Seats.Count;
            }
        }
        return count;
    }

    private void RefreshTables()
    {
        for (int i = tables.Count - 1; i >= 0; i--)
        {
            if (tables[i] == null)
            {
                tables.RemoveAt(i);
            }
        }

        DiningTable[] childTables = GetComponentsInChildren<DiningTable>(true);
        foreach (var table in childTables)
        {
            if (table == null || tables.Contains(table)) continue;

            tables.Add(table);
        }

        foreach (var table in tables)
        {
            table.OnGarbageCleared -= OnTableGarbageCleared;
            table.OnGarbageCleared += OnTableGarbageCleared;
        }
    }
}
