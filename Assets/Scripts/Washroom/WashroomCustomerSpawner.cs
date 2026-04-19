using UnityEngine;
using System.Collections;

/// <summary>
/// Spawns customers for the washroom at regular intervals.
/// </summary>
public class WashroomCustomerSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Washroom targetWashroom;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private GameObject customerPrefab;
    
    [Header("Spawn Settings")]
    [SerializeField] private float minSpawnInterval = 5f;
    [SerializeField] private float maxSpawnInterval = 15f;
    [SerializeField] private bool autoSpawn = true;
    
    private Coroutine spawnCoroutine;
    
    private void Start()
    {
        if (autoSpawn && targetWashroom != null)
        {
            StartSpawning();
        }
    }
    
    public void StartSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
        spawnCoroutine = StartCoroutine(SpawnCoroutine());
    }
    
    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }
    
    private IEnumerator SpawnCoroutine()
    {
        while (true)
        {
            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);
            
            TrySpawnCustomer();
        }
    }
    
    private void TrySpawnCustomer()
    {
        if (targetWashroom == null || customerPrefab == null) return;
        
        // Check if any stall can accept a customer
        if (!targetWashroom.CanAcceptCustomer())
        {
            Debug.Log("[WashroomCustomerSpawner] No stall has queue space, not spawning");
            return;
        }
        
        // Check if any stall is available or will be soon (not ALL stalls need toilet paper)
        if (targetWashroom.GetStallsNeedingToiletPaper() == targetWashroom.StallCount)
        {
            Debug.Log("[WashroomCustomerSpawner] All stalls need toilet paper, not spawning");
            return;
        }
        
        SpawnCustomer();
    }
    
    private void SpawnCustomer()
    {
        // Find a stall with queue space (prefer shortest queue)
        WashroomStall targetStall = targetWashroom.GetStallWithShortestQueue();
        
        if (targetStall == null)
        {
            Debug.Log("[WashroomCustomerSpawner] No stall available for spawn");
            return;
        }
        
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        
        GameObject customerObj = Instantiate(customerPrefab, spawnPos, spawnRot);
        WashroomCustomer customer = customerObj.GetComponent<WashroomCustomer>();
        
        if (customer != null)
        {
            customer.Initialize(targetWashroom, targetStall, exitPoint);
            Debug.Log($"[WashroomCustomerSpawner] Spawned washroom customer for stall {targetStall.gameObject.name}");
        }
        else
        {
            Debug.LogError("[WashroomCustomerSpawner] Customer prefab missing WashroomCustomer component!");
            Destroy(customerObj);
        }
    }
    
    [ContextMenu("Spawn Customer Now")]
    public void SpawnCustomerNow()
    {
        SpawnCustomer();
    }
}
