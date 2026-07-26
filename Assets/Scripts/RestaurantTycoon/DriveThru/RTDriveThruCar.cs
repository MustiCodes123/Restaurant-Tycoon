using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Attach to a drive-through car prefab.
    ///
    /// RTDriveThruSpawner calls Initialize() which provides the ordered food type,
    /// the path waypoints, and the payment amount.
    ///
    /// Path is split into two segments:
    ///   Approach  : spawnPoint → approachWaypoints → stopPoint   (car drives in)
    ///   Departure : stopPoint  → departWaypoints   → destroyPoint (car drives away)
    ///
    /// At stopPoint the car waits, shows a food-order UI, and registers with
    /// RTSpotRegistry and DynamicMissionManager. When the player walks into the
    /// trigger carrying the matching FinishedItem the item is taken, money is
    /// dropped, and the car departs.
    ///
    /// The waypoint arrays are supplied by RTDriveThruSpawner; no local Inspector
    /// assignment is needed except for the visual/UI references.
    /// </summary>
    public class RTDriveThruCar : MonoBehaviour
    {
        // ── Serialized ────────────────────────────────────────────────────────

        [Header("Money")]
        [SerializeField] private GameObject moneyDropPrefab;

        // Set at runtime by RTDriveThruSpawner
        private Transform moneyDropPoint;

        [Header("Player Detection")]
        [SerializeField] private LayerMask playerLayer;

        [Header("Movement")]
        [SerializeField] private float driveSpeed = 8f;

        [Header("Wait")]
        [Tooltip("Seconds the car waits before driving away without being served.")]
        [SerializeField] private float waitTimeout = 45f;

        [Header("Order UI")]
        [Tooltip("Root canvas / panel that shows the food order. Will be scaled in/out.")]
        [SerializeField] private GameObject orderCanvas;
        [Tooltip("Each slot represents one possible food type. Assign itemType + RectTransform + Image on the prefab.")]
        [SerializeField] private List<FoodSlotUI> foodSlots = new List<FoodSlotUI>();
        [SerializeField] private float canvasShowDuration = 0.25f;
        [SerializeField] private float canvasHideDuration = 0.2f;

        [Header("Highlight Animation")]
        [Tooltip("DOPunchScale vector applied to the active food slot while waiting.")]
        [SerializeField] private Vector3 highlightPunchScale = new Vector3(0.25f, 0.25f, 0f);
        [SerializeField] private float highlightPulsePeriod = 0.9f;   // seconds between each punch

        [Header("Served Animation")]
        [Tooltip("One-shot celebratory punch when the player delivers the item.")]
        [SerializeField] private Vector3 servedPunchScale = new Vector3(0.5f, 0.5f, 0f);
        [SerializeField] private float servedPunchDuration = 0.4f;
        [Tooltip("Seconds after being served before the car drives away.")]
        [SerializeField] private float hideDelay = 0.8f;

        // ── Runtime (set by spawner) ───────────────────────────────────────────
        private RTIngredientType orderedItemType;
        private int orderPayment;
        private Vector3[] approachWaypoints;   // spawn → stop
        private Vector3 stopPosition;
        private Vector3[] departWaypoints;     // stop → destroy
        private Vector3 destroyPosition;

        // ── Internal ──────────────────────────────────────────────────────────
        private string carId;
        private RTPlayerCarryController playerCarry;
        private bool isWaiting;
        private bool isServed;
        private bool isDeparting;
        private Coroutine timeoutCoroutine;
        private Coroutine highlightCoroutine;
        private FoodSlotUI activeSlot;

        // ─────────────────────────────────────────────────────────────────────
        // Nested types
        // ─────────────────────────────────────────────────────────────────────

        [System.Serializable]
        public class FoodSlotUI
        {
            [Tooltip("The ingredient type this UI slot represents. Must match an RTIngredientType asset.")]
            public RTIngredientType itemType;
            [Tooltip("RectTransform of this slot's root (used for scale animations and show/hide).")]
            public RectTransform slotTransform;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API (called by RTDriveThruSpawner)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called immediately after Instantiate by RTDriveThruSpawner.
        /// Kicks off the approach path, then waits at the stop point.
        /// </summary>
        /// <param name="itemType">The food the customer wants.</param>
        /// <param name="payment">Money dropped on successful service.</param>
        /// <param name="approach">Waypoints from spawn to stop (including stop as last element).</param>
        /// <param name="depart">Waypoints from stop to destroy (including destroy as last element).</param>
        public void Initialize(RTIngredientType itemType, int payment,
                               Vector3[] approach, Vector3[] depart, Transform dropPoint = null)
        {
            orderedItemType = itemType;
            orderPayment    = payment;
            approachWaypoints = approach;
            departWaypoints   = depart;
            moneyDropPoint    = dropPoint;

            stopPosition    = approach[approach.Length - 1];
            destroyPosition = depart[depart.Length - 1];

            carId = GetInstanceID().ToString();

            // Keep canvas always active — only slots are toggled.
            // This avoids conflicts with the Billboard script on the canvas root.
            if (orderCanvas != null)
                orderCanvas.SetActive(true);

            // Hide all slots immediately.
            foreach (var slot in foodSlots)
                if (slot.slotTransform != null)
                    slot.slotTransform.gameObject.SetActive(false);

            DriveApproach();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Movement
        // ─────────────────────────────────────────────────────────────────────

        private void DriveApproach()
        {
            if (approachWaypoints == null || approachWaypoints.Length == 0)
            {
                ArriveAtStop();
                return;
            }
            StartCoroutine(MoveAlongPath(approachWaypoints, ArriveAtStop));
        }

        private void ArriveAtStop()
        {
            transform.position = stopPosition;
            isWaiting = true;

            ShowUI();
            StartHighlightPulse();
            RTSpotRegistry.RegisterSpot(transform);

            string foodName = orderedItemType != null ? orderedItemType.displayName : "food";
            DynamicMissionManager.Instance?.RegisterDriveThruMission(carId, foodName);

            timeoutCoroutine = StartCoroutine(TimeoutCoroutine());
        }

        private void DriveAway()
        {
            if (isDeparting) return;
            isDeparting = true;

            StopHighlightPulse();
            RTSpotRegistry.UnregisterSpot(transform);

            if (departWaypoints == null || departWaypoints.Length == 0)
            {
                Destroy(gameObject);
                return;
            }
            StartCoroutine(MoveAlongPath(departWaypoints, () => Destroy(gameObject)));
        }

        /// <summary>
        /// Moves the car through an array of world-space waypoints one at a time.
        /// Rotates toward each point before moving. Calls onComplete when done.
        /// </summary>
        private IEnumerator MoveAlongPath(Vector3[] waypoints, System.Action onComplete)
        {
            foreach (var point in waypoints)
            {
                // Face the next waypoint instantly (Y-axis only).
                Vector3 dir = point - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dir);

                float dist = Vector3.Distance(transform.position, point);
                if (dist < 0.001f) continue;

                float duration = dist / driveSpeed;

                // Kill any existing move tween before starting a new one.
                transform.DOKill(false);
                transform.DOMove(point, duration).SetEase(Ease.Linear);

                // Use WaitForSeconds — guaranteed to advance unlike WaitForCompletion.
                yield return new WaitForSeconds(duration);

                // Snap to exact position in case of floating-point drift.
                transform.position = point;
            }

            onComplete?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Player trigger
        // ─────────────────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!isWaiting || isServed || isDeparting) return;
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            if (playerCarry == null)
            {
                playerCarry = other.GetComponent<RTPlayerCarryController>();
                if (playerCarry == null)
                    playerCarry = other.GetComponentInParent<RTPlayerCarryController>();
            }

            TryServePlayer();
        }

        private void OnTriggerStay(Collider other)
        {
            if (!isWaiting || isServed || isDeparting) return;
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            TryServePlayer();
        }

        private void TryServePlayer()
        {
            if (playerCarry == null || orderedItemType == null) return;

            System.Func<IRTCarryable, bool> match = (IRTCarryable i) =>
                i.CarryType == CarryableType.FinishedItem &&
                i.GameObject.GetComponent<RTFinishedItem>()?.ItemType == orderedItemType;

            if (playerCarry.PeekTopItem(match) == null) return;

            IRTCarryable item = playerCarry.TakeTopItem(match);
            if (item == null) return;

            // Destroy the item visually — it's been "taken" by the car.
            Destroy(item.GameObject);

            OnServed();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Served / Timeout
        // ─────────────────────────────────────────────────────────────────────

        private void OnServed()
        {
            if (isServed) return;
            isServed = true;
            isWaiting = false;

            StopTimeout();

            SpawnMoneyDrop();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SoundEffect.CustomerServed);

            // Celebratory punch on the active slot.
            if (activeSlot?.slotTransform != null)
            {
                activeSlot.slotTransform.DOKill();
                activeSlot.slotTransform.DOPunchScale(servedPunchScale, servedPunchDuration, 8, 0.5f);
            }

            DynamicMissionManager.Instance?.CompleteDriveThruMission(carId);

            StartCoroutine(HideThenDepart());
        }

        private IEnumerator HideThenDepart()
        {
            yield return new WaitForSeconds(hideDelay);
            HideUI();
            yield return new WaitForSeconds(canvasHideDuration + 0.05f);
            DriveAway();
        }

        private IEnumerator TimeoutCoroutine()
        {
            yield return new WaitForSeconds(waitTimeout);

            if (!isServed && !isDeparting)
            {
                isWaiting = false;
                DynamicMissionManager.Instance?.RemoveDriveThruMission(carId);
                HideUI();
                yield return new WaitForSeconds(canvasHideDuration + 0.05f);
                DriveAway();
            }
        }

        private void StopTimeout()
        {
            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
                timeoutCoroutine = null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Money
        // ─────────────────────────────────────────────────────────────────────

        private void SpawnMoneyDrop()
        {
            if (moneyDropPrefab == null)
            {
                // Fallback: register directly.
                if (RTLevelManager.Instance != null)
                    RTLevelManager.Instance.RegisterMoneyEarned(orderPayment);
                else
                    CurrencyManager.Instance?.AddMoney(orderPayment);
                return;
            }

            Vector3 spawnPos = moneyDropPoint != null ? moneyDropPoint.position : transform.position;
            GameObject dropGO = Instantiate(moneyDropPrefab, spawnPos, Quaternion.identity);
            MoneyDrop drop = dropGO.GetComponent<MoneyDrop>();
            drop?.Initialize(orderPayment);
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI
        // ─────────────────────────────────────────────────────────────────────

        private void ShowUI()
        {
            if (orderCanvas == null)
            {
                Debug.LogWarning("[RTDriveThruCar] orderCanvas is not assigned!");
                return;
            }

            if (orderedItemType == null)
                Debug.LogWarning("[RTDriveThruCar] orderedItemType is null — no slot will match!");

            activeSlot = null;
            foreach (var slot in foodSlots)
            {
                if (slot.slotTransform == null) continue;

                bool isMatch = slot.itemType != null && slot.itemType == orderedItemType;
                Debug.Log($"[RTDriveThruCar] Slot '{slot.slotTransform.name}': itemType={slot.itemType?.displayName ?? "NULL"}, ordered={orderedItemType?.displayName ?? "NULL"}, match={isMatch}");

                if (isMatch)
                {
                    activeSlot = slot;
                    slot.slotTransform.gameObject.SetActive(true);
                    // Set to full scale immediately — avoids DOScale-from-zero staying invisible.
                    slot.slotTransform.localScale = Vector3.one;
                    slot.slotTransform.DOKill();
                    slot.slotTransform.DOPunchScale(new Vector3(0.35f, 0.35f, 0f), canvasShowDuration, 6, 0.5f);
                }
                else
                {
                    slot.slotTransform.gameObject.SetActive(false);
                }
            }

            // Fallback: show all slots if nothing matched.
            if (activeSlot == null)
            {
                Debug.LogWarning("[RTDriveThruCar] No slot matched orderedItemType — enabling all slots as fallback.");
                foreach (var slot in foodSlots)
                {
                    if (slot.slotTransform == null) continue;
                    slot.slotTransform.gameObject.SetActive(true);
                    slot.slotTransform.localScale = Vector3.one;
                }
                if (foodSlots.Count > 0) activeSlot = foodSlots[0];
            }
        }

        private void HideUI()
        {
            if (activeSlot?.slotTransform == null) return;

            activeSlot.slotTransform.DOKill();
            activeSlot.slotTransform
                .DOScale(Vector3.zero, canvasHideDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (activeSlot?.slotTransform != null)
                        activeSlot.slotTransform.gameObject.SetActive(false);
                });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Highlight pulse
        // ─────────────────────────────────────────────────────────────────────

        private void StartHighlightPulse()
        {
            StopHighlightPulse();
            if (activeSlot?.slotTransform != null)
                highlightCoroutine = StartCoroutine(HighlightPulseCoroutine());
        }

        private void StopHighlightPulse()
        {
            if (highlightCoroutine != null)
            {
                StopCoroutine(highlightCoroutine);
                highlightCoroutine = null;
            }
            if (activeSlot?.slotTransform != null)
            {
                activeSlot.slotTransform.DOKill();
                activeSlot.slotTransform.localScale = Vector3.one;
            }
        }

        private IEnumerator HighlightPulseCoroutine()
        {
            var wait = new WaitForSeconds(highlightPulsePeriod);
            while (isWaiting && activeSlot?.slotTransform != null)
            {
                activeSlot.slotTransform.DOKill();
                activeSlot.slotTransform.DOPunchScale(highlightPunchScale, highlightPulsePeriod * 0.8f, 6, 0.4f);
                yield return wait;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Cleanup
        // ─────────────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            // Safety: unregister if destroyed unexpectedly (e.g. scene change).
            RTSpotRegistry.UnregisterSpot(transform);
            StopTimeout();
        }
    }
}
