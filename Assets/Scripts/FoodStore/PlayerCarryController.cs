using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Handles player carrying items (garbage trays and toilet paper).
/// Max items stacked at carry points.
/// Controls lift idle and lift walk animations.
/// </summary>
public class PlayerCarryController : MonoBehaviour
{
    [Header("Carry Points")]
    [Tooltip("Empty GameObjects where carried items will be placed. First is lowest, last is highest.")]
    [SerializeField] private List<Transform> carryPoints = new List<Transform>();
    
    [Header("Settings")]
    [SerializeField] private int maxCarryCount = 3;
    
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string liftIdleParam = "IsLiftIdle";
    [SerializeField] private string liftWalkParam = "IsLiftWalking";
    
    [Header("Pickup Animation")]
    [SerializeField] private float pickupJumpHeight = 0.3f;
    [SerializeField] private float pickupDuration = 0.2f;
    
    // Carried items - can be Garbage or ToiletPaper
    private List<GameObject> carriedItems = new List<GameObject>();
    private List<Garbage> carriedGarbage = new List<Garbage>();
    private List<ToiletPaper> carriedToiletPaper = new List<ToiletPaper>();
    private bool isMoving = false;
    
    public int CarriedCount => carriedItems.Count;
    public bool IsCarrying => carriedItems.Count > 0;
    public bool IsCarryingGarbage => carriedGarbage.Count > 0;
    public bool IsCarryingToiletPaper => carriedToiletPaper.Count > 0;
    public bool CanCarryMore => carriedItems.Count < maxCarryCount && carriedItems.Count < carryPoints.Count;
    public int MaxCarryCount => maxCarryCount;
    public int ToiletPaperCount => carriedToiletPaper.Count;
    public int GarbageCount => carriedGarbage.Count;
    
    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }
    
    private void Update()
    {
        // Update animation based on carrying state
        UpdateAnimation();
    }
    
    /// <summary>
    /// Set movement state (called by PlayerController)
    /// </summary>
    public void SetMoving(bool moving)
    {
        isMoving = moving;
    }
    
    private void UpdateAnimation()
    {
        if (animator == null) return;
        
        bool isCarrying = IsCarrying;
        
        if (isCarrying)
        {
            if (isMoving)
            {
                // Lift walking
                animator.SetBool(liftIdleParam, false);
                animator.SetBool(liftWalkParam, true);
            }
            else
            {
                // Lift idle
                animator.SetBool(liftIdleParam, true);
                animator.SetBool(liftWalkParam, false);
            }
        }
        else
        {
            // Not carrying - reset lift animations
            animator.SetBool(liftIdleParam, false);
            animator.SetBool(liftWalkParam, false);
        }
    }
    
    /// <summary>
    /// Try to pick up a garbage item
    /// </summary>
    public bool TryPickupGarbage(Garbage garbage)
    {
        if (!CanCarryMore)
        {
            Debug.Log("[PlayerCarryController] Cannot carry more - at capacity");
            return false;
        }
        
        // Get the next available carry point
        int slotIndex = carriedItems.Count;
        if (slotIndex >= carryPoints.Count)
        {
            Debug.LogWarning("[PlayerCarryController] Not enough carry points defined");
            return false;
        }
        
        Transform carryPoint = carryPoints[slotIndex];
        
        // Add to lists
        carriedItems.Add(garbage.gameObject);
        carriedGarbage.Add(garbage);
        
        // Parent and animate to carry point
        garbage.transform.SetParent(carryPoint);
        
        // Jump animation to carry point
        Sequence pickupSequence = DOTween.Sequence();
        pickupSequence.Append(garbage.transform.DOLocalJump(Vector3.zero, pickupJumpHeight, 1, pickupDuration));
        pickupSequence.Join(garbage.transform.DOLocalRotate(Vector3.zero, pickupDuration));
        
        // Play pickup sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.ItemPickup);
        }
        
        Debug.Log($"[PlayerCarryController] Picked up garbage. Now carrying {carriedItems.Count} items ({carriedGarbage.Count} garbage, {carriedToiletPaper.Count} toilet paper)");
        
        return true;
    }
    
    /// <summary>
    /// Try to pick up a toilet paper roll
    /// </summary>
    public bool TryPickupToiletPaper(ToiletPaper paper)
    {
        if (!CanCarryMore)
        {
            Debug.Log("[PlayerCarryController] Cannot carry more - at capacity");
            return false;
        }
        
        if (paper == null || paper.IsPickedUp)
        {
            return false;
        }
        
        // Get the next available carry point
        int slotIndex = carriedItems.Count;
        if (slotIndex >= carryPoints.Count)
        {
            Debug.LogWarning("[PlayerCarryController] Not enough carry points defined");
            return false;
        }
        
        Transform carryPoint = carryPoints[slotIndex];
        
        // Add to lists
        carriedItems.Add(paper.gameObject);
        carriedToiletPaper.Add(paper);
        
        // Use toilet paper's pickup method (handles parenting and animation)
        paper.PickUp(carryPoint);
        
        Debug.Log($"[PlayerCarryController] Picked up toilet paper. Now carrying {carriedItems.Count} items ({carriedGarbage.Count} garbage, {carriedToiletPaper.Count} toilet paper)");
        
        return true;
    }
    
    /// <summary>
    /// Check if player has toilet paper to deliver
    /// </summary>
    public bool HasToiletPaper()
    {
        return carriedToiletPaper.Count > 0;
    }
    
    /// <summary>
    /// Get and remove the top toilet paper for delivery
    /// </summary>
    public ToiletPaper TakeTopToiletPaper()
    {
        if (carriedToiletPaper.Count == 0) return null;
        
        int lastIndex = carriedToiletPaper.Count - 1;
        ToiletPaper paper = carriedToiletPaper[lastIndex];
        carriedToiletPaper.RemoveAt(lastIndex);
        carriedItems.Remove(paper.gameObject);
        
        Debug.Log($"[PlayerCarryController] Took top toilet paper. Remaining: {carriedToiletPaper.Count}");
        
        return paper;
    }
    
    /// <summary>
    /// Dispose all carried garbage
    /// </summary>
    public int DisposeAllGarbage()
    {
        int count = carriedGarbage.Count;
        
        // Dispose each garbage
        foreach (var garbage in carriedGarbage)
        {
            if (garbage != null)
            {
                carriedItems.Remove(garbage.gameObject);
                garbage.Dispose();
            }
        }
        
        carriedGarbage.Clear();
        
        Debug.Log($"[PlayerCarryController] Disposed {count} garbage items. Total items: {carriedItems.Count}");
        
        return count;
    }
    
    /// <summary>
    /// Dispose all carried toilet paper
    /// </summary>
    public int DisposeAllToiletPaper()
    {
        int count = carriedToiletPaper.Count;
        
        foreach (var paper in carriedToiletPaper)
        {
            if (paper != null)
            {
                carriedItems.Remove(paper.gameObject);
                paper.Dispose();
            }
        }
        
        carriedToiletPaper.Clear();
        
        Debug.Log($"[PlayerCarryController] Disposed {count} toilet paper. Total items: {carriedItems.Count}");
        
        return count;
    }
    
    /// <summary>
    /// Dispose all carried items (garbage + toilet paper)
    /// </summary>
    public int DisposeAll()
    {
        int total = DisposeAllGarbage() + DisposeAllToiletPaper();
        return total;
    }
    
    /// <summary>
    /// Get the topmost garbage (for visual purposes)
    /// </summary>
    public Garbage GetTopGarbage()
    {
        if (carriedGarbage.Count == 0) return null;
        return carriedGarbage[carriedGarbage.Count - 1];
    }
    
    /// <summary>
    /// Get the topmost toilet paper (for visual purposes)
    /// </summary>
    public ToiletPaper GetTopToiletPaper()
    {
        if (carriedToiletPaper.Count == 0) return null;
        return carriedToiletPaper[carriedToiletPaper.Count - 1];
    }
}
