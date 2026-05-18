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
    public class RTJanitorController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float arrivalThreshold = 0.6f;

        [Header("Timing")]
        [SerializeField] private float searchInterval = 1f;
        [SerializeField] private float collectDelay = 0.5f;
        [SerializeField] private float disposeDelay = 0.5f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [Tooltip("Animator bool parameter name for walking.")]
        [SerializeField] private string walkBoolName = "IsWalking";

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

        public RTJanitorState State => currentState;
        public bool IsCarryingDishes => heldDishes.Count > 0;

        #region Unity

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed = moveSpeed;
                agent.stoppingDistance = 0.3f;
            }

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

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
            if (speed > 0f && agent != null) agent.speed = speed;

            GoToNearestIdleSpot();
        }

        private void Update()
        {
            switch (currentState)
            {
                case RTJanitorState.Idle:            HandleIdle();           break;
                case RTJanitorState.MovingToTable:   HandleMovingToTable();  break;
                case RTJanitorState.CollectingDishes: HandleCollecting();    break;
                case RTJanitorState.MovingToBin:     HandleMovingToBin();    break;
                case RTJanitorState.DisposingDishes: HandleDisposing();      break;
                case RTJanitorState.MovingToIdle:    HandleMovingToIdle();   break;
            }
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
            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        private void StopMoving()
        {
            if (agent != null)
                agent.isStopped = true;
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
                transform.DORotateQuaternion(Quaternion.LookRotation(dir), 0.2f);
        }

        private void SetWalking(bool walking)
        {
            if (animator != null)
                animator.SetBool(walkBoolName, walking);
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
