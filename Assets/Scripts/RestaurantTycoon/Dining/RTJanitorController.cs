using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    public enum RTJanitorState
    {
        Idle,
        MovingToTable,
        CollectingDishes,
        MovingToBin,
        DisposingDishes,
        MovingToIdle
    }

    /// <summary>
    /// AI janitor that monitors dining tables for dirty dishes,
    /// collects them all at once, walks to the garbage bin, and disposes them.
    /// Idles at the nearest idle spot when nothing needs cleaning.
    /// </summary>
    public class RTJanitorController : MonoBehaviour, IUpgradeableStaff
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float arrivalThreshold = 0.6f;
        [SerializeField] private float stoppingDistance = 0.35f;
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float angularSpeed = 720f;
        [Tooltip("When the janitor needs to turn this sharply, slow movement so he doesn't slide past the path.")]
        [SerializeField] private float turnSlowdownAngle = 70f;
        [SerializeField] private float minimumTurnSpeedFactor = 0.35f;

        [Header("Timing")]
        [SerializeField] private float searchInterval = 1f;
        [SerializeField] private float collectDelay = 0.5f;
        [SerializeField] private float disposeDelay = 0.5f;
        [Tooltip("How many dirty tables the janitor cleans per trip before returning to the bin.")]
        [SerializeField] private int maxTablesPerTrip = 1;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [Tooltip("Animator bool parameter name for walking.")]
        [SerializeField] private string walkBoolName = "IsWalking";
        [Tooltip("Animator bool parameter name for lift idle (holding items but not moving).")]
        [SerializeField] private string liftIdleParam = "IsLiftIdle";
        [Tooltip("Animator bool parameter name for lift walking (holding items and moving).")]
        [SerializeField] private string liftWalkParam = "IsLiftWalking";

        [Header("Carry")]
        [Tooltip("Point on the janitor where dishes are held visually.")]
        [SerializeField] private Transform carryPoint;
        [Tooltip("Local-space offset between each stacked dish.")]
        [SerializeField] private Vector3 carryStackOffset = new Vector3(0, 0.12f, 0);

        [Header("References")]
        [SerializeField] private RTDiningArea diningArea;
        [Tooltip("Transform of the garbage bin the janitor walks to.")]
        [SerializeField] private Transform garbageBinTransform;
        [Tooltip("Transforms the janitor walks to when idle. Picked by nearest distance.")]
        [SerializeField] private List<Transform> idleSpots = new List<Transform>();

        // Runtime
        private NavMeshAgent agent;
        private RTJanitorState currentState = RTJanitorState.Idle;
        private RTDiningTable targetTable;
        private List<RTDirtyDish> heldDishes = new List<RTDirtyDish>();
        private float searchTimer;
        private float actionTimer;
        private int tablesCollectedThisTrip;
        private float targetAgentSpeed;
        private float currentRewardSpeedMultiplier = 1f;
        private bool isInitialized;
        private Tween faceTween;

        public RTJanitorState State => currentState;
        public bool IsCarryingDishes => heldDishes.Count > 0;

        #region Unity

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                ConfigureAgent(moveSpeed);
            }

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator != null)
                animator.applyRootMotion = false;

            if (carryPoint == null)
            {
                GameObject cp = new GameObject("JanitorCarryPoint");
                cp.transform.SetParent(transform);
                cp.transform.localPosition = new Vector3(0f, 1.2f, 0.3f);
                carryPoint = cp.transform;
            }
        }

        private void Start()
        {
            if (diningArea == null)
                diningArea = FindObjectOfType<RTDiningArea>();

            if (!isInitialized)
                GoToNearestIdleSpot();
        }

        /// <summary>
        /// Called by RTJanitorUnlock after the janitor is spawned at runtime.
        /// Overrides Inspector-set references so the janitor is fully wired up
        /// without needing them pre-assigned on the prefab.
        /// </summary>
        public void Initialize(RTDiningArea area, Transform binTransform, System.Collections.Generic.List<Transform> spots, float speed)
        {
            diningArea = area;
            garbageBinTransform = binTransform;
            if (spots != null) idleSpots = spots;
            isInitialized = true;
            if (speed > 0f)
            {
                moveSpeed = speed;
                ConfigureAgent(moveSpeed);
            }

            GoToNearestIdleSpot();
        }

        private void Update()
        {
            UpdateSteering();

            switch (currentState)
            {
                case RTJanitorState.Idle:            HandleIdle();           break;
                case RTJanitorState.MovingToTable:   HandleMovingToTable();  break;
                case RTJanitorState.CollectingDishes: HandleCollecting();    break;
                case RTJanitorState.MovingToBin:     HandleMovingToBin();    break;
                case RTJanitorState.DisposingDishes: HandleDisposing();      break;
                case RTJanitorState.MovingToIdle:    HandleMovingToIdle();   break;
            }

            UpdateAnimation();
        }

        // ── IUpgradeableStaff ──────────────────────────────────────────────────────

        /// <summary>Reduces collect and dispose delays.</summary>
        public void SetUpgradedDuration(float newDuration)
        {
            float clamped = Mathf.Max(0.1f, newDuration);
            collectDelay = clamped;
            disposeDelay = clamped;
            Debug.Log($"[RTJanitorController] Janitor delays upgraded to {clamped}s");
        }

        /// <summary>Increases movement speed.</summary>
        public void SetUpgradedSpeed(float newSpeed)
        {
            float clamped = Mathf.Max(0.5f, newSpeed);
            moveSpeed = clamped;
            ConfigureAgent(moveSpeed);
            Debug.Log($"[RTJanitorController] Janitor speed upgraded to {clamped}");
        }

        public void ApplyRewardSpeedMultiplier()
        {
            currentRewardSpeedMultiplier = RTRewardedAdSystem.CharacterSpeedMultiplier;
            ConfigureAgent(moveSpeed);
        }

        /// <summary>Sets how many tables the janitor cleans per trip before returning to the bin.</summary>
        public void SetCarryCapacity(int capacity)
        {
            maxTablesPerTrip = Mathf.Max(1, capacity);
            Debug.Log($"[RTJanitorController] Janitor max tables per trip upgraded to {maxTablesPerTrip}");
        }

        #endregion

        #region State Handlers

        private void HandleIdle()
        {
            searchTimer += Time.deltaTime;
            if (searchTimer < searchInterval) return;
            searchTimer = 0f;

            RTDiningTable dirty = FindNearestDirtyTable();
            if (dirty != null)
                StartMovingToTable(dirty);
        }

        private void HandleMovingToTable()
        {
            // Target became clean while walking (another janitor / player got there first)
            if (targetTable == null || !targetTable.HasDirtyDishes)
            {
                RTDiningTable another = FindNearestDirtyTable();
                if (another != null)
                {
                    targetTable = another;
                    MoveTo(targetTable.transform.position);
                }
                else
                {
                    GoToNearestIdleSpot();
                }
                return;
            }

            if (HasReachedDestination())
            {
                StopMoving();
                currentState = RTJanitorState.CollectingDishes;
                actionTimer = 0f;
                FaceTarget(targetTable.transform.position);
            }
        }

        private void HandleCollecting()
        {
            actionTimer += Time.deltaTime;
            if (actionTimer < collectDelay) return;

            CollectDishesFromTable();
            tablesCollectedThisTrip++;

            // If under capacity, check for another dirty table before heading to the bin.
            if (tablesCollectedThisTrip < maxTablesPerTrip)
            {
                RTDiningTable next = FindNearestDirtyTable();
                if (next != null)
                {
                    StartMovingToTable(next);
                    return;
                }
            }

            if (heldDishes.Count > 0)
            {
                if (garbageBinTransform != null)
                {
                    MoveTo(garbageBinTransform.position);
                    currentState = RTJanitorState.MovingToBin;
                    SetWalking(true);
                }
                else
                {
                    // No bin assigned — dispose in place
                    DisposeAllDishes();
                    GoToNearestIdleSpot();
                    Debug.LogWarning("[RTJanitorController] No garbage bin assigned — disposed dishes in place.");
                }
            }
            else
            {
                GoToNearestIdleSpot();
            }
        }

        private void HandleMovingToBin()
        {
            if (HasReachedDestination())
            {
                StopMoving();
                currentState = RTJanitorState.DisposingDishes;
                actionTimer = 0f;
                FaceTarget(garbageBinTransform.position);
            }
        }

        private void HandleDisposing()
        {
            actionTimer += Time.deltaTime;
            if (actionTimer < disposeDelay) return;

            DisposeAllDishes();
            tablesCollectedThisTrip = 0;

            RTDiningTable next = FindNearestDirtyTable();
            if (next != null)
                StartMovingToTable(next);
            else
                GoToNearestIdleSpot();
        }

        private void HandleMovingToIdle()
        {
            // Interrupt early if a dirty table appears
            searchTimer += Time.deltaTime;
            if (searchTimer >= searchInterval)
            {
                searchTimer = 0f;
                RTDiningTable dirty = FindNearestDirtyTable();
                if (dirty != null)
                {
                    StartMovingToTable(dirty);
                    return;
                }
            }

            if (HasReachedDestination())
            {
                StopMoving();
                currentState = RTJanitorState.Idle;
                searchTimer = 0f;
            }
        }

        #endregion

        #region Actions

        private void StartMovingToTable(RTDiningTable table)
        {
            targetTable = table;
            MoveTo(table.transform.position);
            currentState = RTJanitorState.MovingToTable;
            SetWalking(true);
            Debug.Log($"[RTJanitorController] Moving to dirty table: {table.name}");
        }

        private void CollectDishesFromTable()
        {
            if (targetTable == null) return;

            List<RTDirtyDish> taken = targetTable.TakeAllDirtyDishes();
            int index = 0;
            foreach (var dish in taken)
            {
                if (dish == null) continue;

                DOTween.Kill(dish.transform, true);
                dish.transform.SetParent(carryPoint);
                dish.transform.DOLocalMove(carryStackOffset * index, 0.25f).SetEase(Ease.OutBack);
                dish.transform.DOLocalRotate(Vector3.zero, 0.25f);
                heldDishes.Add(dish);
                index++;
            }

            Debug.Log($"[RTJanitorController] Collected {taken.Count} dish(es). Carrying: {heldDishes.Count}");
        }

        private void DisposeAllDishes()
        {
            foreach (var dish in heldDishes)
            {
                if (dish != null)
                    dish.OnDisposed();
            }
            heldDishes.Clear();
            Debug.Log("[RTJanitorController] All dishes disposed.");
        }

        #endregion

        #region Navigation Helpers

        private RTDiningTable FindNearestDirtyTable()
        {
            if (diningArea == null || diningArea.Tables == null) return null;

            RTDiningTable nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var table in diningArea.Tables)
            {
                if (table == null || !table.HasDirtyDishes) continue;
                float dist = Vector3.Distance(transform.position, table.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = table;
                }
            }

            return nearest;
        }

        private void GoToNearestIdleSpot()
        {
            if (idleSpots.Count == 0)
            {
                currentState = RTJanitorState.Idle;
                SetWalking(false);
                return;
            }

            Transform nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var spot in idleSpots)
            {
                if (spot == null) continue;
                float dist = Vector3.Distance(transform.position, spot.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = spot;
                }
            }

            if (nearest != null)
            {
                MoveTo(nearest.position);
                currentState = RTJanitorState.MovingToIdle;
                SetWalking(true);
            }
            else
            {
                currentState = RTJanitorState.Idle;
                SetWalking(false);
            }
        }

        private void MoveTo(Vector3 destination)
        {
            if (agent == null) return;
            faceTween?.Kill();
            faceTween = null;
            agent.isStopped = false;
            targetAgentSpeed = moveSpeed * currentRewardSpeedMultiplier;
            agent.SetDestination(destination);
        }

        private void StopMoving()
        {
            if (agent != null)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            SetWalking(false);
        }

        private bool HasReachedDestination()
        {
            if (agent == null || agent.pathPending) return false;
            return agent.remainingDistance <= arrivalThreshold;
        }

        private void FaceTarget(Vector3 targetPos)
        {
            Vector3 dir = targetPos - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
            {
                faceTween?.Kill();
                faceTween = transform.DORotateQuaternion(Quaternion.LookRotation(dir), 0.2f);
            }
        }

        private void ConfigureAgent(float speed)
        {
            if (agent == null) return;

            currentRewardSpeedMultiplier = RTRewardedAdSystem.CharacterSpeedMultiplier;
            targetAgentSpeed = speed * currentRewardSpeedMultiplier;
            agent.speed = targetAgentSpeed;
            agent.acceleration = acceleration;
            agent.angularSpeed = angularSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.autoBraking = true;
            agent.updateRotation = false;
        }

        private void UpdateSteering()
        {
            if (agent == null || agent.isStopped || !agent.hasPath)
                return;

            Vector3 desiredVelocity = agent.desiredVelocity;
            desiredVelocity.y = 0f;

            if (desiredVelocity.sqrMagnitude < 0.001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                angularSpeed * Time.deltaTime);

            float turnAngle = Vector3.Angle(transform.forward, desiredVelocity.normalized);
            float turnT = Mathf.InverseLerp(0f, Mathf.Max(1f, turnSlowdownAngle), turnAngle);
            float speedFactor = Mathf.Lerp(1f, minimumTurnSpeedFactor, turnT);
            targetAgentSpeed = moveSpeed * currentRewardSpeedMultiplier * speedFactor;
            agent.speed = Mathf.MoveTowards(agent.speed, targetAgentSpeed, acceleration * Time.deltaTime);
        }

        private void SetWalking(bool walking)
        {
            if (animator == null) return;

            if (IsCarryingDishes)
            {
                animator.SetBool(walkBoolName, false);
                animator.SetBool(liftIdleParam, !walking);
                animator.SetBool(liftWalkParam, walking);
            }
            else
            {
                animator.SetBool(walkBoolName, walking);
                animator.SetBool(liftIdleParam, false);
                animator.SetBool(liftWalkParam, false);
            }
        }

        private void UpdateAnimation()
        {
            if (animator == null) return;

            if (IsCarryingDishes)
            {
                // Determine if moving based on the current state
                bool isMoving = currentState == RTJanitorState.MovingToTable ||
                               currentState == RTJanitorState.MovingToBin ||
                               currentState == RTJanitorState.MovingToIdle;

                if (isMoving)
                {
                    animator.SetBool(walkBoolName, false);
                    animator.SetBool(liftIdleParam, false);
                    animator.SetBool(liftWalkParam, true);
                }
                else
                {
                    animator.SetBool(walkBoolName, false);
                    animator.SetBool(liftIdleParam, true);
                    animator.SetBool(liftWalkParam, false);
                }
            }
            else
            {
                animator.SetBool(liftIdleParam, false);
                animator.SetBool(liftWalkParam, false);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (targetTable != null)
            {
                Gizmos.DrawLine(transform.position, targetTable.transform.position);
                Gizmos.DrawWireSphere(targetTable.transform.position, 0.4f);
            }

            if (garbageBinTransform != null)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(transform.position, garbageBinTransform.position);
                Gizmos.DrawWireSphere(garbageBinTransform.position, 0.3f);
            }

            Gizmos.color = Color.green;
            foreach (var spot in idleSpots)
                if (spot != null) Gizmos.DrawWireSphere(spot.position, 0.25f);
        }

        #endregion
    }
}
