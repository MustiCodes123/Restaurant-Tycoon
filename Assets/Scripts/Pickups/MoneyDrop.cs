using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class MoneyDrop : MonoBehaviour
{
    private static readonly List<MoneyDrop> ActiveDrops = new List<MoneyDrop>();

    [Header("Money Settings")]
    [SerializeField] private int moneyAmount = 10;
    [SerializeField] private float pickupRadius = 1.5f;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("Stack Settings")]
    [SerializeField] private float stackSearchRadius = 1.75f;
    [SerializeField] private int stackColumns = 3;
    [SerializeField] private float stackSpacingX = 0.38f;
    [SerializeField] private float stackSpacingZ = 0.28f;
    [SerializeField] private float stackLayerHeight = 0.018f;
    [SerializeField] private float stackSettleDuration = 0.24f;
    [SerializeField] private float stackTiltDegrees = 5f;

    [Header("Animation Settings")]
    [SerializeField] private float bounceHeight = 0.5f;
    [SerializeField] private float bounceDuration = 0.3f;
    [SerializeField] private float collectDuration = 0.5f;
    [SerializeField] private float arcHeight = 1.5f;
    [SerializeField] private float collectStaggerPerSlot = 0.035f;
    [SerializeField] private float collectPunchScale = 1.14f;
    
    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    private bool isCollected = false;
    private Vector3 stackAnchor;
    private Vector3 stackPosition;
    private int stackSlotIndex;
    private Collider[] dropColliders;
    
    public int MoneyAmount => moneyAmount;

    private void Awake()
    {
        dropColliders = GetComponentsInChildren<Collider>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }
    
    private void Start()
    {
        RegisterStackSlot();
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
        transform.DOKill();

        Vector3 spawnPosition = transform.position;
        Vector3 liftPosition = spawnPosition + Vector3.up * bounceHeight;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(transform.DOMove(liftPosition, bounceDuration).SetEase(Ease.OutQuad));
        sequence.Append(transform.DOMove(stackPosition, stackSettleDuration).SetEase(Ease.OutBack));
        sequence.Join(transform.DORotateQuaternion(GetStackRotation(), stackSettleDuration).SetEase(Ease.OutQuad));
        sequence.Join(transform.DOScale(transform.localScale * collectPunchScale, stackSettleDuration * 0.5f).SetLoops(2, LoopType.Yoyo));
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

        EnableDropColliders(false);
        
        // Play money flow sound when money is collected
        if (AudioManager.Instance != null)
        {
            float pitch = Mathf.Min(1f + stackSlotIndex * 0.04f, 1.35f);
            AudioManager.Instance.PlaySFXWithPitch(SoundEffect.MoneyFlow, pitch);
        }
        
        // Animate this money prefab to player with a curve
        AnimateToPlayer(player);
    }
    
    private void AnimateToPlayer(Transform player)
    {
        transform.DOKill();

        float delay = stackSlotIndex * collectStaggerPerSlot;
        Vector3 startPos = transform.position;
        Vector3 targetOffset = Vector3.up * 0.5f;
        
        // Calculate bezier control point for arc
        Vector3 midPoint = (startPos + player.position) / 2f;
        Vector3 controlPoint = midPoint + Vector3.up * arcHeight;
        
        float elapsed = 0f;
        
        // Store initial scale
        Vector3 initialScale = transform.localScale;
        
        DOTween.To(() => elapsed, x => elapsed = x, 1f, collectDuration)
            .SetDelay(delay)
            .SetEase(Ease.InCubic)
            .OnUpdate(() =>
            {
                if (this == null || player == null) return;
                
                Vector3 currentTarget = player.position + targetOffset;
                Vector3 pos = CalculateBezierPoint(elapsed, startPos, controlPoint, currentTarget);
                transform.position = pos;
                
                // Scale down from initial scale as it approaches
                float scale = Mathf.Lerp(1f, 0.2f, elapsed);
                transform.localScale = initialScale * scale;
                transform.Rotate(Vector3.up, 540f * Time.deltaTime, Space.World);

                if (spriteRenderer != null)
                {
                    Color color = spriteRenderer.color;
                    color.a = Mathf.Lerp(1f, 0.35f, elapsed);
                    spriteRenderer.color = color;
                }
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

    private void RegisterStackSlot()
    {
        stackAnchor = transform.position;
        stackSlotIndex = 0;

        for (int i = ActiveDrops.Count - 1; i >= 0; i--)
        {
            MoneyDrop drop = ActiveDrops[i];
            if (drop == null)
            {
                ActiveDrops.RemoveAt(i);
                continue;
            }

            if (drop.isCollected)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, drop.stackAnchor);
            if (distance <= stackSearchRadius)
            {
                stackAnchor = drop.stackAnchor;
                stackSlotIndex++;
            }
        }

        ActiveDrops.Add(this);
        stackPosition = stackAnchor + GetStackOffset(stackSlotIndex);

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder += stackSlotIndex;
        }
    }

    private Vector3 GetStackOffset(int index)
    {
        int columns = Mathf.Max(1, stackColumns);
        int column = index % columns;
        int row = index / columns;

        float x = (column - (columns - 1) * 0.5f) * stackSpacingX;
        float z = row * stackSpacingZ;

        if (row % 2 == 1)
        {
            x += stackSpacingX * 0.35f;
        }

        float y = row * stackLayerHeight;
        return new Vector3(x, y, z);
    }

    private Quaternion GetStackRotation()
    {
        float yaw = ((stackSlotIndex % Mathf.Max(1, stackColumns)) - 1) * stackTiltDegrees;
        return Quaternion.Euler(0f, yaw, 0f);
    }

    private void EnableDropColliders(bool enabled)
    {
        if (dropColliders == null)
        {
            return;
        }

        for (int i = 0; i < dropColliders.Length; i++)
        {
            if (dropColliders[i] != null)
            {
                dropColliders[i].enabled = enabled;
            }
        }
    }
    
    private void AddMoneyAndDestroy()
    {
        // Register with RT level manager first (restaurant tycoon scenes).
        // RTLevelManager adds wallet money and mission progress, so avoid adding
        // to CurrencyManager here as well.
        if (RestaurantTycoon.RTLevelManager.Instance != null)
        {
            RestaurantTycoon.RTLevelManager.Instance.RegisterMoneyEarned(moneyAmount);
        }
        // Fallback to generic LevelManager (other scenes)
        else
        {
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.AddMoney(moneyAmount);
            }

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.RegisterMoneyEarned(moneyAmount);
            }
        }
        
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        ActiveDrops.Remove(this);
        transform.DOKill();
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(Application.isPlaying ? stackAnchor : transform.position, stackSearchRadius);
    }
}
