using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

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

        [Header("Tutorial Waypoints")]
        [Tooltip("Ordered GameObjects the arrow guides the player through at game start. Completes once and never repeats (saved via PlayerPrefs).")]
        [SerializeField] private List<GameObject> tutorialWaypoints = new List<GameObject>();
        [Tooltip("Horizontal distance (XZ) from the player to a waypoint that counts as arrived and advances to the next.")]
        [SerializeField] private float tutorialArrivalDistance = 2f;

        // ── Runtime ─────────────────────────────────────────────────────
        private const string TUTORIAL_DONE_KEY = "RTPlayerArrow_TutorialDone";
        private Transform currentTarget;
        private float currentAngleZ;
        private Tween bobTween;
        private Tween breatheTween;
        private Vector3 arrowLocalOrigin;
        private int tutorialIndex;
        private bool tutorialActive;
        private Transform tutorialOverrideTarget; // set by RTTutorialController

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (arrowCanvas == null)
                arrowCanvas = gameObject;

            if (arrowTransform != null)
                arrowLocalOrigin = arrowTransform.localPosition;

            bool tutorialDone = PlayerPrefs.GetInt(TUTORIAL_DONE_KEY, 0) == 1;
            tutorialActive = !tutorialDone && tutorialWaypoints != null && tutorialWaypoints.Count > 0;
            tutorialIndex = 0;

            arrowCanvas.SetActive(false);
        }

        private void OnEnable()
        {
            RTSpotRegistry.OnTargetChanged += OnTargetChanged;

            if (tutorialActive)
            {
                ApplyTutorialTarget();
                arrowCanvas.SetActive(true);
                return;
            }

            // Sync immediately in case spots were registered before this enabled.
            SyncTarget(animated: false);
        }

        private void OnDisable()
        {
            RTSpotRegistry.OnTargetChanged -= OnTargetChanged;
        }

        private void OnDestroy()
        {
            RTSpotRegistry.OnTargetChanged -= OnTargetChanged;
        }

        private void Update()
        {
            if (tutorialActive)
                TickTutorial();

            // Controller-driven override always wins
            if (tutorialOverrideTarget != null)
                currentTarget = tutorialOverrideTarget;

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
            // Suppress registry changes while an override or waypoint tutorial is active
            if (tutorialActive || tutorialOverrideTarget != null) return;
            SyncTarget(animated: true);
        }

        private void SyncTarget(bool animated)
        {
            Transform newTarget = RTSpotRegistry.CurrentTarget;
            bool targetActuallyChanged = newTarget != currentTarget;
            currentTarget = newTarget;

            if (currentTarget == null)
            {
                arrowCanvas.SetActive(false);
                return;
            }

            arrowCanvas.SetActive(true);
        }

        private void StartBob() { }
        private void StopBob() { }
        private void StartBreathe() { }
        private void StopBreathe() { }

        // ── Public override API (used by RTTutorialController) ─────────────────────

        /// <summary>
        /// Point the arrow at a specific world transform, overriding the RTSpotRegistry.
        /// Call ClearTutorialOverride() to return to normal registry behaviour.
        /// </summary>
        public void SetTutorialOverrideTarget(Transform target)
        {
            tutorialOverrideTarget = target;
            currentTarget = target;

            if (target != null)
                arrowCanvas.SetActive(true);
        }

        /// <summary>
        /// Remove the controller override and resume normal RTSpotRegistry-driven behaviour.
        /// </summary>
        public void ClearTutorialOverride()
        {
            tutorialOverrideTarget = null;
            SyncTarget(animated: false);
        }

        // ── Tutorial ──────────────────────────────────────────────────────────

        private void TickTutorial()
        {
            if (tutorialIndex >= tutorialWaypoints.Count)
            {
                CompleteTutorial();
                return;
            }

            // Skip any null entries
            if (tutorialWaypoints[tutorialIndex] == null)
            {
                tutorialIndex++;
                ApplyTutorialTarget();
                return;
            }

            // Use XZ distance so vertical offset of the canvas doesn't affect the check
            Vector3 playerPos = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 toWp = tutorialWaypoints[tutorialIndex].transform.position - playerPos;
            toWp.y = 0f;
            if (toWp.magnitude <= tutorialArrivalDistance)
            {
                tutorialIndex++;
                ApplyTutorialTarget();
            }
        }

        private void ApplyTutorialTarget()
        {
            // Skip null entries
            while (tutorialIndex < tutorialWaypoints.Count && tutorialWaypoints[tutorialIndex] == null)
                tutorialIndex++;

            if (tutorialIndex >= tutorialWaypoints.Count)
            {
                CompleteTutorial();
                return;
            }

            currentTarget = tutorialWaypoints[tutorialIndex].transform;
        }

        private void CompleteTutorial()
        {
            tutorialActive = false;
            PlayerPrefs.SetInt(TUTORIAL_DONE_KEY, 1);
            PlayerPrefs.Save();
            Debug.Log("[RTPlayerArrow] Tutorial waypoint sequence complete.");
            // Resume normal registry-driven arrow behaviour
            SyncTarget(animated: false);
        }
    }
}
