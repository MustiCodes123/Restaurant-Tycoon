using UnityEngine;
using System;
using System.Collections.Generic;

public class ServiceSpot : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Transform customerWaitPoint;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("Queue Settings")]
    [SerializeField] private int maxQueueSize = 2;
    [SerializeField] private float queueSpacing = 1.5f;
    [SerializeField] private Transform queueDirection; // Direction queue extends from wait point
    
    [Header("Store Reference")]
    [SerializeField] private Store parentStore;
    
    // Cached FoodStore reference (if this is a food store)
    private FoodStore parentFoodStore;
    
    private List<IQueueableCustomer> queue = new List<IQueueableCustomer>();
    private bool isBeingServiced;
    
    private void Awake()
    {
        // Auto-find parent store if not assigned
        if (parentStore == null)
        {
            parentStore = GetComponentInParent<Store>();
            if (parentStore != null)
            {
                Debug.Log($"[ServiceSpot] {name} auto-assigned parent store: {parentStore.StoreName}");
            }
            else
            {
                Debug.LogWarning($"[ServiceSpot] {name} could not find parent Store component!");
            }
        }
        
        // Cache FoodStore reference if this is a food store
        parentFoodStore = GetComponentInParent<FoodStore>();
    }
    
    public int QueueCount => queue.Count;
    public bool HasCustomerWaiting => queue.Count > 0 && queue[0].IsWaitingAtStore;
    public bool CanAcceptCustomer => queue.Count < maxQueueSize;
    public int MaxQueueSize => maxQueueSize;
    
    public bool IsOccupied => queue.Count > 0;
    
    public bool CanServe
    {
        get
        {
            if (queue.Count == 0) return false;
            // Can only serve if front customer has arrived and is waiting
            if (!queue[0].IsWaitingAtStore) return false;
            
            return true;
        }
    }
    
    public IQueueableCustomer CurrentCustomer => queue.Count > 0 ? queue[0] : null;
    public Transform CustomerWaitPoint => customerWaitPoint;
    public Vector3 Position => transform.position;
    public Store ParentStore => parentStore;
    
    public event Action<ServiceSpot> OnPlayerEntered;
    public event Action<ServiceSpot> OnPlayerExited;
    
    /// <summary>
    /// Adds a customer to this spot's queue and returns their queue position
    /// </summary>
    public int AddCustomerToQueue(IQueueableCustomer customer)
    {
        if (customer == null)
        {
            Debug.LogError($"[ServiceSpot] {name} - Cannot add null customer to queue!");
            return -1;
        }
        
        if (queue.Count >= maxQueueSize)
        {
            Debug.LogWarning($"[ServiceSpot] {name} queue is full!");
            return -1;
        }
        
        queue.Add(customer);
        int position = queue.Count - 1;
        Debug.Log($"[ServiceSpot] {name} - Customer added at position {position}. Queue size: {queue.Count}");
        return position;
    }
    
    /// <summary>
    /// Gets the world position for a specific queue position
    /// </summary>
    public Vector3 GetQueueWorldPosition(int position)
    {
        if (customerWaitPoint == null) return transform.position;
        
        // Position 0 is at the wait point (service position)
        if (position == 0)
        {
            return customerWaitPoint.position;
        }
        
        // Calculate queue direction
        Vector3 direction;
        if (queueDirection != null)
        {
            direction = (queueDirection.position - customerWaitPoint.position).normalized;
        }
        else
        {
            // Default: queue extends backward from the spot
            direction = -transform.forward;
        }
        
        return customerWaitPoint.position + direction * (position * queueSpacing);
    }
    
    /// <summary>
    /// Removes a customer from the queue and advances others
    /// </summary>
    public void RemoveCustomer(IQueueableCustomer customer)
    {
        int index = queue.IndexOf(customer);
        if (index < 0) return;
        
        queue.RemoveAt(index);
        Debug.Log($"[ServiceSpot] {name} - Customer removed from position {index}. Queue size: {queue.Count}");
        
        // Update positions for all customers behind the removed one
        for (int i = index; i < queue.Count; i++)
        {
            queue[i].OnQueuePositionChanged(i, this);
        }
        
        isBeingServiced = false;
    }
    
    /// <summary>
    /// Gets the front customer in the queue
    /// </summary>
    public IQueueableCustomer GetFrontCustomer()
    {
        if (queue.Count == 0) return null;
        if (!queue[0].IsWaitingAtStore) return null;
        return queue[0];
    }
    
    public void StartService()
    {
        if (queue.Count > 0)
        {
            isBeingServiced = true;
        }
    }
    
    public void CompleteService()
    {
        if (queue.Count == 0) return;
        
        IQueueableCustomer frontCustomer = queue[0];
        if (frontCustomer != null)
        {
            frontCustomer.OnServed();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            OnPlayerEntered?.Invoke(this);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            OnPlayerExited?.Invoke(this);
        }
    }
    
    private void OnDrawGizmos()
    {
        // Draw service position
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        if (customerWaitPoint != null)
        {
            // Draw wait point
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(customerWaitPoint.position, 0.3f);
            Gizmos.DrawLine(transform.position, customerWaitPoint.position);
            
            // Draw queue positions
            Gizmos.color = Color.yellow;
            for (int i = 0; i < maxQueueSize; i++)
            {
                Vector3 pos = GetQueueWorldPosition(i);
                Gizmos.DrawWireSphere(pos, 0.2f);
                if (i > 0)
                {
                    Vector3 prevPos = GetQueueWorldPosition(i - 1);
                    Gizmos.DrawLine(prevPos, pos);
                }
            }
        }
    }
}
