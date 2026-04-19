using UnityEngine;
using System.Collections.Generic;

public class Store : MonoBehaviour
{
    [Header("Store Info")]
    [SerializeField] private string storeName = "Store";
    [SerializeField] private int moneyPerCustomer = 10;
    
    [Header("Observatory Points")]
    [SerializeField] private List<Transform> observatoryPoints = new List<Transform>();
    
    [Header("Service Spots")]
    [SerializeField] private List<ServiceSpot> serviceSpots = new List<ServiceSpot>();
    
    [Header("Cleaning Spots")]
    [SerializeField] private List<CleaningSpot> cleaningSpots = new List<CleaningSpot>();
    
    [Header("Upgrade")]
    [SerializeField] private StoreUpgrade storeUpgrade;
    
    private bool needsCleaning = false;
    private int dirtySpotCount = 0;
    
    public string StoreName => storeName;
    public int MoneyPerCustomer => GetTotalMoneyPerCustomer();
    public int BaseMoneyPerCustomer => moneyPerCustomer;
    public bool NeedsCleaning => needsCleaning;
    public bool IsClean => !needsCleaning;
    public StoreUpgrade StoreUpgrade => storeUpgrade;
    public List<Transform> ObservatoryPoints => observatoryPoints;
    
    /// <summary>
    /// Gets total money per customer including upgrade bonuses
    /// </summary>
    private int GetTotalMoneyPerCustomer()
    {
        int bonus = storeUpgrade != null ? storeUpgrade.GetBonusMoneyPerCustomer() : 0;
        return moneyPerCustomer + bonus;
    }
    
    public ServiceSpot GetAvailableSpot()
    {
        foreach (var spot in serviceSpots)
        {
            if (!spot.IsOccupied)
            {
                return spot;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Gets a service spot that can accept more customers in its queue
    /// </summary>
    public ServiceSpot GetAvailableSpotForQueue()
    {
        foreach (var spot in serviceSpots)
        {
            if (spot.CanAcceptCustomer)
            {
                return spot;
            }
        }
        return null;
    }
    
    public bool HasAvailableSpot()
    {
        // Must be clean AND have a spot with room in queue
        if (needsCleaning) return false;
        return GetAvailableSpotForQueue() != null;
    }
    
    public int GetOccupiedSpotCount()
    {
        int count = 0;
        foreach (var spot in serviceSpots)
        {
            if (spot.IsOccupied) count++;
        }
        return count;
    }
    
    /// <summary>
    /// Called when a customer is served at this store. Makes the store dirty.
    /// </summary>
    public void OnCustomerServed()
    {
        if (cleaningSpots.Count == 0) return;
        
        needsCleaning = true;
        dirtySpotCount = cleaningSpots.Count;
        
        // Activate all cleaning spots
        foreach (var spot in cleaningSpots)
        {
            spot.MakeDirty();
        }
        
        Debug.Log($"[Store] {storeName} needs cleaning! {dirtySpotCount} spots to clean.");
    }
    
    /// <summary>
    /// Called by CleaningSpot when it's cleaned
    /// </summary>
    public void OnCleaningSpotCleaned(CleaningSpot spot)
    {
        dirtySpotCount--;
        
        Debug.Log($"[Store] {storeName} - Spot cleaned! {dirtySpotCount} spots remaining.");
        
        if (dirtySpotCount <= 0)
        {
            needsCleaning = false;
            dirtySpotCount = 0;
            Debug.Log($"[Store] {storeName} is now clean!");
        }
    }
    
    public List<CleaningSpot> GetDirtyCleaningSpots()
    {
        List<CleaningSpot> dirtySpots = new List<CleaningSpot>();
        foreach (var spot in cleaningSpots)
        {
            if (spot.IsDirty)
            {
                dirtySpots.Add(spot);
            }
        }
        return dirtySpots;
    }
}
