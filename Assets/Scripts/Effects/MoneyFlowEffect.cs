using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Handles animating money prefabs along curved paths between positions.
/// Used for upgrade spot payments (player -> spot) and money collection (world -> player).
/// </summary>
public class MoneyFlowEffect : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject moneyPrefab;
    
    [Header("Spawn Settings")]
    [SerializeField] private int minIconsPerSpawn = 2;
    [SerializeField] private int maxIconsPerSpawn = 5;
    [SerializeField] private float spawnDelay = 0.05f;
    [SerializeField] private float spreadRadius = 0.3f;
    
    [Header("Animation Settings")]
    [SerializeField] private float flyDuration = 0.6f;
    [SerializeField] private float arcHeight = 1.5f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private AnimationCurve flyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Sound Settings")]
    [SerializeField] private float basePitch = 1.0f;
    [SerializeField] private float pitchIncrement = 0.1f;
    [SerializeField] private float maxPitch = 1.5f;
    
    private Vector3 originalPrefabScale = Vector3.one;
    
    [Header("Pooling")]
    [SerializeField] private bool usePooling = true;
    [SerializeField] private int poolSize = 20;
    [SerializeField] private float maxLifetime = 5f; // Safety timeout
    
    private Queue<GameObject> iconPool = new Queue<GameObject>();
    private List<GameObject> activeIcons = new List<GameObject>();
    
    private void Awake()
    {
        // Store original prefab scale before pooling
        if (moneyPrefab != null)
        {
            originalPrefabScale = moneyPrefab.transform.localScale;
        }
        
        if (usePooling && moneyPrefab != null)
        {
            InitializePool();
        }
    }
    
    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject icon = Instantiate(moneyPrefab, transform);
            
            // Remove MoneyDrop script to prevent it from interfering with upgrade logic
            MoneyDrop moneyDropScript = icon.GetComponent<MoneyDrop>();
            if (moneyDropScript != null)
            {
                Destroy(moneyDropScript);
            }
            
            icon.SetActive(false);
            iconPool.Enqueue(icon);
        }
    }
    
    private GameObject GetPooledIcon()
    {
        GameObject icon = null;
        
        if (iconPool.Count > 0)
        {
            icon = iconPool.Dequeue();
            icon.SetActive(true);
        }
        else
        {
            // Create new one if pool is empty
            icon = Instantiate(moneyPrefab, transform);
            
            // Remove MoneyDrop script to prevent it from interfering with upgrade logic
            MoneyDrop moneyDropScript = icon.GetComponent<MoneyDrop>();
            if (moneyDropScript != null)
            {
                Destroy(moneyDropScript);
            }
        }
        
        // Track active icons
        if (icon != null && !activeIcons.Contains(icon))
        {
            activeIcons.Add(icon);
        }
        
        return icon;
    }
    
    private void ReturnToPool(GameObject icon)
    {
        if (icon == null) return;
        
        // Remove from active list
        activeIcons.Remove(icon);
        
        // Kill any DOTween animations on this object
        icon.transform.DOKill();
        
        if (usePooling)
        {
            icon.SetActive(false);
            icon.transform.localScale = originalPrefabScale;
            icon.transform.localRotation = Quaternion.identity;
            icon.transform.SetParent(transform);
            
            // Only enqueue if not already in pool
            if (!iconPool.Contains(icon))
            {
                iconPool.Enqueue(icon);
            }
        }
        else
        {
            Destroy(icon);
        }
    }
    
    /// <summary>
    /// Force cleanup of stagnant or orphaned money icons
    /// </summary>
    public void ForceCleanup()
    {
        // Clean up all active icons
        for (int i = activeIcons.Count - 1; i >= 0; i--)
        {
            if (activeIcons[i] != null)
            {
                ReturnToPool(activeIcons[i]);
            }
            else
            {
                activeIcons.RemoveAt(i);
            }
        }
    }
    
    /// <summary>
    /// Cleanup all pooled objects when this component is destroyed
    /// </summary>
    private void OnDestroy()
    {
        // Stop all coroutines
        StopAllCoroutines();
        
        // Clean up active icons
        ForceCleanup();
        
        // Destroy all pooled objects
        while (iconPool.Count > 0)
        {
            GameObject icon = iconPool.Dequeue();
            if (icon != null)
            {
                icon.transform.DOKill();
                Destroy(icon);
            }
        }
    }
    
    /// <summary>
    /// Spawns money icons that fly from source to target position.
    /// Used for upgrade payments (player -> upgrade spot).
    /// </summary>
    public void SpawnMoneyToTarget(Vector3 sourcePosition, Vector3 targetPosition, int amount)
    {
        if (moneyPrefab == null) return;
        
        StartCoroutine(SpawnMoneyRoutine(sourcePosition, targetPosition, amount));
    }
    
    /// <summary>
    /// Spawns money icons that fly from source to a moving target transform.
    /// Used for money collection (money drop -> player).
    /// </summary>
    public void SpawnMoneyToPlayer(Vector3 sourcePosition, Transform playerTransform, int amount, System.Action onComplete = null)
    {
        if (moneyPrefab == null || playerTransform == null) return;
        
        StartCoroutine(SpawnMoneyToMovingTargetRoutine(sourcePosition, playerTransform, amount, onComplete));
    }
    
    private IEnumerator SpawnMoneyRoutine(Vector3 source, Vector3 target, int amount)
    {
        int iconCount = CalculateIconCount(amount);
        
        for (int i = 0; i < iconCount; i++)
        {
            Vector3 randomOffset = Random.insideUnitSphere * spreadRadius;
            randomOffset.y = 0;
            Vector3 startPos = source + randomOffset;
            
            // Calculate pitch for this coin (increases with each spawn)
            float currentPitch = Mathf.Min(basePitch + (i * pitchIncrement), maxPitch);
            
            // Play money flow sound with increasing pitch
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFXWithPitch(SoundEffect.MoneyFlow, currentPitch);
            }
            
            StartCoroutine(AnimateSingleMoney(startPos, target, false));
            
            yield return new WaitForSeconds(spawnDelay);
        }
    }
    
    private IEnumerator SpawnMoneyToMovingTargetRoutine(Vector3 source, Transform target, int amount, System.Action onComplete)
    {
        int iconCount = CalculateIconCount(amount);
        List<Coroutine> animations = new List<Coroutine>();
        
        for (int i = 0; i < iconCount; i++)
        {
            Vector3 randomOffset = Random.insideUnitSphere * spreadRadius;
            randomOffset.y = 0;
            Vector3 startPos = source + randomOffset;
            
            // Calculate pitch for this coin (increases with each spawn)
            float currentPitch = Mathf.Min(basePitch + (i * pitchIncrement), maxPitch);
            
            // Play money flow sound with increasing pitch
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFXWithPitch(SoundEffect.MoneyFlow, currentPitch);
            }
            
            bool isLast = (i == iconCount - 1);
            Coroutine anim = StartCoroutine(AnimateMoneyToMovingTarget(startPos, target, isLast, isLast ? onComplete : null));
            animations.Add(anim);
            
            yield return new WaitForSeconds(spawnDelay);
        }
    }
    
    private IEnumerator AnimateSingleMoney(Vector3 startPos, Vector3 targetPos, bool applyImpact)
    {
        GameObject moneyIcon = GetPooledIcon();
        if (moneyIcon == null) yield break;
        
        Transform iconTransform = moneyIcon.transform;
        iconTransform.position = startPos;
        iconTransform.localScale = originalPrefabScale;
        
        // Calculate control point for bezier curve (arc above)
        Vector3 midPoint = (startPos + targetPos) / 2f;
        Vector3 controlPoint = midPoint + Vector3.up * arcHeight;
        
        float elapsed = 0f;
        float randomRotationOffset = Random.Range(-90f, 90f);
        
        // Use try-finally to ensure cleanup happens
        try
        {
            while (elapsed < flyDuration && elapsed < maxLifetime)
            {
                // Safety check - if icon was destroyed externally, exit
                if (moneyIcon == null || !moneyIcon.activeInHierarchy)
                {
                    yield break;
                }
                
                elapsed += Time.deltaTime;
                float t = flyCurve.Evaluate(elapsed / flyDuration);
                
                // Quadratic bezier curve
                iconTransform.position = CalculateBezierPoint(t, startPos, controlPoint, targetPos);
                
                // Keep original scale throughout animation
                iconTransform.localScale = originalPrefabScale;
                
                // Rotation
                iconTransform.Rotate(Vector3.up, (rotationSpeed + randomRotationOffset) * Time.deltaTime);
                
                yield return null;
            }
        }
        finally
        {
            // Always return to pool, even if there was an error
            if (moneyIcon != null)
            {
                iconTransform.position = targetPos;
                ReturnToPool(moneyIcon);
            }
        }
    }
    
    private IEnumerator AnimateMoneyToMovingTarget(Vector3 startPos, Transform target, bool isLast, System.Action onComplete)
    {
        GameObject moneyIcon = GetPooledIcon();
        if (moneyIcon == null)
        {
            if (isLast) onComplete?.Invoke();
            yield break;
        }
        
        Transform iconTransform = moneyIcon.transform;
        iconTransform.position = startPos;
        iconTransform.localScale = originalPrefabScale;
        
        float elapsed = 0f;
        float randomRotationOffset = Random.Range(-90f, 90f);
        
        // Store initial target position for control point calculation
        Vector3 initialTargetPos = target != null ? target.position : startPos;
        Vector3 midPoint = (startPos + initialTargetPos) / 2f;
        Vector3 controlPoint = midPoint + Vector3.up * arcHeight;
        
        // Use try-finally to ensure cleanup happens
        try
        {
            while (elapsed < flyDuration && elapsed < maxLifetime)
            {
                // Safety check - if icon was destroyed externally or target is gone, exit
                if (moneyIcon == null || !moneyIcon.activeInHierarchy || target == null)
                {
                    yield break;
                }
                
                elapsed += Time.deltaTime;
                float t = flyCurve.Evaluate(elapsed / flyDuration);
                
                // Get current target position (player might have moved)
                Vector3 currentTargetPos = target.position + Vector3.up * 0.5f;
                
                // Recalculate bezier with updated target but keep control point stable for smooth arc
                Vector3 currentPos = CalculateBezierPointDynamic(t, startPos, controlPoint, currentTargetPos);
                iconTransform.position = currentPos;
                
                // Keep original scale throughout animation
                iconTransform.localScale = originalPrefabScale;
                
                // Rotation
                iconTransform.Rotate(Vector3.up, (rotationSpeed + randomRotationOffset) * Time.deltaTime);
                
                yield return null;
            }
        }
        finally
        {
            // Always return to pool, even if there was an error
            if (moneyIcon != null)
            {
                if (target != null)
                {
                    iconTransform.position = target.position + Vector3.up * 0.5f;
                }
                
                ReturnToPool(moneyIcon);
            }
            
            if (isLast)
            {
                onComplete?.Invoke();
            }
        }
    }
    
    private int CalculateIconCount(int amount)
    {
        return Mathf.Clamp(Mathf.CeilToInt(amount / 20f), minIconsPerSpawn, maxIconsPerSpawn);
    }
    
    /// <summary>
    /// Calculates a point on a quadratic bezier curve
    /// </summary>
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
    
    /// <summary>
    /// Calculates bezier point but allows the end point to change dynamically
    /// </summary>
    private Vector3 CalculateBezierPointDynamic(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        // Use modified calculation that makes the curve track the moving target better
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        
        Vector3 point = uu * p0;
        point += 2f * u * t * p1;
        point += tt * p2;
        
        return point;
    }
}
