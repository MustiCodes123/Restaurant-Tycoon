using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Rack that holds toilet paper rolls. Player can pick up rolls when standing nearby.
/// Uses PlayerCarryController for unified carry system.
/// </summary>
public class ToiletPaperRack : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Layer mask for detecting player. Make sure player is on this layer!")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private GameObject toiletPaperPrefab;
    
    [Header("Dispensing")]
    [Tooltip("Delay between each toilet paper pickup")]
    [SerializeField] private float dispenseDelay = 0.5f;
    [Tooltip("Time to respawn toilet paper rolls after all are taken")]
    [SerializeField] private float respawnDelay = 2f;
    
    [Header("Spawn Points")]
    [Tooltip("Points where toilet paper rolls are displayed on the rack")]
    [SerializeField] private List<Transform> paperSpawnPoints = new List<Transform>();
    
    [Header("Animation")]
    [SerializeField] private float spawnPopScale = 1.2f;
    [SerializeField] private float spawnPopDuration = 0.3f;
    
    [Header("Proximity Detection")]
    [SerializeField] private float proximityRadius = 2f;
    
    private List<ToiletPaper> availablePapers = new List<ToiletPaper>();
    private PlayerCarryController playerCarryController;
    private Transform playerTransform;
    private bool isDispensing = false;
    private Coroutine dispenseCoroutine;
    private Coroutine respawnCoroutine;
    
    public int AvailableCount => availablePapers.Count;
    
    private void Start()
    {
        // Validate setup
        if (playerLayer == 0)
        {
            Debug.LogError($"[ToiletPaperRack] Player Layer is not set on {gameObject.name}!");
        }
        
        if (toiletPaperPrefab == null)
        {
            Debug.LogError($"[ToiletPaperRack] Toilet Paper Prefab is not assigned on {gameObject.name}!");
        }
        
        // Spawn initial toilet papers
        SpawnAllPapers();
        
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerCarryController = player.GetComponent<PlayerCarryController>();
            if (playerCarryController == null)
            {
                playerCarryController = player.GetComponentInChildren<PlayerCarryController>();
            }
            
            Debug.Log($"[ToiletPaperRack] Found player: {player.name}, has PlayerCarryController: {playerCarryController != null}");
        }
        else
        {
            Debug.LogWarning($"[ToiletPaperRack] No GameObject with 'Player' tag found!");
        }
        
        Debug.Log($"[ToiletPaperRack] Initialized with {availablePapers.Count} papers, dispenseDelay: {dispenseDelay}s");
    }
    
    private void Update()
    {
        // Proximity-based detection
        if (proximityRadius > 0 && playerTransform != null && playerCarryController != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            bool nowInRange = distance <= proximityRadius;
            
            // Player just entered range - start dispensing
            if (nowInRange && !isDispensing)
            {
                Debug.Log($"[ToiletPaperRack] Player in range, starting dispense. Distance: {distance:F2}, availablePapers: {availablePapers.Count}");
                StartDispensing();
            }
        }
    }
    
    private void SpawnAllPapers()
    {
        foreach (var spawnPoint in paperSpawnPoints)
        {
            if (spawnPoint != null)
            {
                SpawnPaperAt(spawnPoint, false);
            }
        }
    }
    
    private void SpawnPaperAt(Transform spawnPoint, bool withAnimation = true)
    {
        if (toiletPaperPrefab == null) return;
        
        GameObject paperObj = Instantiate(toiletPaperPrefab, spawnPoint.position, spawnPoint.rotation);
        ToiletPaper paper = paperObj.GetComponent<ToiletPaper>();
        
        if (paper != null)
        {
            paper.Initialize(this);
            availablePapers.Add(paper);
            
            if (withAnimation)
            {
                Vector3 originalScale = paperObj.transform.localScale;
                paperObj.transform.localScale = Vector3.zero;
                paperObj.transform.DOScale(originalScale * spawnPopScale, spawnPopDuration * 0.6f)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => paperObj.transform.DOScale(originalScale, spawnPopDuration * 0.4f));
            }
        }
    }
    
    private void StartDispensing()
    {
        Debug.Log($"[ToiletPaperRack] Starting dispense coroutine. Available papers: {availablePapers.Count}, CanCarryMore: {playerCarryController?.CanCarryMore}");
        isDispensing = true;
        dispenseCoroutine = StartCoroutine(DispenseCoroutine());
    }
    
    private void StopDispensing()
    {
        Debug.Log($"[ToiletPaperRack] Stopping dispense. Was dispensing: {isDispensing}");
        isDispensing = false;
        
        if (dispenseCoroutine != null)
        {
            StopCoroutine(dispenseCoroutine);
            dispenseCoroutine = null;
        }
    }
    
    private IEnumerator DispenseCoroutine()
    {
        Debug.Log($"[ToiletPaperRack] DispenseCoroutine started. CanCarryMore: {playerCarryController?.CanCarryMore}, CarriedCount: {playerCarryController?.CarriedCount}");
        
        // Wait initial delay before first pickup
        yield return new WaitForSeconds(dispenseDelay);
        
        // Check actual distance each iteration for reliability
        while (playerCarryController != null && playerTransform != null)
        {
            // Check actual distance each iteration
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            if (distance > proximityRadius)
            {
                Debug.Log($"[ToiletPaperRack] Player moved out of range (distance: {distance:F2}), stopping dispense");
                break;
            }
            
            // Check if player can carry more
            if (!playerCarryController.CanCarryMore)
            {
                Debug.Log($"[ToiletPaperRack] Player can't carry more (carrying {playerCarryController.CarriedCount}/{playerCarryController.MaxCarryCount}), waiting...");
                yield return new WaitForSeconds(0.2f);
                continue;
            }
            
            // Check if we have papers available - if not, respawn immediately
            if (availablePapers.Count == 0)
            {
                Debug.Log($"[ToiletPaperRack] No papers available, respawning immediately");
                RespawnAllPapers();
                yield return new WaitForSeconds(0.1f); // Small delay for animation
                continue;
            }
            
            // Dispense one paper
            ToiletPaper paper = availablePapers[0];
            availablePapers.RemoveAt(0);
            
            if (paper != null && playerCarryController.TryPickupToiletPaper(paper))
            {
                Debug.Log($"[ToiletPaperRack] Dispensed toilet paper. Remaining on rack: {availablePapers.Count}");
            }
            else
            {
                Debug.Log($"[ToiletPaperRack] Failed to pickup toilet paper (paper null: {paper == null})");
            }
            
            // Wait before next dispense
            yield return new WaitForSeconds(dispenseDelay);
        }
        
        Debug.Log($"[ToiletPaperRack] DispenseCoroutine ended");
        isDispensing = false;
        dispenseCoroutine = null;
    }
    
    /// <summary>
    /// Immediately respawn all papers at spawn points
    /// </summary>
    private void RespawnAllPapers()
    {
        int respawnedCount = 0;
        foreach (var spawnPoint in paperSpawnPoints)
        {
            if (spawnPoint != null)
            {
                bool hasActivePaper = false;
                foreach (var paper in availablePapers)
                {
                    if (paper != null && Vector3.Distance(paper.transform.position, spawnPoint.position) < 0.1f)
                    {
                        hasActivePaper = true;
                        break;
                    }
                }
                
                if (!hasActivePaper)
                {
                    SpawnPaperAt(spawnPoint, true);
                    respawnedCount++;
                }
            }
        }
        
        Debug.Log($"[ToiletPaperRack] Respawned {respawnedCount} papers. Now available: {availablePapers.Count}");
    }
    
    private IEnumerator RespawnCoroutine()
    {
        Debug.Log($"[ToiletPaperRack] RespawnCoroutine started, waiting {respawnDelay}s");
        
        yield return new WaitForSeconds(respawnDelay);
        
        RespawnAllPapers();
        respawnCoroutine = null;
    }
    
    /// <summary>
    /// Called when a paper is removed from the rack
    /// </summary>
    public void OnPaperTaken(ToiletPaper paper)
    {
        availablePapers.Remove(paper);
    }
    
    /// <summary>
    /// Called by janitor to take one toilet paper from the rack.
    /// Returns the toilet paper or null if none available.
    /// </summary>
    public ToiletPaper TakeToiletPaperForJanitor()
    {
        if (availablePapers.Count == 0)
        {
            Debug.Log("[ToiletPaperRack] No toilet paper available for janitor");
            return null;
        }
        
        ToiletPaper paper = availablePapers[0];
        availablePapers.RemoveAt(0);
        
        Debug.Log($"[ToiletPaperRack] Janitor took toilet paper. Remaining: {availablePapers.Count}");
        
        // Start respawn if rack is empty
        if (availablePapers.Count == 0 && respawnCoroutine == null)
        {
            respawnCoroutine = StartCoroutine(RespawnCoroutine());
        }
        
        return paper;
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw proximity radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, proximityRadius);
        
        // Draw spawn points
        Gizmos.color = Color.cyan;
        foreach (var point in paperSpawnPoints)
        {
            if (point != null)
            {
                Gizmos.DrawWireSphere(point.position, 0.1f);
            }
        }
    }
}
