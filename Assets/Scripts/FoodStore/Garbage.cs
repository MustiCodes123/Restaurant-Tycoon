using UnityEngine;
using DG.Tweening;

/// <summary>
/// Garbage left on table after customer finishes eating.
/// Player picks it up automatically on trigger enter.
/// </summary>
public class Garbage : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask playerLayer;
    
    [Header("Spawn Animation")]
    [SerializeField] private float spawnPopScale = 1.2f;
    [SerializeField] private float spawnPopDuration = 0.3f;
    [SerializeField] private Ease spawnPopEase = Ease.OutBack;
    
    private DiningTable sourceTable;
    private bool isPickedUp = false;
    
    public bool IsPickedUp => isPickedUp;
    public DiningTable SourceTable => sourceTable;
    
    private void Awake()
    {
        // Ensure we have a collider for trigger detection
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            // Add a sphere collider if none exists
            SphereCollider sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 0.5f;
            Debug.LogWarning("[Garbage] Added missing SphereCollider with trigger");
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning("[Garbage] Collider was not set to trigger! Setting it now.");
            col.isTrigger = true;
        }
    }
    
    public void Initialize(DiningTable table)
    {
        sourceTable = table;
        PlaySpawnAnimation();
    }
    
    private void PlaySpawnAnimation()
    {
        Vector3 originalScale = transform.localScale;
        transform.localScale = Vector3.zero;
        
        transform.DOScale(originalScale * spawnPopScale, spawnPopDuration * 0.6f)
            .SetEase(spawnPopEase)
            .OnComplete(() => {
                transform.DOScale(originalScale, spawnPopDuration * 0.4f);
            });
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (isPickedUp) return;
        
        Debug.Log($"[Garbage] OnTriggerEnter: {other.gameObject.name}, Layer: {other.gameObject.layer}, PlayerLayer: {playerLayer.value}");
        
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            Debug.Log("[Garbage] Player detected, trying pickup...");
            TryPickup(other.gameObject);
        }
    }
    
    private void TryPickup(GameObject player)
    {
        // Get player carry controller - try multiple methods
        PlayerCarryController carryController = player.GetComponent<PlayerCarryController>();
        
        if (carryController == null)
        {
            carryController = player.GetComponentInParent<PlayerCarryController>();
        }
        
        if (carryController == null)
        {
            // Try finding in children (in case trigger is on a child)
            carryController = player.GetComponentInChildren<PlayerCarryController>();
        }
        
        if (carryController == null)
        {
            // Last resort - find any PlayerCarryController in the scene
            carryController = FindObjectOfType<PlayerCarryController>();
            Debug.LogWarning($"[Garbage] Had to use FindObjectOfType to find PlayerCarryController: {carryController != null}");
        }
        
        if (carryController == null)
        {
            Debug.LogError("[Garbage] Could not find PlayerCarryController anywhere! Make sure it's attached to the Player.");
            return;
        }
        
        // Try to pick up
        if (carryController.TryPickupGarbage(this))
        {
            MarkPickedUp();
            Debug.Log("[Garbage] Picked up by player");
        }
        else
        {
            Debug.Log("[Garbage] Player cannot carry more garbage");
        }
    }
    
    /// <summary>
    /// Called when garbage is disposed in bin
    /// </summary>
    public void Dispose()
    {
        // Shrink and destroy
        transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() => {
                Destroy(gameObject);
            });
    }

    public void MarkPickedUp()
    {
        if (isPickedUp) return;

        isPickedUp = true;

        if (sourceTable != null)
        {
            sourceTable.OnGarbagePickedUp();
        }
    }

    private void OnDestroy()
    {
        if (!isPickedUp && sourceTable != null)
        {
            sourceTable.OnGarbagePickedUp();
        }
    }
}
