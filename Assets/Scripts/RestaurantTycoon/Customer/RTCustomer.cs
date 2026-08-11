using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

namespace RestaurantTycoon
{
    public enum RTCustomerState
    {
        MovingToCounter,
        WaitingAtCounter,
        PickingUpItem,
        WaitingForSeat,
        MovingToTable,
        SittingDown,
        Eating,
        StandingUp,
        MovingToCashier,
        WaitingAtCashier,
        Leaving
    }

    /// <summary>
    /// Restaurant customer. Walks to counter, waits for finished items, picks them up.
    /// Demands a random number of items (1-4). Shows waiting UI with "received/total" text.
    /// Later steps will add dining, cashier, and leaving behaviour.
    /// </summary>
    public class RTCustomer : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float arrivalThreshold = 0.5f;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("Appearance")]
        [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

        [Header("Item Hold")]
        [Tooltip("Where the picked-up item attaches (customer's hand)")]
        [SerializeField] private Transform itemHoldPoint;
        [Tooltip("Offset per stacked item (local space, applied above hold point)")]
        [SerializeField] private Vector3 itemHoldOffset = new Vector3(0, 0.3f, 0);

        [Header("Item Demand")]
        [SerializeField] private int minItemDemand = 1;
        [SerializeField] private int maxItemDemand = 4;

        [Header("Pickup Animation")]
        [SerializeField] private float pickupBounceHeight = 0.4f;
        [SerializeField] private float pickupBounceDuration = 0.3f;
        [SerializeField] private float pickupInterval = 0.3f;

        [Header("Waiting UI")]
        [SerializeField] private Canvas waitingUICanvas;
        [SerializeField] private Transform waitingUI;
        [SerializeField] private TextMeshProUGUI demandText;
        [SerializeField] private float pulseMinScale = 0.8f;
        [SerializeField] private float pulseMaxScale = 1.2f;
        [SerializeField] private float pulseDuration = 0.5f;
        [SerializeField] private Ease pulseEase = Ease.InOutSine;

        [Header("Money")]
        [SerializeField] private int moneyPerCustomer = 10;

        [Header("Dining")]
        [SerializeField] private float sitDuration = 0.5f;
        [SerializeField] private float standDuration = 0.5f;
        [SerializeField] private float itemPlaceDuration = 0.2f;
        [SerializeField] private float itemPlaceInterval = 0.15f;
        [SerializeField] private float itemPlaceJumpHeight = 0.2f;

        // Components
        private NavMeshAgent agent;
        private float baseAgentSpeed = 3.5f;
        private RTCustomerState currentState;

        // References
        private RTCustomerCounter targetCounter;
        private RTCustomerSpawner spawner;
        private Transform exitPoint;

        // Items
        private int itemDemand;
        private int itemsReceived;
        private List<RTFinishedItem> heldItems = new List<RTFinishedItem>();

        // Queue
        private int queuePosition;
        private bool isMoving;
        private bool hasArrivedAtQueuePosition;
        private Vector3 currentDestination;

        // Dining
        private RTDiningArea diningArea;
        private RTDiningSeat assignedSeat;
        private Coroutine eatingCoroutine;

        // Cashier
        private RTCashier targetCashier;
        private int cashierQueuePosition;
        private bool hasArrivedAtCashierPosition;

        // Tweens
        private Tween waitingUITween;
        private Coroutine pickupCoroutine;

        public RTCustomerState State => currentState;
        public RTCustomerSpawner Spawner => spawner;
        public bool IsWaitingAtCounter => currentState == RTCustomerState.WaitingAtCounter && hasArrivedAtQueuePosition;
        public bool IsWaitingAtCashier => currentState == RTCustomerState.WaitingAtCashier && hasArrivedAtCashierPosition;
        public int ItemDemand => itemDemand;
        public int ItemsReceived => itemsReceived;
        public int MoneyPerCustomer => moneyPerCustomer;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
                baseAgentSpeed = agent.speed;
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            HideWaitingUI();
        }

        public void ApplyRewardSpeedMultiplier()
        {
            if (agent != null)
                agent.speed = baseAgentSpeed * RTRewardedAdSystem.CharacterSpeedMultiplier;
        }

        private void Update()
        {
            if (isMoving && agent != null && !agent.pathPending)
            {
                if (agent.remainingDistance <= arrivalThreshold)
                {
                    if (agent.velocity.sqrMagnitude < 0.01f || !agent.hasPath)
                    {
                        StopMoving();
                        OnReachedDestination();
                    }
                }
            }
        }

        #region Initialization

        /// <summary>
        /// Initialize the customer with references and random appearance.
        /// </summary>
        public void Initialize(RTCustomerCounter counter, Transform exit, RTCustomerSpawner customerSpawner, List<Material> skins, RTDiningArea dining = null, RTCashier cashier = null)
        {
            targetCounter = counter;
            exitPoint = exit;
            spawner = customerSpawner;
            diningArea = dining;
            targetCashier = cashier;

            // Random skin
            if (skinnedMeshRenderer != null && skins != null && skins.Count > 0)
            {
                int index = Random.Range(0, skins.Count);
                skinnedMeshRenderer.material = skins[index];
            }

            // Random item demand
            itemDemand = Random.Range(minItemDemand, maxItemDemand + 1);
            itemsReceived = 0;

            // Join queue
            JoinCounterQueue();
        }

        #endregion

        #region Counter Queue

        private void JoinCounterQueue()
        {
            if (targetCounter == null)
            {
                Debug.LogError("[RTCustomer] No target counter assigned!");
                StartLeaving();
                return;
            }

            queuePosition = targetCounter.AddCustomerToQueue(this);
            if (queuePosition < 0)
            {
                Debug.LogWarning("[RTCustomer] Counter queue full!");
                StartLeaving();
                return;
            }

            hasArrivedAtQueuePosition = false;
            currentState = RTCustomerState.MovingToCounter;
            MoveTo(targetCounter.GetQueueWorldPosition(queuePosition));
        }

        /// <summary>
        /// Called by RTCustomerCounter when queue advances.
        /// Must physically walk to new position before being served.
        /// </summary>
        public void OnCounterQueuePositionChanged(int newPosition)
        {
            queuePosition = newPosition;
            hasArrivedAtQueuePosition = false;
            currentState = RTCustomerState.MovingToCounter;
            MoveTo(targetCounter.GetQueueWorldPosition(newPosition));
        }

        #endregion

        #region State Handling

        private void OnReachedDestination()
        {
            switch (currentState)
            {
                case RTCustomerState.MovingToCounter:
                    OnArrivedAtCounter();
                    break;

                case RTCustomerState.Leaving:
                    OnExited();
                    break;

                case RTCustomerState.MovingToTable:
                    OnArrivedAtTable();
                    break;

                case RTCustomerState.MovingToCashier:
                    OnArrivedAtCashier();
                    break;
            }
        }

        private void OnArrivedAtCounter()
        {
            hasArrivedAtQueuePosition = true;
            currentState = RTCustomerState.WaitingAtCounter;

            // Set correct idle animation based on carrying state
            if (heldItems.Count > 0)
                SetLiftIdle(true);
            else
                SetWalking(false);

            // Face the counter
            if (targetCounter != null)
            {
                Vector3 lookDir = (targetCounter.transform.position - transform.position);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(lookDir.normalized);
            }

            // If at front of queue, show UI and try to take items
            if (queuePosition == 0)
            {
                ShowWaitingUI();
                TryPickUpItem();
            }
        }

        /// <summary>
        /// Try to pick up items from the counter.
        /// Called when customer arrives at front, when queue advances, or when item is placed.
        /// Only works when physically arrived at position 0.
        /// </summary>
        public void TryPickUpItem()
        {
            if (currentState != RTCustomerState.WaitingAtCounter) return;
            if (!hasArrivedAtQueuePosition) return;
            if (queuePosition != 0) return;
            if (targetCounter == null || !targetCounter.HasItem) return;
            if (itemsReceived >= itemDemand) return;
            if (pickupCoroutine != null) return;

            pickupCoroutine = StartCoroutine(PickupItemsCoroutine());
        }

        private IEnumerator PickupItemsCoroutine()
        {
            while (itemsReceived < itemDemand)
            {
                if (targetCounter == null || !targetCounter.HasItem)
                {
                    // No items available, wait for more
                    currentState = RTCustomerState.WaitingAtCounter;
                    pickupCoroutine = null;
                    yield break;
                }

                RTFinishedItem item = targetCounter.TakeItem();
                if (item == null)
                {
                    currentState = RTCustomerState.WaitingAtCounter;
                    pickupCoroutine = null;
                    yield break;
                }

                currentState = RTCustomerState.PickingUpItem;
                int currentIndex = itemsReceived;

                DOTween.Kill(item.transform, true);

                if (itemHoldPoint != null)
                {
                    Vector3 targetWorldPos = itemHoldPoint.TransformPoint(itemHoldOffset * currentIndex);
                    item.transform.SetParent(null);

                    bool animDone = false;
                    item.transform.DOJump(targetWorldPos, pickupBounceHeight, 1, pickupBounceDuration)
                        .SetEase(Ease.OutQuad)
                        .OnComplete(() =>
                        {
                            item.transform.SetParent(itemHoldPoint);
                            item.transform.localPosition = itemHoldOffset * currentIndex;
                            item.transform.localRotation = Quaternion.identity;
                            animDone = true;
                        });

                    yield return new WaitUntil(() => animDone);
                }
                else
                {
                    Destroy(item.gameObject);
                }

                heldItems.Add(item);
                itemsReceived++;
                UpdateDemandText();

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(SoundEffect.ItemPickup);

                Debug.Log($"[RTCustomer] Picked up item {itemsReceived}/{itemDemand}");

                if (itemsReceived >= itemDemand) break;

                // Small delay between pickups
                yield return new WaitForSeconds(pickupInterval);
            }

            pickupCoroutine = null;

            if (itemsReceived >= itemDemand)
            {
                OnAllItemsPickedUp();
            }
            else
            {
                currentState = RTCustomerState.WaitingAtCounter;
            }
        }

        private void OnAllItemsPickedUp()
        {
            HideWaitingUI();

            // Remove from counter queue
            targetCounter?.RemoveCustomer(this);

            if (diningArea != null)
            {
                TryFindSeat();
            }
            else
            {
                // No dining area configured - skip to leaving
                StartLeaving();
            }
        }

        #endregion

        #region Dining

        private void TryFindSeat()
        {
            RTDiningSeat seat = diningArea.FindAvailableSeat();
            if (seat != null)
            {
                assignedSeat = seat;
                seat.Reserve(this);
                currentState = RTCustomerState.MovingToTable;

                Vector3 target = seat.ApproachPoint != null
                    ? seat.ApproachPoint.position
                    : seat.SitPoint.position;
                MoveTo(target);

                Debug.Log("[RTCustomer] Moving to dining table");
            }
            else
            {
                currentState = RTCustomerState.WaitingForSeat;
                diningArea.OnSeatBecameAvailable += OnSeatBecameAvailable;

                if (heldItems.Count > 0)
                    SetLiftIdle(true);
                else
                    SetWalking(false);

                Debug.Log("[RTCustomer] No seat available, waiting...");
            }
        }

        private void OnSeatBecameAvailable()
        {
            if (currentState != RTCustomerState.WaitingForSeat) return;

            diningArea.OnSeatBecameAvailable -= OnSeatBecameAvailable;
            TryFindSeat();
        }

        private void OnArrivedAtTable()
        {
            if (agent != null)
                agent.enabled = false;

            // Face the table and tween to exact sit position
            Quaternion seatedRotation = assignedSeat.GetSeatedRotation();
            transform.DORotateQuaternion(seatedRotation, sitDuration * 0.5f);

            if (assignedSeat.SitPoint != null)
            {
                currentState = RTCustomerState.SittingDown;
                transform.DOMove(assignedSeat.SitPoint.position, sitDuration)
                    .OnComplete(StartSitting);
            }
            else
            {
                StartSitting();
            }
        }

        private void StartSitting()
        {
            currentState = RTCustomerState.SittingDown;
            SetSitting(true);

            // After sit animation completes, place items on table
            DOVirtual.DelayedCall(sitDuration, () => StartCoroutine(PlaceItemsOnTableCoroutine()));
        }

        private IEnumerator PlaceItemsOnTableCoroutine()
        {
            RTDiningTable table = assignedSeat?.ParentTable;

            if (table == null || table.ItemPlacementPoint == null || heldItems.Count == 0)
            {
                StartEating();
                yield break;
            }

            Vector3 basePos = table.ItemPlacementPoint.position;
            Vector3 stackOffset = table.ItemStackOffset;

            for (int i = 0; i < heldItems.Count; i++)
            {
                RTFinishedItem item = heldItems[i];
                if (item == null) continue;

                Vector3 savedScale = item.transform.lossyScale;
                DOTween.Kill(item.transform, true);
                item.transform.SetParent(null);
                item.transform.localScale = savedScale;

                Vector3 targetPos = basePos + stackOffset * i;

                item.transform.DOJump(targetPos, itemPlaceJumpHeight, 1, itemPlaceDuration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => item.transform.position = targetPos);

                yield return new WaitForSeconds(itemPlaceInterval);
            }

            // Wait for last placement animation
            yield return new WaitForSeconds(Mathf.Max(0, itemPlaceDuration - itemPlaceInterval));

            StartEating();
        }

        private void StartEating()
        {
            currentState = RTCustomerState.Eating;
            SetEating(true);

            eatingCoroutine = StartCoroutine(EatingCoroutine());
            Debug.Log($"[RTCustomer] Started eating {heldItems.Count} items");
        }

        private IEnumerator EatingCoroutine()
        {
            float totalDuration = assignedSeat?.EatingDuration ?? 5f;
            float interval = heldItems.Count > 0 ? totalDuration / heldItems.Count : totalDuration;
            RTDiningTable table = assignedSeat?.ParentTable;

            // Replace items top to bottom with dirty dishes
            for (int i = heldItems.Count - 1; i >= 0; i--)
            {
                yield return new WaitForSeconds(interval);

                RTFinishedItem item = heldItems[i];
                if (item != null && item.gameObject != null)
                {
                    Vector3 pos = item.transform.position;
                    Quaternion rot = item.transform.rotation;

                    // Shrink food item
                    item.transform.DOScale(Vector3.zero, 0.15f)
                        .SetEase(Ease.InBack)
                        .OnComplete(() =>
                        {
                            if (item != null && item.gameObject != null)
                                Destroy(item.gameObject);
                        });

                    // Spawn dirty dish at same position (pops in from scale 0)
                    if (table != null)
                        table.SpawnDirtyDishAt(pos, rot);
                }
            }

            heldItems.Clear();
            eatingCoroutine = null;
            OnFinishedEating();
        }

        private void OnFinishedEating()
        {
            SetEating(false);
            SetSitting(false);
            SetStandingUp(true);
            currentState = RTCustomerState.StandingUp;

            DOVirtual.DelayedCall(standDuration, OnStoodUp);
            Debug.Log("[RTCustomer] Finished eating, standing up");
        }

        private void OnStoodUp()
        {
            SetStandingUp(false);

            // Tween back to approach point (ground level) before re-enabling NavMesh
            if (assignedSeat != null && assignedSeat.ApproachPoint != null)
            {
                transform.DOMove(assignedSeat.ApproachPoint.position, 0.3f)
                    .OnComplete(() =>
                    {
                        if (agent != null) agent.enabled = true;
                        assignedSeat?.Release();
                        assignedSeat = null;
                        AfterDining();
                    });
            }
            else
            {
                if (agent != null) agent.enabled = true;
                assignedSeat?.Release();
                assignedSeat = null;
                AfterDining();
            }
        }

        private void AfterDining()
        {
            if (targetCashier != null)
                JoinCashierQueue();
            else
                StartLeaving();
        }

        #endregion

        #region Cashier

        private void JoinCashierQueue()
        {
            cashierQueuePosition = targetCashier.AddCustomerToQueue(this);
            if (cashierQueuePosition < 0)
            {
                Debug.LogWarning("[RTCustomer] Cashier queue full, leaving!");
                StartLeaving();
                return;
            }

            hasArrivedAtCashierPosition = false;
            currentState = RTCustomerState.MovingToCashier;
            MoveTo(targetCashier.GetQueueWorldPosition(cashierQueuePosition));
        }

        /// <summary>
        /// Called by RTCashier when queue advances.
        /// </summary>
        public void OnCashierQueuePositionChanged(int newPosition)
        {
            cashierQueuePosition = newPosition;
            hasArrivedAtCashierPosition = false;
            currentState = RTCustomerState.MovingToCashier;
            MoveTo(targetCashier.GetQueueWorldPosition(newPosition));
        }

        private void OnArrivedAtCashier()
        {
            hasArrivedAtCashierPosition = true;
            currentState = RTCustomerState.WaitingAtCashier;
            SetWalking(false);

            // Face the cashier
            if (targetCashier != null)
            {
                Vector3 lookDir = targetCashier.transform.position - transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(lookDir.normalized);
            }

            ShowWaitingUI();

            Debug.Log($"[RTCustomer] Arrived at cashier position {cashierQueuePosition}");
        }

        /// <summary>
        /// Called by RTCashier when the player completes the service radial.
        /// </summary>
        public void OnServedAtCashier()
        {
            HideWaitingUI();
            targetCashier?.RemoveCustomer(this);
            Debug.Log("[RTCustomer] Served at cashier, leaving");
            StartLeaving();
        }

        #endregion

        #region Leaving

        private void StartLeaving()
        {
            currentState = RTCustomerState.Leaving;

            if (heldItems.Count > 0)
                SetLiftCarrying(true);
            else
                SetWalking(true);

            if (exitPoint != null)
            {
                MoveTo(exitPoint.position);
            }
            else
            {
                OnExited();
            }
        }

        private void OnExited()
        {
            Debug.Log("[RTCustomer] Exited restaurant.");

            // Unsubscribe from dining events
            if (diningArea != null)
                diningArea.OnSeatBecameAvailable -= OnSeatBecameAvailable;

            // Release seat if still assigned
            if (assignedSeat != null)
            {
                assignedSeat.Release();
                assignedSeat = null;
            }

            // Notify spawner
            if (spawner != null)
                spawner.OnCustomerExited(this);

            // Destroy all held items
            foreach (var item in heldItems)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            heldItems.Clear();

            Destroy(gameObject);
        }

        #endregion

        #region Movement & Animation

        private void MoveTo(Vector3 destination)
        {
            if (agent == null) return;

            ApplyRewardSpeedMultiplier();
            currentDestination = destination;
            agent.SetDestination(destination);
            isMoving = true;

            if (heldItems.Count > 0)
                SetLiftCarrying(true);
            else
                SetWalking(true);
        }

        private void StopMoving()
        {
            if (agent != null)
                agent.ResetPath();
            isMoving = false;

            if (heldItems.Count > 0)
                SetLiftIdle(true);
            else
                SetWalking(false);
        }

        private void SetWalking(bool walking)
        {
            if (animator == null) return;
            animator.SetBool("IsWalking", walking);
            animator.SetBool("IsLiftWalking", false);
            animator.SetBool("IsLiftIdle", false);
        }

        private void SetLiftCarrying(bool carrying)
        {
            if (animator == null) return;
            if (carrying)
            {
                animator.SetBool("IsLiftWalking", true);
                animator.SetBool("IsLiftIdle", false);
                animator.SetBool("IsWalking", false);
            }
            else
            {
                animator.SetBool("IsLiftWalking", false);
                animator.SetBool("IsLiftIdle", false);
            }
        }

        private void SetLiftIdle(bool liftIdle)
        {
            if (animator == null) return;
            animator.SetBool("IsLiftIdle", liftIdle);
            animator.SetBool("IsLiftWalking", false);
            animator.SetBool("IsWalking", false);
        }

        private void SetSitting(bool sitting)
        {
            if (animator == null) return;
            animator.SetBool("IsSitting", sitting);
            if (sitting)
            {
                animator.SetBool("IsWalking", false);
                animator.SetBool("IsLiftWalking", false);
                animator.SetBool("IsLiftIdle", false);
            }
        }

        private void SetEating(bool eating)
        {
            if (animator == null) return;
            animator.SetBool("IsEating", eating);
        }

        private void SetStandingUp(bool standing)
        {
            if (animator == null) return;
            animator.SetBool("IsStandingUp", standing);
        }

        #endregion

        #region Waiting UI

        private void ShowWaitingUI()
        {
            UpdateDemandText();

            if (waitingUICanvas != null)
                waitingUICanvas.enabled = true;

            if (waitingUI != null)
            {
                waitingUI.gameObject.SetActive(true);
                waitingUI.localScale = Vector3.one * pulseMinScale;

                waitingUITween?.Kill();
                waitingUITween = waitingUI
                    .DOScale(pulseMaxScale, pulseDuration)
                    .SetEase(pulseEase)
                    .SetLoops(-1, LoopType.Yoyo);
            }
        }

        private void HideWaitingUI()
        {
            waitingUITween?.Kill();

            if (waitingUI != null)
                waitingUI.gameObject.SetActive(false);

            if (waitingUICanvas != null)
                waitingUICanvas.enabled = false;
        }

        private void UpdateDemandText()
        {
            if (demandText != null)
                demandText.text = $"{itemsReceived}/{itemDemand}";
        }

        #endregion

        private void OnDestroy()
        {
            waitingUITween?.Kill();
            if (pickupCoroutine != null)
                StopCoroutine(pickupCoroutine);
            if (eatingCoroutine != null)
                StopCoroutine(eatingCoroutine);
            if (diningArea != null)
                diningArea.OnSeatBecameAvailable -= OnSeatBecameAvailable;
            DOTween.Kill(transform);
        }
    }
}
