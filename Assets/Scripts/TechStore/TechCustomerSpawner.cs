using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Spawns tech customers for tech stores.
/// Tracks customers in "entry phase" (from spawn until served at item container).
/// New customer spawns as soon as one is served at an item container.
/// Uses shortest queue selection for item containers.
/// </summary>
public class TechCustomerSpawner : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject techCustomerPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private bool autoSpawn = true;
    
    [Header("Target Tech Stores")]
    [SerializeField] private List<TechStore> targetTechStores = new List<TechStore>();
    [SerializeField] private bool useRoundRobin = true; // If true, cycles through stores. If false, picks randomly.
    
    [Header("Customer Limit")]
    [Tooltip("Maximum number of tech customers in the entry phase (spawn → item container service) PER STORE. Once served at container, they free up a slot for the next customer.")]
    [SerializeField] private int maxConcurrentCustomersPerStore = 10;
    
    private bool isSpawning = false;
    private Dictionary<TechStore, int> customersPerStore = new Dictionary<TechStore, int>();
    private int currentStoreIndex = 0;
    
    public int CurrentCustomerCount => GetTotalCustomerCount();
    public int MaxConcurrentCustomersPerStore => maxConcurrentCustomersPerStore;
    public List<TechStore> TargetTechStores => targetTechStores;
    public int StoreCount => targetTechStores?.Count ?? 0;
    
    private int GetTotalCustomerCount()
    {
        int total = 0;
        foreach (var count in customersPerStore.Values)
        {
            total += count;
        }
        return total;
    }
    
    public int GetCustomerCountForStore(TechStore store)
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
                SpawnTechCustomer();
            }
        }
    }
    
    /// <summary>
    /// Check if we can spawn a customer for any available store
    /// </summary>
    private bool CanSpawn()
    {
        if (targetTechStores == null || targetTechStores.Count == 0) return false;
        
        // Check if any store is available for spawning
        return GetAvailableStoreAndContainer(out _, out _);
    }
    
    /// <summary>
    /// Get an available store and item container that can accept a new customer
    /// Returns true if found, and sets the out parameters
    /// </summary>
    private bool GetAvailableStoreAndContainer(out TechStore store, out TechItemContainer container)
    {
        store = null;
        container = null;
        
        if (targetTechStores == null || targetTechStores.Count == 0) return false;
        
        if (useRoundRobin)
        {
            // Round-robin: try each store starting from currentStoreIndex
            for (int i = 0; i < targetTechStores.Count; i++)
            {
                int storeIndex = (currentStoreIndex + i) % targetTechStores.Count;
                TechStore candidateStore = targetTechStores[storeIndex];
                
                if (IsStoreAvailable(candidateStore, out TechItemContainer availableContainer))
                {
                    currentStoreIndex = (storeIndex + 1) % targetTechStores.Count;
                    store = candidateStore;
                    container = availableContainer;
                    return true;
                }
            }
        }
        else
        {
            // Random selection: shuffle and pick first available
            List<TechStore> shuffled = new List<TechStore>(targetTechStores);
            for (int i = 0; i < shuffled.Count; i++)
            {
                int randomIndex = Random.Range(i, shuffled.Count);
                TechStore temp = shuffled[i];
                shuffled[i] = shuffled[randomIndex];
                shuffled[randomIndex] = temp;
            }
            
            foreach (var candidateStore in shuffled)
            {
                if (IsStoreAvailable(candidateStore, out TechItemContainer availableContainer))
                {
                    store = candidateStore;
                    container = availableContainer;
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if a specific store is available for new customers
    /// </summary>
    private bool IsStoreAvailable(TechStore store, out TechItemContainer availableContainer)
    {
        availableContainer = null;
        
        if (store == null || store.BaseStore == null) return false;
        
        // Check if store GameObject is active in the scene
        if (!store.gameObject.activeInHierarchy) return false;
        
        // Check if store has reached its customer limit
        int currentCount = GetCustomerCountForStore(store);
        if (currentCount >= maxConcurrentCustomersPerStore) return false;
        
        // Check if store needs cleaning
        if (store.BaseStore.NeedsCleaning) return false;
        
        // Check if there's an available item container (with shortest queue)
        availableContainer = store.FindBestAvailableContainer();
        if (availableContainer == null) return false;
        
        // Check if there's room in the cashier queue (for after they get their item)
        ServiceSpot serviceSpot = store.BaseStore.GetAvailableSpotForQueue();
        if (serviceSpot == null) return false;
        
        return true;
    }
    
    public void SpawnTechCustomer()
    {
        if (targetTechStores == null || targetTechStores.Count == 0)
        {
            Debug.LogWarning("[TechCustomerSpawner] No target tech stores assigned!");
            return;
        }
        
        // Get an available store and container
        if (!GetAvailableStoreAndContainer(out TechStore selectedStore, out TechItemContainer selectedContainer))
        {
            Debug.Log("[TechCustomerSpawner] No available tech stores or containers");
            return;
        }
        
        GameObject customerObj = Instantiate(techCustomerPrefab, spawnPoint.position, spawnPoint.rotation);
        TechCustomer techCustomer = customerObj.GetComponent<TechCustomer>();
        
        if (techCustomer != null)
        {
            techCustomer.Initialize(selectedContainer, selectedStore, exitPoint, selectedStore.EnableObservatoryPhase);
            techCustomer.SetSpawner(this);
            
            // Track customer for this specific store
            if (!customersPerStore.ContainsKey(selectedStore))
                customersPerStore[selectedStore] = 0;
            customersPerStore[selectedStore]++;
            
            int storeCount = customersPerStore[selectedStore];
            Debug.Log($"[TechCustomerSpawner] Spawned tech customer for {selectedStore.BaseStore?.StoreName}. Container: {selectedContainer.name}. Store customers: {storeCount}/{maxConcurrentCustomersPerStore}, Total: {GetTotalCustomerCount()}");
        }
        else
        {
            Debug.LogError("[TechCustomerSpawner] Tech customer prefab missing TechCustomer component!");
            Destroy(customerObj);
        }
    }
    
    /// <summary>
    /// Called when a tech customer exits the store (after paying).
    /// Customer already left entry phase when served at container, so no count change.
    /// </summary>
    public void OnCustomerExited()
    {
        Debug.Log($"[TechCustomerSpawner] Customer exited store.");
    }
    
    /// <summary>
    /// Called when a customer is served at an item container - they leave entry phase, spawn replacement immediately
    /// </summary>
    public void OnCustomerServedAtContainer(TechStore store)
    {
        // Customer leaves entry phase when served at item container
        if (customersPerStore.ContainsKey(store))
        {
            customersPerStore[store] = Mathf.Max(0, customersPerStore[store] - 1);
            Debug.Log($"[TechCustomerSpawner] Customer served at container for {store.BaseStore?.StoreName}. Store customers: {customersPerStore[store]}/{maxConcurrentCustomersPerStore}");
        }
        
        // Try to spawn a new customer immediately
        if (CanSpawn())
        {
            SpawnTechCustomer();
        }
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
        
        // Draw lines to all target tech stores
        if (targetTechStores != null && targetTechStores.Count > 0)
        {
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
            
            for (int i = 0; i < targetTechStores.Count; i++)
            {
                if (targetTechStores[i] != null)
                {
                    // Use different colors for each store
                    Gizmos.color = Color.HSVToRGB((float)i / targetTechStores.Count, 1f, 1f);
                    Gizmos.DrawLine(spawnPos, targetTechStores[i].transform.position);
                }
            }
        }
    }
}
