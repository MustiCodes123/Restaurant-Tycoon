using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Spawns food customers for food stores.
/// Tracks customers from spawn until they leave the pickup queue (when they get their food).
/// This ensures we don't spawn more customers than the pickup system can handle.
/// </summary>
public class FoodCustomerSpawner : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject foodCustomerPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private bool autoSpawn = true;
    
    [Header("Target Food Stores")]
    [SerializeField] private List<FoodStore> targetFoodStores = new List<FoodStore>();
    [SerializeField] private bool useRoundRobin = true; // If true, cycles through stores. If false, picks randomly.
    
    [Header("Customer Limit")]
    [Tooltip("Maximum customers per store (from spawn until leaving pickup). The actual limit is the MINIMUM of this value and the store's pickup queue max size.")]
    [SerializeField] private int maxConcurrentCustomersPerStore = 1;
    
    private bool isSpawning = false;
    private Dictionary<FoodStore, int> customersPerStore = new Dictionary<FoodStore, int>(); // Track customers per store
    private int currentStoreIndex = 0; // For round-robin selection
    
    public int CurrentCustomerCount => GetTotalCustomerCount();
    public int MaxConcurrentCustomersPerStore => maxConcurrentCustomersPerStore;
    public List<FoodStore> TargetFoodStores => targetFoodStores;
    public int StoreCount => targetFoodStores?.Count ?? 0;
    
    private int GetTotalCustomerCount()
    {
        int total = 0;
        foreach (var count in customersPerStore.Values)
        {
            total += count;
        }
        return total;
    }
    
    public int GetCustomerCountForStore(FoodStore store)
    {
        return customersPerStore.ContainsKey(store) ? customersPerStore[store] : 0;
    }
    
    private void Start()
    {
        if (spawnPoint == null) spawnPoint = transform;
        
        if (autoSpawn)
        {
            StartCoroutine(SpawnRoutine());
        }
    }
    
    private IEnumerator SpawnRoutine()
    {
        while (autoSpawn)
        {
            yield return new WaitForSeconds(spawnInterval);
            
            if (CanSpawn())
            {
                SpawnFoodCustomer();
            }
        }
    }
    
    /// <summary>
    /// Check if we can spawn a customer for any available store
    /// </summary>
    private bool CanSpawn()
    {
        if (targetFoodStores == null || targetFoodStores.Count == 0) return false;
        
        // Check if any store is available for spawning (has room for more customers)
        return GetAvailableStore() != null;
    }
    
    /// <summary>
    /// Get an available store that can accept a new customer
    /// </summary>
    private FoodStore GetAvailableStore()
    {
        if (targetFoodStores == null || targetFoodStores.Count == 0) return null;
        
        if (useRoundRobin)
        {
            // Round-robin: try each store starting from currentStoreIndex
            for (int i = 0; i < targetFoodStores.Count; i++)
            {
                int storeIndex = (currentStoreIndex + i) % targetFoodStores.Count;
                FoodStore store = targetFoodStores[storeIndex];
                
                if (IsStoreAvailable(store))
                {
                    currentStoreIndex = (storeIndex + 1) % targetFoodStores.Count; // Update for next spawn
                    return store;
                }
            }
        }
        else
        {
            // Random selection: shuffle and pick first available
            List<FoodStore> shuffled = new List<FoodStore>(targetFoodStores);
            for (int i = 0; i < shuffled.Count; i++)
            {
                int randomIndex = Random.Range(i, shuffled.Count);
                FoodStore temp = shuffled[i];
                shuffled[i] = shuffled[randomIndex];
                shuffled[randomIndex] = temp;
            }
            
            foreach (var store in shuffled)
            {
                if (IsStoreAvailable(store))
                    return store;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Check if a specific store is available for new customers.
    /// We limit customers based on the pickup queue capacity - if pickup max is 1,
    /// only 1 customer can be in the entire flow (cashier → pickup) at a time.
    /// </summary>
    private bool IsStoreAvailable(FoodStore store)
    {
        if (store == null || store.BaseStore == null) return false;
        
        // Check if store GameObject is active in the scene
        if (!store.gameObject.activeInHierarchy) return false;
        
        // Check if store needs cleaning
        if (store.BaseStore.NeedsCleaning) return false;
        
        // Get the pickup queue max size - this is our hard limit
        int pickupMaxSize = store.PickupPoint != null ? store.PickupPoint.MaxQueueSize : 1;
        
        // Current tracked customers (from spawn until they leave pickup)
        int currentCount = GetCustomerCountForStore(store);
        
        // Don't spawn if we already have as many customers as the pickup queue can handle
        // This ensures that if pickup max is 1, only 1 customer total can be in cashier+pickup
        if (currentCount >= pickupMaxSize)
        {
            Debug.Log($"[FoodCustomerSpawner] Store {store.BaseStore?.StoreName} at capacity: {currentCount}/{pickupMaxSize} (pickup queue max)");
            return false;
        }
        
        // Also respect the per-store limit if it's more restrictive
        if (currentCount >= maxConcurrentCustomersPerStore)
        {
            Debug.Log($"[FoodCustomerSpawner] Store {store.BaseStore?.StoreName} at spawner limit: {currentCount}/{maxConcurrentCustomersPerStore}");
            return false;
        }
        
        // Check if there's room in the cashier queue
        ServiceSpot serviceSpot = store.BaseStore.GetAvailableSpotForQueue();
        return serviceSpot != null;
    }
    
    public void SpawnFoodCustomer()
    {
        if (targetFoodStores == null || targetFoodStores.Count == 0)
        {
            Debug.LogWarning("[FoodCustomerSpawner] No target food stores assigned!");
            return;
        }
        
        // Get an available store
        FoodStore selectedStore = GetAvailableStore();
        if (selectedStore == null)
        {
            Debug.Log("[FoodCustomerSpawner] No available food stores");
            return;
        }
        
        ServiceSpot serviceSpot = selectedStore.BaseStore?.GetAvailableSpotForQueue();
        if (serviceSpot == null)
        {
            Debug.Log($"[FoodCustomerSpawner] No available service spots for {selectedStore.BaseStore?.StoreName}");
            return;
        }
        
        GameObject customerObj = Instantiate(foodCustomerPrefab, spawnPoint.position, spawnPoint.rotation);
        FoodCustomer foodCustomer = customerObj.GetComponent<FoodCustomer>();
        
        if (foodCustomer != null)
        {
            foodCustomer.Initialize(serviceSpot, selectedStore, exitPoint, selectedStore.EnableObservatoryPhase);
            foodCustomer.SetSpawner(this); // Register spawner for tracking
            
            // Track customer for this specific store
            if (!customersPerStore.ContainsKey(selectedStore))
                customersPerStore[selectedStore] = 0;
            customersPerStore[selectedStore]++;
            
            int storeCount = customersPerStore[selectedStore];
            Debug.Log($"[FoodCustomerSpawner] Spawned food customer for {selectedStore.BaseStore?.StoreName}. Store customers: {storeCount}/{maxConcurrentCustomersPerStore}, Total: {GetTotalCustomerCount()}");
        }
        else
        {
            Debug.LogError("[FoodCustomerSpawner] Food customer prefab missing FoodCustomer component!");
            Destroy(customerObj);
        }
    }
    
    /// <summary>
    /// Called when a food customer exits the store (after eating).
    /// No longer affects spawn count since customer already left entry phase when served.
    /// </summary>
    public void OnCustomerExited()
    {
        Debug.Log($"[FoodCustomerSpawner] Customer exited store.");
    }
    
    /// <summary>
    /// Called when a customer is served at cashier - just log it, don't decrement count yet
    /// Customer count is decremented when they leave the pickup queue
    /// </summary>
    public void OnCustomerServedAtCashier(FoodStore store)
    {
        Debug.Log($"[FoodCustomerSpawner] Customer served at cashier for {store.BaseStore?.StoreName}. Store customers: {GetCustomerCountForStore(store)}/{maxConcurrentCustomersPerStore}");
        
        // Don't decrement here - wait until customer leaves pickup queue
        // Don't spawn here either - wait until pickup queue frees up
    }
    
    /// <summary>
    /// Called when a customer leaves the pickup queue (got their food, heading to table)
    /// This is when we decrement the count and can spawn a replacement
    /// </summary>
    public void OnCustomerLeftPickup(FoodStore store)
    {
        // Customer leaves tracked count when they leave the pickup queue
        if (customersPerStore.ContainsKey(store))
        {
            customersPerStore[store] = Mathf.Max(0, customersPerStore[store] - 1);
            Debug.Log($"[FoodCustomerSpawner] Customer left pickup for {store.BaseStore?.StoreName}. Store customers: {customersPerStore[store]}/{maxConcurrentCustomersPerStore}");
        }
        
        // Try to spawn a new customer immediately
        if (CanSpawn())
        {
            SpawnFoodCustomer();
        }
    }
    
    /// <summary>
    /// Called when a customer is served at pickup point (food given) - spawn replacement immediately
    /// This ensures continuous flow even if cashier spawning didn't trigger
    /// </summary>
    public void OnCustomerServedAtPickup()
    {
        Debug.Log($"[FoodCustomerSpawner] Customer served at pickup. Total customers: {GetTotalCustomerCount()}. Checking spawn...");
        
        // The actual spawn will happen when the customer leaves pickup via OnCustomerLeftPickup
    }
    
    public void SetSpawnInterval(float interval)
    {
        spawnInterval = interval;
    }
    
    public void SetAutoSpawn(bool enabled)
    {
        autoSpawn = enabled;
        if (enabled && !isSpawning)
        {
            StartCoroutine(SpawnRoutine());
        }
    }
    
    private void OnDrawGizmos()
    {
        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);
        }
        
        if (exitPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(exitPoint.position, 0.5f);
        }
        
        // Draw lines to all target food stores
        if (targetFoodStores != null && targetFoodStores.Count > 0)
        {
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
            
            for (int i = 0; i < targetFoodStores.Count; i++)
            {
                if (targetFoodStores[i] != null)
                {
                    // Use different colors for each store
                    Gizmos.color = Color.HSVToRGB((float)i / targetFoodStores.Count, 1f, 1f);
                    Gizmos.DrawLine(spawnPos, targetFoodStores[i].transform.position);
                }
            }
        }
    }
}
