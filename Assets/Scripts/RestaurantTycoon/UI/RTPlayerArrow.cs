using UnityEngine;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Attach to the Arrow Canvas GameObject that is a child of the player.
    /// The canvas should be positioned flat/horizontal above the player's head.
    ///
    /// When any RT spot becomes active (via RTSpotRegistry), the canvas enables
    /// and the arrow image rotates to point toward that spot in world space.
    /// When the last spot is hidden the canvas disables.
    ///
    /// Setup:
    ///   - arrowCanvas   → the Canvas GameObject this script lives on (or its parent)
    ///   - arrowTransform → the RectTransform of the arrow Image inside that canvas
    ///   - arrowImageDefaultAngle → set to match how your arrow art is oriented:
    ///       0   = arrow points toward world +Z at rest
    ///       90  = arrow points toward world +X at rest  (most common for "right-pointing" art)
    ///       -90 = arrow points toward world -X at rest
    ///       180 = arrow points toward world -Z at rest
    /// </summary>
    public class RTPlayerArrow : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Canvas that contains the arrow — will be enabled/disabled.")]
        [SerializeField] private GameObject arrowCanvas;
        [Tooltip("RectTransform of the arrow Image inside the canvas.")]
        [SerializeField] private RectTransform arrowTransform;

        [Header("Rotation")]
        [Tooltip("Offset in degrees to match your arrow art's default orientation.\n" +
                 "0 = art points toward +Z, 90 = art points toward +X, etc.")]
        [SerializeField] private float arrowImageDefaultAngle = 0f;
        [Tooltip("How fast the arrow smoothly turns toward the target (degrees/sec).")]
        [SerializeField] private float rotationSpeed = 360f;

        [Header("Bob Animation")]
        [Tooltip("How many units the arrow bobs up and down.")]
        [SerializeField] private float bobAmount = 6f;
        [Tooltip("Duration of one bob cycle (up OR down).")]
        [SerializeField] private float bobDuration = 0.4f;

        [Header("Breathe Animation")]
        [Tooltip("Scale the arrow breathes down to while active.")]
        [SerializeField] private float breatheScaleMin = 0.85f;
        [Tooltip("Scale the arrow breathes up to while active.")]
        [SerializeField] private float breatheScaleMax = 1.15f;
        [Tooltip("Duration of one breathe cycle (in OR out).")]
        [SerializeField] private float breatheDuration = 0.5f;

        [Header("Target Change Animation")]
        [Tooltip("Scale punch when the target switches.")]
        [SerializeField] private Vector3 punchScale = new Vector3(0.4f, 0.4f, 0f);
        [SerializeField] private float punchDuration = 0.35f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private Transform currentTarget;
        private float currentAngleZ;
        private Tween bobTween;
        private Tween breatheTween;
        private Vector3 arrowLocalOrigin;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (arrowCanvas == null)
                arrowCanvas = gameObject;

            if (arrowTransform != null)
                arrowLocalOrigin = arrowTransform.localPosition;

            arrowCanvas.SetActive(false);
        }

        private void OnEnable()
        {
            RTSpotRegistry.OnTargetChanged += OnTargetChanged;
            // Sync immediately in case spots were registered before this enabled.
            SyncTarget(animated: false);
        }

        private void OnDisable()
        {
            RTSpotRegistry.OnTargetChanged -= OnTargetChanged;
            StopBob();
            StopBreathe();
        }

        private void OnDestroy()
        {
            RTSpotRegistry.OnTargetChanged -= OnTargetChanged;
        }

        private void Update()
        {
            if (currentTarget == null || arrowTransform == null) return;

            // Project the direction to the target onto the horizontal plane.
            Vector3 toTarget = currentTarget.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.001f) return;

            // Compute the world-space angle of the target direction (from +Z axis).
            float worldAngle = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;

            // The canvas is flat above the player and rotates with the player.
            // We convert the world angle to local canvas space by subtracting the
            // player's Y rotation, then apply the art offset.
            float playerYAngle = transform.parent != null
                ? transform.parent.eulerAngles.y
                : transform.eulerAngles.y;

            float localAngle = worldAngle - playerYAngle - arrowImageDefaultAngle;

            // Smooth rotation.
            currentAngleZ = Mathf.MoveTowardsAngle(currentAngleZ, -localAngle, rotationSpeed * Time.deltaTime);
            arrowTransform.localEulerAngles = new Vector3(0f, 0f, currentAngleZ);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void OnTargetChanged()
        {
            SyncTarget(animated: true);
        }

        private void SyncTarget(bool animated)
        {
            Transform newTarget = RTSpotRegistry.CurrentTarget;
            bool targetActuallyChanged = newTarget != currentTarget;
            currentTarget = newTarget;

            if (currentTarget == null)
            {
                StopBob();
                StopBreathe();
                arrowCanvas.SetActive(false);
                return;
            }

            bool wasHidden = !arrowCanvas.activeSelf;
            arrowCanvas.SetActive(true);

            if (wasHidden)
            {
                StartBob();
                StartBreathe();
            }

            if (animated && targetActuallyChanged && arrowTransform != null)
            {
                arrowTransform.DOKill();
                arrowTransform.DOPunchScale(punchScale, punchDuration, 8, 0.5f);
            }
        }

        private void StartBob()
        {
            if (arrowTransform == null) return;
            StopBob();

            arrowTransform.localPosition = arrowLocalOrigin;
            bobTween = arrowTransform
                .DOLocalMoveY(arrowLocalOrigin.y + bobAmount, bobDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopBob()
        {
            bobTween?.Kill();
            bobTween = null;
            if (arrowTransform != null)
                arrowTransform.localPosition = arrowLocalOrigin;
        }

        private void StartBreathe()
        {
            if (arrowTransform == null) return;
            StopBreathe();

            arrowTransform.localScale = Vector3.one;
            breatheTween = arrowTransform
                .DOScale(breatheScaleMax, breatheDuration)
                .From(breatheScaleMin)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopBreathe()
        {
            breatheTween?.Kill();
            breatheTween = null;
            if (arrowTransform != null)
                arrowTransform.localScale = Vector3.one;
        }
    }
}
