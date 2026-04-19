using UnityEngine;
using DG.Tweening;

/// <summary>
/// Garbage disposal bin.
/// Player disposes garbage and toilet paper automatically on trigger enter.
/// Uses unified PlayerCarryController for both garbage and toilet paper.
/// </summary>
public class GarbageBin : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask playerLayer;
    
    [Header("UI Settings")]
    [SerializeField] private GameObject binUI;
    [SerializeField] private PlayerCarryController playerCarryController;
    
    [Header("Animation")]
    [SerializeField] private Transform binLid; // Optional lid that opens
    [SerializeField] private float lidOpenAngle = 45f;
    [SerializeField] private float lidOpenDuration = 0.2f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem disposeParticles;
    
    private bool isProcessing = false;
    
    private void Start()
    {
        // Auto-find player carry controller if not assigned
        if (playerCarryController == null)
        {
            playerCarryController = FindObjectOfType<PlayerCarryController>();
        }
        
        // Initial UI state - hidden
        if (binUI != null)
        {
            binUI.SetActive(false);
        }
    }
    
    private void Update()
    {
        UpdateUIVisibility();
    }
    
    private void UpdateUIVisibility()
    {
        if (binUI == null) return;
        
        // Show UI only if player is carrying garbage or toilet paper
        bool hasItems = playerCarryController != null && playerCarryController.IsCarrying;
        binUI.SetActive(hasItems);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (isProcessing) return;
        
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            TryDispose(other.gameObject);
        }
    }
    
    private void TryDispose(GameObject player)
    {
        // Get carry controller
        PlayerCarryController carryController = player.GetComponent<PlayerCarryController>();
        if (carryController == null)
        {
            carryController = player.GetComponentInParent<PlayerCarryController>();
        }
        
        // Check if player has anything to dispose
        if (carryController == null || !carryController.IsCarrying) return;
        
        isProcessing = true;
        
        // Open lid if exists
        if (binLid != null)
        {
            binLid.DOLocalRotate(new Vector3(-lidOpenAngle, 0, 0), lidOpenDuration)
                .OnComplete(() => {
                    // Dispose everything
                    int totalDisposed = DisposeAll(carryController);
                    OnDisposeComplete(totalDisposed);
                    
                    // Close lid after delay
                    DOVirtual.DelayedCall(0.3f, () => {
                        binLid.DOLocalRotate(Vector3.zero, lidOpenDuration);
                        isProcessing = false;
                    });
                });
        }
        else
        {
            int totalDisposed = DisposeAll(carryController);
            OnDisposeComplete(totalDisposed);
            isProcessing = false;
        }
    }
    
    private int DisposeAll(PlayerCarryController carryController)
    {
        // Use the unified dispose method that handles both garbage and toilet paper
        return carryController.DisposeAll();
    }
    
    private void OnDisposeComplete(int totalDisposed)
    {
        if (totalDisposed > 0)
        {
            // Play particles
            if (disposeParticles != null)
            {
                disposeParticles.Play();
            }
            
            // Play garbage drop sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.GarbageDrop);
            }
            
            Debug.Log($"[GarbageBin] Disposed {totalDisposed} items");
        }
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(transform.position, new Vector3(0.5f, 0.8f, 0.5f));
    }
}
