using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// The pickup counter where customers wait for their food.
/// Has a proper queue system like ServiceSpot.
/// Shows item preparation progress UI when customer is waiting.
/// </summary>
public class PickupPoint : MonoBehaviour
{
    [Header("Positions")]
    [SerializeField] private Transform customerWaitPoint;
    [SerializeField] private Transform traySpawnPoint;
    
    [Header("Queue Settings")]
    [SerializeField] private int maxQueueSize = 5;
    [SerializeField] private float queueSpacing = 1.5f;
    [SerializeField] private Transform queueDirection; // Direction queue extends from wait point
    
    [Header("UI")]
    [SerializeField] private Canvas itemUICanvas;
    [SerializeField] private GameObject itemUIContainer;
    [SerializeField] private Image[] itemIcons; // Icons for each item to prepare
    [SerializeField] private Color preparedColor = Color.green;
    [SerializeField] private Color unpreparedColor = Color.gray;
    
    [Header("Animation")]
    [SerializeField] private float iconPunchScale = 1.3f;
    [SerializeField] private float iconPunchDuration = 0.3f;
    
    private FoodStore parentFoodStore;
    private List<FoodCustomer> queue = new List<FoodCustomer>();
    
    public Transform CustomerWaitPoint => customerWaitPoint;
    public Transform TraySpawnPoint => traySpawnPoint;
    public bool HasCustomerWaiting => queue.Count > 0;
    public int QueueCount => queue.Count;
    public int MaxQueueSize => maxQueueSize;
    public bool CanAcceptCustomer => queue.Count < maxQueueSize;
    public FoodCustomer FrontCustomer => queue.Count > 0 ? queue[0] : null;
    
    /// <summary>
    /// Check if the front customer is ready to be served (waiting for food, not waiting for seat)
    /// </summary>
    public bool CanServeFrontCustomer
    {
        get
        {
            if (queue.Count == 0) return false;
            var front = queue[0];
            // Can only serve if front customer is waiting for food (not waiting for seat)
            return front != null && front.State == FoodCustomer.FoodCustomerState.WaitingForFood;
        }
    }
    
    public void Initialize(FoodStore foodStore)
    {
        parentFoodStore = foodStore;
        HideItemUI();
    }
    
    /// <summary>
    /// Add a customer to the pickup queue
    /// </summary>
    public int AddCustomerToQueue(FoodCustomer customer)
    {
        if (customer == null)
        {
            Debug.LogError("[PickupPoint] Cannot add null customer to queue!");
            return -1;
        }
        
        if (queue.Count >= maxQueueSize)
        {
            Debug.LogWarning("[PickupPoint] Queue is full!");
            return -1;
        }
        
        queue.Add(customer);
        int position = queue.Count - 1;
        Debug.Log($"[PickupPoint] Customer added at position {position}. Queue size: {queue.Count}");
        return position;
    }
    
