using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject customerPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private bool autoSpawn = true;
    
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
                SpawnCustomer();
            }
        }
    }
    
    /// <summary>
    /// Checks if we can spawn a customer by finding an available service spot
    /// </summary>
    private bool CanSpawn()
    {
        return FindAvailableServiceSpot() != null;
    }
    
    /// <summary>
    /// Finds a service spot that can accept more customers in its queue
    /// Excludes stores that have a FoodCustomerSpawner assigned
    /// </summary>
    private ServiceSpot FindAvailableServiceSpot()
    {
        // Find all active stores
        Store[] allStores = FindObjectsOfType<Store>();
        
        // Find all food customer spawners to know which stores to exclude
        FoodCustomerSpawner[] foodSpawners = FindObjectsOfType<FoodCustomerSpawner>();
        HashSet<Store> foodStores = new HashSet<Store>();
        foreach (var foodSpawner in foodSpawners)
        {
            // Add all target food stores from each spawner
            if (foodSpawner.TargetFoodStores != null)
            {
                foreach (var foodStore in foodSpawner.TargetFoodStores)
                {
                    if (foodStore?.BaseStore != null)
                    {
                        foodStores.Add(foodStore.BaseStore);
                    }
                }
            }
        }
        
        foreach (Store store in allStores)
        {
            if (!store.gameObject.activeInHierarchy) continue;
            if (store.NeedsCleaning) continue; // Skip dirty stores
            
            // Skip stores that have a FoodCustomerSpawner handling them
            if (foodStores.Contains(store)) continue;
            
            // Check each service spot in the store
            ServiceSpot spot = store.GetAvailableSpotForQueue();
            if (spot != null)
            {
                return spot;
            }
        }
        
        return null;
    }
    
    public void SpawnCustomer()
    {
        ServiceSpot targetSpot = FindAvailableServiceSpot();
        if (targetSpot == null)
        {
            Debug.Log("[CustomerSpawner] No available service spots to spawn customer");
            return;
        }
        
        GameObject customerObj = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);
        Customer customer = customerObj.GetComponent<Customer>();
        customer.Initialize(targetSpot, exitPoint);
        
        Debug.Log($"[CustomerSpawner] Spawned customer for {targetSpot.ParentStore?.StoreName ?? "Unknown Store"}");
    }
    
    public void SetSpawnInterval(float interval)
    {
        spawnInterval = interval;
    }
    
    public void SetAutoSpawn(bool enabled)
    {
        autoSpawn = enabled;
        if (enabled)
        {
            StartCoroutine(SpawnRoutine());
        }
    }
    
    private void OnDrawGizmos()
    {
        if (spawnPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);
        }
        
        if (exitPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(exitPoint.position, 0.5f);
        }
    }
}
