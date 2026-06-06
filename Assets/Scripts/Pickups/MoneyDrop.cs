using UnityEngine;
using DG.Tweening;

public class MoneyDrop : MonoBehaviour
{
    [Header("Money Settings")]
    [SerializeField] private int moneyAmount = 10;
    [SerializeField] private float pickupRadius = 1.5f;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("Animation Settings")]
    [SerializeField] private float bounceHeight = 0.5f;
    [SerializeField] private float bounceDuration = 0.3f;
    [SerializeField] private float collectDuration = 0.5f;
    [SerializeField] private float arcHeight = 1.5f;
    
    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    private bool isCollected = false;
    private Vector3 startPosition;
    
    public int MoneyAmount => moneyAmount;
    
    private void Start()
    {
        startPosition = transform.position;
        PlaySpawnAnimation();
        
        // Play customer served sound when money drop is instantiated
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.CustomerServed);
        }
    }
    
    public void Initialize(int amount)
    {
        moneyAmount = amount;
    }
    
    private void PlaySpawnAnimation()
    {
        // Bounce animation when money drops
        transform.DOMoveY(startPosition.y + bounceHeight, bounceDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => {
                transform.DOMoveY(startPosition.y, bounceDuration)
                    .SetEase(Ease.InQuad);
            });
    }
    
    private void Update()
    {
        if (isCollected)
            return;
        
        // Check for player in range
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, pickupRadius, playerLayer);
        
        if (hitColliders.Length > 0)
        {
            CollectMoney(hitColliders[0].transform);
        }
    }
    
    private void CollectMoney(Transform player)
    {
        if (isCollected)
            return;
        
        isCollected = true;
        
        // Play money flow sound when money is collected
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.MoneyFlow);
        }
        
        // Animate this money prefab to player with a curve
        AnimateToPlayer(player);
    }
    
    private void AnimateToPlayer(Transform player)
    {
        Vector3 startPos = transform.position;
        Vector3 targetOffset = Vector3.up * 0.5f;
        
        // Calculate bezier control point for arc
        Vector3 midPoint = (startPos + player.position) / 2f;
        Vector3 controlPoint = midPoint + Vector3.up * arcHeight;
        
        float elapsed = 0f;
        
        // Store initial scale
        Vector3 initialScale = transform.localScale;
        
        DOTween.To(() => elapsed, x => elapsed = x, 1f, collectDuration)
            .SetEase(Ease.InQuad)
            .OnUpdate(() =>
            {
                if (this == null || player == null) return;
                
                Vector3 currentTarget = player.position + targetOffset;
                Vector3 pos = CalculateBezierPoint(elapsed, startPos, controlPoint, currentTarget);
                transform.position = pos;
                
                // Scale down from initial scale as it approaches
                transform.localScale = initialScale * Mathf.Lerp(1f, 0.3f, elapsed);
            })
            .OnComplete(() =>
            {
                if (this != null)
                {
                    AddMoneyAndDestroy();
                }
            });
    }
    
    private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        
        Vector3 point = uu * p0;
        point += 2f * u * t * p1;
        point += tt * p2;
        
        return point;
    }
    
    private void AddMoneyAndDestroy()
    {
        // Add money to currency manager
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddMoney(moneyAmount);
        }
        
        // Register with RT level manager first (restaurant tycoon scenes)
        if (RestaurantTycoon.RTLevelManager.Instance != null)
        {
            RestaurantTycoon.RTLevelManager.Instance.RegisterMoneyEarned(moneyAmount);
        }
        // Fallback to generic LevelManager (other scenes)
        else if (LevelManager.Instance != null)
        {
            LevelManager.Instance.RegisterMoneyEarned(moneyAmount);
        }
        
        Destroy(gameObject);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
