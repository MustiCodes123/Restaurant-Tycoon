using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

public class CurrencyUI : MonoBehaviour
{
    public static CurrencyUI Instance { get; private set; }
    
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI cashText;
    [SerializeField] private RectTransform cashIconTransform;
    
    [Header("Animation Settings")]
    [SerializeField] private float popScale = 1.2f;
    [SerializeField] private float popDuration = 0.2f;
    [SerializeField] private float counterDuration = 0.5f;
    
    [Header("Money Fly Animation")]
    [SerializeField] private GameObject moneyIconPrefab;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera uiCamera;
    
    [Header("Fly Animation Settings")]
    [SerializeField] private int minIconCount = 3;
    [SerializeField] private int maxIconCount = 8;
    [SerializeField] private float flyDuration = 1.4f;
    [SerializeField] private float spawnDelay = 0.05f;
    [SerializeField] private float spreadRadius = 80f;
    [SerializeField] private float arcHeight = 150f;
    [SerializeField] private float startScale = 1.2f;
    [SerializeField] private float endScale = 0.6f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private AnimationCurve flyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool usePooling = true;
    [SerializeField] private int poolSize = 20;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    private Queue<GameObject> iconPool = new Queue<GameObject>();
    
    private int displayedMoney = 0;
    private Coroutine counterCoroutine;
    private RectTransform canvasRect;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }
        
        if (canvas != null)
        {
            canvasRect = canvas.transform as RectTransform;
            
            // Get the camera for screen space canvas
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && uiCamera == null)
            {
                uiCamera = canvas.worldCamera;
            }
        }
        
        // Initialize object pool
        if (usePooling && moneyIconPrefab != null)
        {
            InitializePool();
        }
    }
    
    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject icon = Instantiate(moneyIconPrefab, canvas.transform);
            icon.SetActive(false);
            iconPool.Enqueue(icon);
        }
    }
    
    private GameObject GetPooledIcon()
    {
        if (iconPool.Count > 0)
        {
            GameObject icon = iconPool.Dequeue();
            icon.SetActive(true);
            return icon;
        }
        
        // Create new one if pool is empty
        return Instantiate(moneyIconPrefab, canvas.transform);
    }
    
    private void ReturnToPool(GameObject icon)
    {
        if (usePooling)
        {
            icon.SetActive(false);
            icon.transform.localScale = Vector3.one;
            icon.transform.localRotation = Quaternion.identity;
            iconPool.Enqueue(icon);
        }
        else
        {
            Destroy(icon);
        }
    }
    
    private void Start()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnMoneyChanged += OnMoneyChanged;
            displayedMoney = CurrencyManager.Instance.CurrentMoney;
            UpdateCashText(displayedMoney);
        }
    }
    
    private void OnMoneyChanged(int newAmount)
    {
        // Animate the counter and pop effect
        AnimateMoneyChange(newAmount);
    }
    
    private void AnimateMoneyChange(int newAmount)
    {
        // Stop any existing counter animation
        if (counterCoroutine != null)
        {
            StopCoroutine(counterCoroutine);
        }
        
        // Start counting animation
        counterCoroutine = StartCoroutine(AnimateCounter(displayedMoney, newAmount));
        
        // Pop animation
        if (cashIconTransform != null)
        {
            cashIconTransform.DOKill();
            cashIconTransform.localScale = Vector3.one;
            cashIconTransform.DOPunchScale(Vector3.one * (popScale - 1f), popDuration, 10, 1f);
        }
        else if (cashText != null)
        {
            cashText.transform.DOKill();
            cashText.transform.localScale = Vector3.one;
            cashText.transform.DOPunchScale(Vector3.one * (popScale - 1f), popDuration, 10, 1f);
        }
    }
    
    private IEnumerator AnimateCounter(int fromValue, int toValue)
    {
        float elapsed = 0f;
        
        while (elapsed < counterDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / counterDuration;
            
            // Ease out cubic for smooth deceleration
            t = 1f - Mathf.Pow(1f - t, 3f);
            
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(fromValue, toValue, t));
            displayedMoney = currentValue;
            UpdateCashText(currentValue);
            
            yield return null;
        }
        
        displayedMoney = toValue;
        UpdateCashText(toValue);
    }
    
    private void UpdateCashText(int amount)
    {
        if (cashText != null)
        {
            cashText.text = $"${amount}";
        }
    }
    
    public void AnimateMoneyToUI(Vector3 worldPosition, int amount)
    {
        if (moneyIconPrefab == null || canvas == null || cashIconTransform == null)
        {
            Debug.LogWarning("Missing references for money animation!");
            return;
        }
        
        StartCoroutine(SpawnMoneyIcons(worldPosition, amount));
    }
    
    /// <summary>
    /// Spawns multiple money icons that fly to the UI with staggered timing
    /// </summary>
    private IEnumerator SpawnMoneyIcons(Vector3 worldPosition, int amount)
    {
        // Calculate number of icons based on amount (more money = more icons)
        int iconCount = Mathf.Clamp(Mathf.CeilToInt(amount / 10f), minIconCount, maxIconCount);
        
        // Convert world position to canvas position once
        Vector2 startCanvasPos = WorldToCanvasPosition(worldPosition);
        Vector2 targetPos = GetTargetPosition();
        
        if (debugMode)
        {
            Debug.Log($"[CurrencyUI] World Pos: {worldPosition}, Start Canvas Pos: {startCanvasPos}, Target Pos: {targetPos}");
            Debug.Log($"[CurrencyUI] Canvas Mode: {canvas.renderMode}, Cash Icon Anchored Pos: {cashIconTransform.anchoredPosition}");
        }
        
        for (int i = 0; i < iconCount; i++)
        {
            // Add random spread to start position
            Vector2 randomOffset = Random.insideUnitCircle * spreadRadius;
            Vector2 iconStartPos = startCanvasPos + randomOffset;
            
            StartCoroutine(AnimateSingleMoneyIcon(iconStartPos, targetPos, i == iconCount - 1));
            
            yield return new WaitForSeconds(spawnDelay);
        }
    }
    
    /// <summary>
    /// Converts world position to canvas local position, handling different canvas render modes
    /// </summary>
    private Vector2 WorldToCanvasPosition(Vector3 worldPosition)
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector2.zero;
        
        Vector2 screenPos = cam.WorldToScreenPoint(worldPosition);
        
        // Handle different canvas render modes
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                null,
                out Vector2 canvasPos
            );
            return canvasPos;
        }
        else
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                uiCamera ?? canvas.worldCamera,
                out Vector2 canvasPos
            );
            return canvasPos;
        }
    }
    
    /// <summary>
    /// Gets the target position (cash icon) in canvas local space as anchored position
    /// </summary>
    private Vector2 GetTargetPosition()
    {
        if (cashIconTransform != null)
        {
            // Convert cash icon world position to screen position, then to canvas anchored position
            // This ensures consistency with how we calculate start positions
            Vector2 screenPos;
            
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // For overlay canvas, use the rect transform's screen position
                screenPos = RectTransformUtility.WorldToScreenPoint(null, cashIconTransform.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPos,
                    null,
                    out Vector2 canvasPos
                );
                return canvasPos;
            }
            else
            {
                // For camera-based canvas
                screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera ?? canvas.worldCamera, cashIconTransform.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPos,
                    uiCamera ?? canvas.worldCamera,
                    out Vector2 canvasPos
                );
                return canvasPos;
            }
        }
        return Vector2.zero;
    }
    
    /// <summary>
    /// Animates a single money icon along a bezier curve path
    /// </summary>
    private IEnumerator AnimateSingleMoneyIcon(Vector2 startPos, Vector2 targetPos, bool isLastIcon)
    {
        GameObject moneyIcon = GetPooledIcon();
        RectTransform iconRect = moneyIcon.GetComponent<RectTransform>();
        
        if (iconRect == null)
        {
            ReturnToPool(moneyIcon);
            yield break;
        }
        
        // Ensure the icon is properly set up for consistent positioning
        // Set anchors to center so anchoredPosition works predictably
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        
        // Reset transform and set start position
        iconRect.anchoredPosition = startPos;
        iconRect.localScale = Vector3.one * startScale;
        iconRect.localRotation = Quaternion.identity;
        
        // Calculate bezier control point for arc (perpendicular to path, going up)
        Vector2 midPoint = (startPos + targetPos) / 2f;
        Vector2 direction = (targetPos - startPos).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        
        // Control point is above the midpoint for a nice arc
        // Add some randomness to the arc
        float randomArcOffset = Random.Range(-arcHeight * 0.3f, arcHeight * 0.3f);
        Vector2 controlPoint = midPoint + perpendicular * (arcHeight + randomArcOffset);
        
        // If target is above start, flip the arc direction
        if (targetPos.y > startPos.y)
        {
            controlPoint = midPoint - perpendicular * (arcHeight + randomArcOffset) * 0.5f;
        }
        
        // Random rotation direction
        float rotationDirection = Random.value > 0.5f ? 1f : -1f;
        float randomRotationSpeed = rotationSpeed * Random.Range(0.7f, 1.3f);
        
        // Animate
        float elapsed = 0f;
        float duration = flyDuration * Random.Range(0.9f, 1.1f); // Slight variation
        
        // Initial pop effect
        iconRect.DOPunchScale(Vector3.one * 0.3f, 0.15f, 5, 0.5f);
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            // Apply custom animation curve for smooth easing
            float easedT = flyCurve.Evaluate(t);
            
            // Quadratic bezier curve position
            Vector2 pos = CalculateQuadraticBezierPoint(easedT, startPos, controlPoint, targetPos);
            iconRect.anchoredPosition = pos;
            
            // Scale animation (start big, end small with ease)
            float scaleT = Mathf.SmoothStep(0f, 1f, t);
            float scale = Mathf.Lerp(startScale, endScale, scaleT);
            iconRect.localScale = Vector3.one * scale;
            
            // Rotation animation (slow down as it approaches target)
            float rotationEase = 1f - (t * t); // Quadratic ease out
            iconRect.Rotate(0f, 0f, rotationDirection * randomRotationSpeed * rotationEase * Time.deltaTime);
            
            // Add slight wobble for more organic feel
            float wobble = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * 5f;
            iconRect.anchoredPosition += new Vector2(wobble, 0);
            
            yield return null;
        }
        
        // Snap to final position
        iconRect.anchoredPosition = targetPos;
        
        // Impact effect on the cash icon when last icon arrives
        if (isLastIcon && cashIconTransform != null)
        {
            cashIconTransform.DOKill();
            cashIconTransform.localScale = Vector3.one;
            cashIconTransform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 8, 0.5f);
        }
        
        // Small impact punch on the icon itself
        iconRect.DOPunchScale(Vector3.one * 0.2f, 0.1f, 3, 0.5f);
        yield return new WaitForSeconds(0.1f);
        
        // Return to pool
        ReturnToPool(moneyIcon);
    }
    
    /// <summary>
    /// Calculates a point on a quadratic bezier curve
    /// </summary>
    private Vector2 CalculateQuadraticBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        
        Vector2 point = uu * p0;           // (1-t)^2 * P0
        point += 2f * u * t * p1;          // 2(1-t)t * P1
        point += tt * p2;                   // t^2 * P2
        
        return point;
    }
    
    /// <summary>
    /// Alternative method: Animate money from a screen position directly (useful for UI-to-UI animations)
    /// </summary>
    public void AnimateMoneyFromScreenPosition(Vector2 screenPosition, int amount)
    {
        if (moneyIconPrefab == null || canvas == null || cashIconTransform == null)
        {
            return;
        }
        
        Vector2 canvasPos;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out canvasPos);
        }
        else
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera ?? canvas.worldCamera, out canvasPos);
        }
        
        StartCoroutine(SpawnMoneyIconsFromCanvasPos(canvasPos, amount));
    }
    
    private IEnumerator SpawnMoneyIconsFromCanvasPos(Vector2 startPos, int amount)
    {
        int iconCount = Mathf.Clamp(Mathf.CeilToInt(amount / 10f), minIconCount, maxIconCount);
        Vector2 targetPos = GetTargetPosition();
        
        for (int i = 0; i < iconCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * spreadRadius;
            Vector2 iconStartPos = startPos + randomOffset;
            
            StartCoroutine(AnimateSingleMoneyIcon(iconStartPos, targetPos, i == iconCount - 1));
            
            yield return new WaitForSeconds(spawnDelay);
        }
    }
    
    public Vector3 GetCashUIWorldPosition()
    {
        if (cashIconTransform != null)
        {
            return cashIconTransform.position;
        }
        else if (cashText != null)
        {
            return cashText.transform.position;
        }
        
        return Vector3.zero;
    }
    
    private void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
        }
    }
}
