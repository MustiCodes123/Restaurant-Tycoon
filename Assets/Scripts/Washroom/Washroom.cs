using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Main washroom controller that manages stalls and toilet paper racks.
/// Each stall now has its own queue system.
/// </summary>
public class Washroom : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private WashroomUnlockData washroomData;
    
    [Header("Stalls")]
    [SerializeField] private List<WashroomStall> stalls = new List<WashroomStall>();
    
    [Header("Toilet Paper")]
    [SerializeField] private List<ToiletPaperRack> toiletPaperRacks = new List<ToiletPaperRack>();
    
    public WashroomUnlockData WashroomData => washroomData;
    public List<WashroomStall> Stalls => stalls;
    public int StallCount => stalls.Count;
    
    public event Action<WashroomStall> OnStallNeedsToiletPaper;
    
    private void Start()
    {
        // Initialize all stalls
        int usesPerPaper = washroomData != null ? washroomData.UsesPerToiletPaper : 3;
        
        foreach (var stall in stalls)
        {
            if (stall != null)
            {
                stall.Initialize(this, usesPerPaper);
                stall.OnStallNeedsToiletPaper += HandleStallNeedsToiletPaper;
            }
        }
        
        Debug.Log($"[Washroom] Initialized with {stalls.Count} stalls");
    }
    
    private void OnDestroy()
    {
        foreach (var stall in stalls)
        {
            if (stall != null)
            {
                stall.OnStallNeedsToiletPaper -= HandleStallNeedsToiletPaper;
            }
        }
    }
    
    /// <summary>
    /// Get a stall that has queue space and is operational.
    /// Prefers stalls with shorter queues.
    /// </summary>
    public WashroomStall GetStallWithShortestQueue()
    {
        WashroomStall bestStall = null;
        int shortestQueue = int.MaxValue;
        
        foreach (var stall in stalls)
        {
            if (stall != null && stall.HasQueueSpace)
            {
                if (stall.QueueCount < shortestQueue)
                {
                    shortestQueue = stall.QueueCount;
                    bestStall = stall;
                }
            }
        }
        
        return bestStall;
    }
    
    /// <summary>
    /// Check if any stall can accept a new customer
    /// </summary>
    public bool CanAcceptCustomer()
    {
        foreach (var stall in stalls)
        {
            if (stall != null && stall.HasQueueSpace)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get total queue count across all stalls
    /// </summary>
    public int GetTotalQueueCount()
    {
        int total = 0;
        foreach (var stall in stalls)
        {
            if (stall != null)
            {
                total += stall.QueueCount;
            }
        }
        return total;
    }
    
    /// <summary>
    /// Get count of stalls needing toilet paper
    /// </summary>
    public int GetStallsNeedingToiletPaper()
    {
        int count = 0;
        foreach (var stall in stalls)
        {
            if (stall != null && stall.NeedsToiletPaper)
            {
                count++;
            }
        }
        return count;
    }
    
    private void HandleStallNeedsToiletPaper(WashroomStall stall)
    {
        OnStallNeedsToiletPaper?.Invoke(stall);
        Debug.Log($"[Washroom] Stall {stall.gameObject.name} needs toilet paper!");
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw connections to stalls
        Gizmos.color = Color.magenta;
        foreach (var stall in stalls)
        {
            if (stall != null)
            {
                Gizmos.DrawLine(transform.position, stall.transform.position);
            }
        }
    }
}
