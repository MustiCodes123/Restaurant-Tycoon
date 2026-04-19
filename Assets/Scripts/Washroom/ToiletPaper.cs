using UnityEngine;
using DG.Tweening;

/// <summary>
/// Individual toilet paper roll that can be picked up by player and delivered to stalls.
/// </summary>
public class ToiletPaper : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private float pickupJumpHeight = 0.3f;
    [SerializeField] private float pickupDuration = 0.2f;
    [SerializeField] private float throwArcHeight = 1.5f;
    [SerializeField] private float throwDuration = 0.4f;
    
    private bool isPickedUp = false;
    private ToiletPaperRack sourceRack;
    
    public bool IsPickedUp => isPickedUp;
    public ToiletPaperRack SourceRack => sourceRack;
    
    public void Initialize(ToiletPaperRack rack)
    {
        sourceRack = rack;
    }
    
    /// <summary>
    /// Pick up and parent to carry point with jump animation
    /// </summary>
    public void PickUp(Transform carryPoint)
    {
        if (isPickedUp) return;
        
        isPickedUp = true;
        transform.SetParent(carryPoint);
        
        // Jump animation to carry point
        Sequence pickupSequence = DOTween.Sequence();
        pickupSequence.Append(transform.DOLocalJump(Vector3.zero, pickupJumpHeight, 1, pickupDuration));
        pickupSequence.Join(transform.DOLocalRotate(Vector3.zero, pickupDuration));
        
        // Play pickup sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.ItemPickup);
        }
    }
    
    /// <summary>
    /// Throw to a target position with arc animation
    /// </summary>
    public void ThrowTo(Vector3 targetPosition, System.Action onComplete = null)
    {
        transform.SetParent(null);
        
        // Arc throw animation
        Sequence throwSequence = DOTween.Sequence();
        
        // Calculate mid-point for arc
        Vector3 startPos = transform.position;
        Vector3 midPoint = (startPos + targetPosition) / 2f + Vector3.up * throwArcHeight;
        
        // Create path for arc
        Vector3[] path = new Vector3[] { midPoint, targetPosition };
        
        throwSequence.Append(transform.DOPath(path, throwDuration, PathType.CatmullRom)
            .SetEase(Ease.OutQuad));
        throwSequence.Join(transform.DORotate(new Vector3(360, 0, 0), throwDuration, RotateMode.FastBeyond360));
        
        throwSequence.OnComplete(() =>
        {
            // Play drop sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.ItemPickup);
            }
            
            onComplete?.Invoke();
            
            // Destroy after landing
            Destroy(gameObject, 0.1f);
        });
    }
    
    /// <summary>
    /// Dispose the toilet paper (destroy with shrink animation)
    /// </summary>
    public void Dispose()
    {
        transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() => Destroy(gameObject));
    }
}
