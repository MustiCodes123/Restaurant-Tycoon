using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Food store controller that manages the food preparation and dining flow.
/// Attach this to the root of a food store alongside the base Store component.
/// </summary>
public class FoodStore : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Store baseStore;
    
    [Header("Pickup")]
    [SerializeField] private PickupPoint pickupPoint;
    
    [Header("Item Containers")]
    [SerializeField] private List<ItemContainer> itemContainers = new List<ItemContainer>();
    
    [Header("Dining")]
    [SerializeField] private DiningArea diningArea;
    
    [Header("Tray Settings")]
    [SerializeField] private GameObject trayPrefab;
    
    [Header("Observatory Phase")]
    [Tooltip("Enable or disable the observatory phase where customers observe before going to cashier")]
    [SerializeField] private bool enableObservatoryPhase = true;
    
    private FoodCustomer currentPickupCustomer;
    private FoodTray currentTray;
    private int preparedItemCount = 0;
    private bool isWaitingForSeat = false; // True when front customer has food but waiting for seat
    
    public Store BaseStore => baseStore;
    public PickupPoint PickupPoint => pickupPoint;
    public List<ItemContainer> ItemContainers => itemContainers;
    public DiningArea DiningArea => diningArea;
    public int TotalItemCount => itemContainers.Count;
    public int PreparedItemCount => preparedItemCount;
    public bool AllItemsPrepared => preparedItemCount >= itemContainers.Count;
    public FoodCustomer CurrentPickupCustomer => currentPickupCustomer;
    public bool HasCustomerAtPickup => currentPickupCustomer != null;
    public bool EnableObservatoryPhase => enableObservatoryPhase;
    
    /// <summary>
    /// Returns true only if front customer is waiting for food (not waiting for seat)
    /// Used by ItemContainer to know if preparation should proceed
    /// </summary>
    public bool CanServeCustomer => currentPickupCustomer != null && !isWaitingForSeat;
    
    private void Awake()
    {
        if (baseStore == null)
        {
            baseStore = GetComponent<Store>();
        }
    }
    
    private void Start()
    {
        Debug.Log($"[FoodStore] {gameObject.name} Start() - ItemContainers count: {itemContainers.Count}, PickupPoint: {pickupPoint != null}, DiningArea: {diningArea != null}");
        
        // Subscribe to item container events
        foreach (var container in itemContainers)
        {
            if (container != null)
            {
                container.Initialize(this);
                container.OnItemPrepared += OnItemPrepared;
                Debug.Log($"[FoodStore] Initialized ItemContainer: {container.gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"[FoodStore] {gameObject.name} has a null ItemContainer in the list!");
            }
        }
        
        // Subscribe to pickup point events
        if (pickupPoint != null)
        {
            pickupPoint.Initialize(this);
            Debug.Log($"[FoodStore] Initialized PickupPoint: {pickupPoint.gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[FoodStore] {gameObject.name} has no PickupPoint assigned!");
        }
        
        // Subscribe to dining area events
        if (diningArea != null)
        {
            diningArea.OnSeatBecameAvailable += OnSeatBecameAvailable;
        }
    }
    
    private void OnDestroy()
    {
        foreach (var container in itemContainers)
        {
            if (container != null)
            {
                container.OnItemPrepared -= OnItemPrepared;
            }
        }
        
        if (diningArea != null)
        {
            diningArea.OnSeatBecameAvailable -= OnSeatBecameAvailable;
        }
    }
    
    /// <summary>
    /// Called when a customer arrives at the pickup point
    /// </summary>
    public void OnCustomerArrivedAtPickup(FoodCustomer customer)
    {
        Debug.Log($"[FoodStore] OnCustomerArrivedAtPickup called for {customer?.name ?? "NULL"}. Current pickup customer: {currentPickupCustomer?.name ?? "NONE"}");
        
        if (currentPickupCustomer != null)
        {
            Debug.LogWarning("[FoodStore] Customer already at pickup point! Ignoring new customer.");
            return;
        }
        
        currentPickupCustomer = customer;
        preparedItemCount = 0;
        
        // Reset all item containers and show waiting indicators
        foreach (var container in itemContainers)
        {
            container.ResetContainer();
            container.ShowWaitingIndicator();
        }
        
        // Spawn tray at pickup point
        SpawnTray();
        
        // Show item preparation UI
        if (pickupPoint != null)
        {
            pickupPoint.ShowItemUI();
        }
        
        Debug.Log($"[FoodStore] Customer arrived at pickup. {itemContainers.Count} items to prepare.");
    }
    
    private void SpawnTray()
    {
        if (trayPrefab == null)
        {
            Debug.LogError("[FoodStore] Cannot spawn tray - trayPrefab is not assigned!");
            return;
        }
        
        if (pickupPoint == null || pickupPoint.TraySpawnPoint == null)
        {
            Debug.LogError("[FoodStore] Cannot spawn tray - pickupPoint or TraySpawnPoint is null!");
            return;
        }
        
        GameObject trayObj = Instantiate(trayPrefab, pickupPoint.TraySpawnPoint.position, pickupPoint.TraySpawnPoint.rotation);
        currentTray = trayObj.GetComponent<FoodTray>();
        
        if (currentTray == null)
        {
            // Try to add FoodTray component if missing
            Debug.LogWarning("[FoodStore] Tray prefab is missing FoodTray component! Adding one automatically.");
            currentTray = trayObj.AddComponent<FoodTray>();
        }
        
        currentTray.Initialize(itemContainers.Count);
        
        Debug.Log($"[FoodStore] Tray spawned at pickup point. FoodTray component: {currentTray != null}");
    }
    
    private void OnItemPrepared(ItemContainer container)
    {
        preparedItemCount++;
        
        // Hide this container's waiting indicator since it's now prepared
        container.HideWaitingIndicator();
        
        // Activate corresponding item on tray
        if (currentTray != null)
        {
            int containerIndex = itemContainers.IndexOf(container);
            currentTray.ActivateItem(containerIndex);
        }
        
        // Update UI
        if (pickupPoint != null)
        {
            pickupPoint.UpdateItemUI(preparedItemCount, itemContainers.Count);
        }
        
        Debug.Log($"[FoodStore] Item prepared: {preparedItemCount}/{itemContainers.Count}");
        
        // Check if all items prepared
        if (AllItemsPrepared)
        {
            OnAllItemsPrepared();
        }
    }
    
    private void OnAllItemsPrepared()
    {
        Debug.Log("[FoodStore] All items prepared! Customer can take tray.");
        
        if (pickupPoint != null)
        {
            pickupPoint.HideItemUI();
        }
        
        // Notify customer that food is ready
        if (currentPickupCustomer != null)
        {
            currentPickupCustomer.OnFoodReady(currentTray);
        }
    }
    
    /// <summary>
    /// Called when customer picks up the tray and leaves pickup point
    /// </summary>
    public void OnCustomerLeftPickup()
    {
        Debug.Log("[FoodStore] OnCustomerLeftPickup called. Clearing current customer...");
        
        currentPickupCustomer = null;
        currentTray = null;
        preparedItemCount = 0;
        isWaitingForSeat = false;
        
        // Hide all waiting indicators
        foreach (var container in itemContainers)
        {
            container.HideWaitingIndicator();
        }
        
        Debug.Log("[FoodStore] Customer left pickup. Ready for next customer.");
        
        // IMPORTANT: Immediately check if next customer in queue can be served
        // This must happen AFTER clearing currentPickupCustomer
        if (pickupPoint != null && pickupPoint.QueueCount > 0)
        {
            Debug.Log($"[FoodStore] {pickupPoint.QueueCount} customers still in pickup queue. Checking front customer...");
            
            var frontCustomer = pickupPoint.FrontCustomer;
            if (frontCustomer != null && frontCustomer.State == FoodCustomer.FoodCustomerState.WaitingForFood)
            {
                Debug.Log("[FoodStore] Front customer is waiting for food. Starting service...");
                OnCustomerArrivedAtPickup(frontCustomer);
            }
        }
    }
    
    /// <summary>
    /// Called when next customer in pickup queue can be served
    /// </summary>
    public void OnNextCustomerInPickupQueue()
    {
        // If there's a customer waiting and no one being served, start serving
        if (currentPickupCustomer == null && pickupPoint != null && pickupPoint.CanServeFrontCustomer)
        {
            pickupPoint.OnFrontCustomerReadyForService(pickupPoint.FrontCustomer);
        }
    }
    
    /// <summary>
    /// Called when a customer with food is waiting for a seat
    /// This blocks serving the next customer in pickup queue
    /// </summary>
    public void OnCustomerWaitingForSeat(FoodCustomer customer)
    {
        isWaitingForSeat = true;
        
        // Hide item container indicators since we can't serve next customer
        foreach (var container in itemContainers)
        {
            container.HideWaitingIndicator();
        }
        
        Debug.Log("[FoodStore] Customer waiting for seat - item containers blocked");
    }
    
    /// <summary>
    /// Called when a seat becomes available (garbage picked up)
    /// </summary>
    public void OnSeatBecameAvailable()
    {
        // If front customer is waiting for seat, notify them
        if (isWaitingForSeat && pickupPoint != null)
        {
            pickupPoint.NotifyFrontCustomerSeatAvailable();
        }
    }
    
    /// <summary>
    /// Finds an available seat in the dining area
    /// </summary>
    public DiningSeat FindAvailableSeat()
    {
        if (diningArea == null) return null;
        return diningArea.FindAvailableSeat();
    }
    
    /// <summary>
    /// Checks if there's any available seat
    /// </summary>
    public bool HasAvailableSeat()
    {
        if (diningArea == null) return false;
        return diningArea.HasAvailableSeat();
    }
}
