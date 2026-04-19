using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Food tray that holds prepared items.
/// Instantiated when customer arrives at pickup point.
/// Items are SetActive(true) as they're prepared.
/// </summary>
public class FoodTray : MonoBehaviour
{
    [Header("Item Slots")]
    [SerializeField] private List<GameObject> itemSlots = new List<GameObject>();
    
    [Header("Animation")]
    [SerializeField] private float itemPopScale = 1.3f;
    [SerializeField] private float itemPopDuration = 0.3f;
    [SerializeField] private Ease itemPopEase = Ease.OutBack;
    
    [Header("Movement")]
    [SerializeField] private float moveToHandDuration = 0.3f;
    [SerializeField] private Ease moveToHandEase = Ease.InOutQuad;    
    private int totalItemCount;
    private int activatedItemCount = 0;
    private bool isBeingCarried = false;
    
    public bool IsComplete => activatedItemCount >= totalItemCount;
    public bool IsBeingCarried => isBeingCarried;
    
    private void Awake()
    {
        // Hide all items initially
        foreach (var item in itemSlots)
        {
            if (item != null)
            {
                item.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Initialize with expected item count
    /// </summary>
    public void Initialize(int itemCount)
    {
        totalItemCount = itemCount;
        activatedItemCount = 0;
        
        // Ensure we have enough slots
        if (itemSlots.Count < itemCount)
        {
            Debug.LogWarning($"[FoodTray] Not enough item slots! Have {itemSlots.Count}, need {itemCount}");
        }
    }
    
    /// <summary>
    /// Activate an item slot when it's prepared
    /// </summary>
    public void ActivateItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= itemSlots.Count)
        {
            Debug.LogWarning($"[FoodTray] Invalid item index: {itemIndex}");
            return;
        }
        
        GameObject item = itemSlots[itemIndex];
        if (item == null) return;
        
        if (!item.activeSelf)
        {
            item.SetActive(true);
            activatedItemCount++;
            
            // Pop animation
            Vector3 originalScale = item.transform.localScale;
            item.transform.localScale = Vector3.zero;
            item.transform.DOScale(originalScale * itemPopScale, itemPopDuration * 0.5f)
                .SetEase(itemPopEase)
                .OnComplete(() => {
                    item.transform.DOScale(originalScale, itemPopDuration * 0.5f);
                });
            
            Debug.Log($"[FoodTray] Item {itemIndex} activated. {activatedItemCount}/{totalItemCount}");
        }
    }
    
    /// <summary>
    /// Move tray to customer's hand
    /// </summary>
    public void MoveToHand(Transform handPoint, System.Action onComplete = null)
    {
        Debug.Log($"[FoodTray] MoveToHand called. HandPoint: {handPoint?.name ?? "NULL"}, Tray position: {transform.position}");
        
        if (handPoint == null)
        {
            Debug.LogError("[FoodTray] MoveToHand called with null handPoint!");
            onComplete?.Invoke();
            return;
        }
        
        isBeingCarried = true;
        
        // Store original world rotation before parenting
        Quaternion worldRotation = transform.rotation;
        
        // Parent to hand
        transform.SetParent(handPoint);
        
        // Force local transform to identity (like player garbage pickup)
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;        
        Debug.Log($"[FoodTray] Tray parented to {handPoint.name}. HandPoint world euler: {handPoint.eulerAngles}, Tray local euler: {transform.localEulerAngles}");
        
        // Animate with a small bounce effect
        Sequence moveSequence = DOTween.Sequence();
        
        // Small pop scale animation for feedback
        moveSequence.Append(transform.DOPunchScale(Vector3.one * 0.1f, moveToHandDuration, 1, 0.5f));
        
        moveSequence.OnComplete(() => {
            // Ensure final position and rotation are exactly zero
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            Debug.Log("[FoodTray] MoveToHand animation complete!");
            onComplete?.Invoke();
        });
    }
    
    /// <summary>
    /// Place tray on table
    /// </summary>
    public void PlaceOnTable(Transform tablePoint)
    {
        isBeingCarried = false;
        
        // Unparent and move to table
        transform.SetParent(null);
        transform.position = tablePoint.position;
        transform.rotation = tablePoint.rotation;
    }
    
    /// <summary>
    /// Destroy the tray (when customer finishes eating)
    /// </summary>
    public void DestroyTray()
    {
        transform.DOScale(Vector3.zero, 0.3f)
            .SetEase(Ease.InBack)
            .OnComplete(() => {
                Destroy(gameObject);
            });
    }
}