    /// <summary>
    /// Gets the world position for a specific queue position
    /// </summary>
    public Vector3 GetQueueWorldPosition(int position)
    {
        if (customerWaitPoint == null) return transform.position;
        
        // Position 0 is at the wait point (pickup position)
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
    /// Remove a customer from the queue and advance others
    /// </summary>
    public void RemoveCustomer(FoodCustomer customer)
    {
        int index = queue.IndexOf(customer);
        if (index < 0) 
        {
            Debug.LogWarning($"[PickupPoint] Customer not found in queue!");
            return;
        }
        
        queue.RemoveAt(index);
        Debug.Log($"[PickupPoint] Customer removed from position {index}. Queue size: {queue.Count}");
        
        // Update positions for all customers behind the removed one
        for (int i = index; i < queue.Count; i++)
        {
            Debug.Log($"[PickupPoint] Advancing customer at {i+1} to position {i}");
            queue[i].OnPickupQueuePositionChanged(i);
        }
    }
    
    /// <summary>
    /// Called when front customer is ready to be served
    /// </summary>
    public void OnFrontCustomerReadyForService(FoodCustomer customer)
    {
        if (queue.Count == 0)
        {
            Debug.LogWarning("[PickupPoint] OnFrontCustomerReadyForService called but queue is empty!");
            return;
        }
        
        if (queue[0] != customer)
        {
            Debug.LogWarning("[PickupPoint] OnFrontCustomerReadyForService called for non-front customer!");
            return;
        }
        
        Debug.Log("[PickupPoint] Front customer ready for service. Notifying FoodStore...");
        parentFoodStore?.OnCustomerArrivedAtPickup(customer);
    }
    
    /// <summary>
    /// Called when customer leaves this pickup point (got food and moving to table)
    /// </summary>
    public void OnCustomerLeft(FoodCustomer customer)
    {
        Debug.Log($"[PickupPoint] OnCustomerLeft called. Queue size before: {queue.Count}");
        RemoveCustomer(customer);
        HideItemUI();
        Debug.Log($"[PickupPoint] Calling OnCustomerLeftPickup. Queue size after: {queue.Count}");
        parentFoodStore?.OnCustomerLeftPickup();
    }
    
    /// <summary>
    /// Notify front customer that a seat is now available
    /// </summary>
    public void NotifyFrontCustomerSeatAvailable()
    {
        if (queue.Count > 0)
        {
            var front = queue[0];
            if (front.State == FoodCustomer.FoodCustomerState.NoSeatAvailable)
            {
                front.OnSeatBecameAvailable();
            }
        }
    }
    
    public void ShowItemUI()
    {
        if (itemUICanvas != null)
        {
            itemUICanvas.enabled = true;
        }
        
        if (itemUIContainer != null)
        {
            itemUIContainer.SetActive(true);
        }
        
        // Reset all icons to unprepared state
        ResetItemIcons();
    }
    
    public void HideItemUI()
    {
        if (itemUICanvas != null)
        {
            itemUICanvas.enabled = false;
        }
        
        if (itemUIContainer != null)
        {
            itemUIContainer.SetActive(false);
        }
    }
    
    private void ResetItemIcons()
    {
        if (itemIcons == null) return;
        
        int itemCount = parentFoodStore?.TotalItemCount ?? 0;
        
        for (int i = 0; i < itemIcons.Length; i++)
        {
            if (itemIcons[i] != null)
            {
                // Show only icons for items that exist
                itemIcons[i].gameObject.SetActive(i < itemCount);
                itemIcons[i].color = unpreparedColor;
            }
        }
    }
    
    public void UpdateItemUI(int preparedCount, int totalCount)
    {
        if (itemIcons == null) return;
        
        for (int i = 0; i < itemIcons.Length && i < totalCount; i++)
        {
            if (itemIcons[i] != null)
            {
                bool isPrepared = i < preparedCount;
                
                // If this item just got prepared, animate it
                if (isPrepared && itemIcons[i].color != preparedColor)
                {
                    itemIcons[i].color = preparedColor;
                    
                    // Punch scale animation
                    itemIcons[i].transform.DOPunchScale(Vector3.one * (iconPunchScale - 1f), iconPunchDuration, 1, 0.5f);
                }
            }
        }
    }
    
    private void OnDrawGizmos()
    {
        // Draw customer wait point
        if (customerWaitPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(customerWaitPoint.position, 0.3f);
            Gizmos.DrawLine(transform.position, customerWaitPoint.position);
        }
        
        // Draw tray spawn point
        if (traySpawnPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(traySpawnPoint.position, new Vector3(0.3f, 0.1f, 0.3f));
        }
        
        // Draw queue positions
        if (customerWaitPoint != null)
        {
            for (int i = 1; i < maxQueueSize; i++)
            {
                Vector3 pos = GetQueueWorldPosition(i);
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(pos, 0.2f);
            }
        }
    }
}
