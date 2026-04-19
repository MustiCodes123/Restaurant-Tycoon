using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tech store controller that manages item container service flow.
/// Attach this to the root of a tech store alongside the base Store component.
/// </summary>
public class TechStore : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Store baseStore;
    
    [Header("Item Containers")]
    [SerializeField] private List<TechItemContainer> itemContainers = new List<TechItemContainer>();
    
    [Header("Observatory Phase")]
    [Tooltip("Enable or disable the observatory phase where customers observe before going to item containers")]
    [SerializeField] private bool enableObservatoryPhase = true;
    
    public Store BaseStore => baseStore;
    public List<TechItemContainer> ItemContainers => itemContainers;
    public int ItemContainerCount => itemContainers.Count;
    public bool EnableObservatoryPhase => enableObservatoryPhase;
    
    private void Awake()
    {
        if (baseStore == null)
        {
            baseStore = GetComponent<Store>();
        }
    }
    
    private void Start()
    {
        Debug.Log($"[TechStore] {gameObject.name} Start() - ItemContainers count: {itemContainers.Count}");
        
        // Initialize all item containers
        foreach (var container in itemContainers)
        {
            if (container != null)
            {
                container.Initialize(this);
                container.OnCustomerServed += OnCustomerServedAtContainer;
                Debug.Log($"[TechStore] Initialized TechItemContainer: {container.gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"[TechStore] {gameObject.name} has a null TechItemContainer in the list!");
            }
        }
    }
    
    private void OnDestroy()
    {
        foreach (var container in itemContainers)
        {
            if (container != null)
            {
                container.OnCustomerServed -= OnCustomerServedAtContainer;
            }
        }
    }
    
    /// <summary>
    /// Called when a customer is served at an item container
    /// </summary>
    private void OnCustomerServedAtContainer(TechItemContainer container, TechCustomer customer)
    {
        Debug.Log($"[TechStore] Customer served at container: {container?.name}");
    }
    
    /// <summary>
    /// Finds the item container with the shortest queue that can accept customers
    /// </summary>
    public TechItemContainer FindBestAvailableContainer()
    {
        TechItemContainer bestContainer = null;
        int shortestQueue = int.MaxValue;
        
        foreach (var container in itemContainers)
        {
            if (container == null) continue;
            
            if (container.CanAcceptCustomer && container.QueueCount < shortestQueue)
            {
                shortestQueue = container.QueueCount;
                bestContainer = container;
            }
        }
        
        return bestContainer;
    }
    
    /// <summary>
    /// Check if any item container can accept a new customer
    /// </summary>
    public bool HasAvailableContainer()
    {
        foreach (var container in itemContainers)
        {
            if (container != null && container.CanAcceptCustomer)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get total customers currently in all item container queues
    /// </summary>
    public int GetTotalCustomersInContainerQueues()
    {
        int total = 0;
        foreach (var container in itemContainers)
        {
            if (container != null)
            {
                total += container.QueueCount;
            }
        }
        return total;
    }
    
    /// <summary>
    /// Get the total capacity across all item containers
    /// </summary>
    public int GetTotalContainerCapacity()
    {
        int total = 0;
        foreach (var container in itemContainers)
        {
            if (container != null)
            {
                // We can't access maxQueueSize directly, so use CanAcceptCustomer
                // This returns capacity as "current queue + available slots"
                // For accurate capacity, we'd need to expose maxQueueSize
                total += container.QueueCount + (container.CanAcceptCustomer ? 1 : 0);
            }
        }
        return total;
    }
}
