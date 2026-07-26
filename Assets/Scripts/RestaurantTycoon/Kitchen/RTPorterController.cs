using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    public enum RTPorterState
    {
        Idle,
        MovingToIngredientContainer,
        CollectingIngredient,
        MovingToInputContainer,
        DeliveringIngredient,
        MovingToIdle
    }

    /// <summary>
    /// AI porter that picks up a specific ingredient type from an RTIngredientContainer
    /// and delivers it to an RTCookInputContainer.
    /// Idles at the nearest idle spot when the source container is empty or the input is full.
    /// </summary>
    public class RTPorterController : MonoBehaviour, IUpgradeableStaff
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float arrivalThreshold = 0.6f;

        [Header("Timing")]
        [SerializeField] private float searchInterval = 1f;
        [SerializeField] private float collectDelay = 0.5f;
        [SerializeField] private float deliverDelay = 0.5f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [Tooltip("Animator bool parameter name for walking.")]
        [SerializeField] private string walkBoolName = "IsWalking";
        [Tooltip("Animator bool parameter name for lift idle (holding items but not moving).")]
        [SerializeField] private string liftIdleParam = "IsLiftIdle";
        [Tooltip("Animator bool parameter name for lift walking (holding items and moving).")]
        [SerializeField] private string liftWalkParam = "IsLiftWalking";

        [Header("Carry")]
        [Tooltip("Point on the porter where the held ingredient is shown.")]
        [SerializeField] private Transform carryPoint;
        [Tooltip("Maximum number of ingredients the porter carries per trip.")]
        [SerializeField] private int carryCapacity = 1;
        [Tooltip("Local-space offset between each stacked ingredient.")]
        [SerializeField] private Vector3 carryStackOffset = new Vector3(0f, 0.1f, 0f);

        [Header("References")]
        [Tooltip("The ingredient container this porter sources ingredients from.")]
        [SerializeField] private RTIngredientContainer ingredientContainer;

        [Tooltip("The cook input container this porter delivers ingredients to.")]
        [SerializeField] private RTCookInputContainer inputContainer;

        [Tooltip("Transforms the porter walks to when idle. Picked by nearest distance.")]
        [SerializeField] private List<Transform> idleSpots = new List<Transform>();

        // ── Runtime ───────────────────────────────────────────────────────────
        private NavMeshAgent agent;
        private RTPorterState currentState = RTPorterState.Idle;
        private List<RTIngredient> heldIngredients = new List<RTIngredient>();
        private float searchTimer;
        private float actionTimer;

        public RTPorterState State => currentState;
        public bool IsCarryingIngredient => heldIngredients.Count > 0;

        /// <summary>Reduces collect and deliver delays.</summary>
        public void SetUpgradedDuration(float newDuration)
        {
            float clamped = Mathf.Max(0.1f, newDuration);
            collectDelay = clamped;
            deliverDelay = clamped;
            Debug.Log($"[RTPorterController] Porter delays upgraded to {clamped}s");
        }

        /// <summary>Increases movement speed.</summary>
        public void SetUpgradedSpeed(float newSpeed)
        {
            float clamped = Mathf.Max(0.5f, newSpeed);
            moveSpeed = clamped;
            if (agent != null) agent.speed = clamped;
            Debug.Log($"[RTPorterController] Porter speed upgraded to {clamped}");
        }

        /// <summary>Sets how many ingredients the porter carries per trip.</summary>
        public void SetCarryCapacity(int capacity)
        {
            carryCapacity = Mathf.Max(1, capacity);
            Debug.Log($"[RTPorterController] Porter carry capacity upgraded to {carryCapacity}");
        }

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

            // NavMeshAgent drives all movement — root motion must be off or it fights the agent.
            if (animator != null)
                animator.applyRootMotion = false;

            if (carryPoint == null)
            {
                GameObject cp = new GameObject("PorterCarryPoint");
                cp.transform.SetParent(transform);
                cp.transform.localPosition = new Vector3(0f, 1.2f, 0.3f);
                carryPoint = cp.transform;
            }
        }

        private void OnEnable()
        {
            StartCoroutine(InitializeAfterFrame());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            currentState = RTPorterState.Idle;
            SetWalking(false);
        }

        private IEnumerator InitializeAfterFrame()
        {
            // Wait one frame so Unity's activation cycle completes.
            yield return null;

            // Safety: re-enable the agent if a previous coroutine was interrupted
            // mid-toggle and left it disabled.
            if (agent != null && !agent.enabled)
                agent.enabled = true;

            // If the agent is not yet on the NavMesh, cycle its enabled state to force
            // re-registration. agent.isOnNavMesh does not reliably become true on
            // SetActive(true) alone in all Unity versions; the toggle is the safest fix.
            if (agent != null && !agent.isOnNavMesh)
            {
                agent.enabled = false;
                yield return null;
                agent.enabled = true;
                yield return null;
            }

            // Final fallback: warp to the nearest valid NavMesh position.
            if (agent != null && !agent.isOnNavMesh)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 10f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    yield return null;
                }
            }

            if (agent == null || !agent.isOnNavMesh)
            {
                Debug.LogWarning($"[RTPorterController] NavMeshAgent not on NavMesh after initialization — {gameObject.name} will stay idle.");
                SetWalking(false);
                currentState = RTPorterState.Idle;
                yield break;
            }

            agent.ResetPath();
            agent.velocity = Vector3.zero;
            currentState = RTPorterState.Idle;
            SetWalking(false);
            searchTimer = 0f;
            GoToNearestIdleSpot();
        }

        private void Start() { /* initialization handled in OnEnable coroutine */ }

        private void Update()
        {

            switch (currentState)
            {
                case RTPorterState.Idle:                        HandleIdle();                   break;
                case RTPorterState.MovingToIngredientContainer: HandleMovingToSource();         break;
                case RTPorterState.CollectingIngredient:        HandleCollecting();             break;
                case RTPorterState.MovingToInputContainer:      HandleMovingToInput();          break;
                case RTPorterState.DeliveringIngredient:        HandleDelivering();             break;
                case RTPorterState.MovingToIdle:                HandleMovingToIdle();           break;
            }

            UpdateAnimation();
        }

        #endregion

        #region State Handlers

        private void HandleIdle()
        {
            // If holding ingredients from an aborted delivery, re-attempt as soon
            // as the input container has space — don't collect more.
            if (heldIngredients.Count > 0)
            {
                if (inputContainer != null && !inputContainer.IsFull)
                {
                    MoveTo(inputContainer.transform.position);
                    currentState = RTPorterState.MovingToInputContainer;
                    SetWalking(true);
                }
                return;
            }

            searchTimer += Time.deltaTime;
            if (searchTimer < searchInterval) return;
            searchTimer = 0f;

            if (ShouldFetchIngredient())
                StartMovingToSource();
        }

        private void HandleMovingToSource()
        {
            if (!ShouldFetchIngredient())
            {
                GoToNearestIdleSpot();
                return;
            }

            if (HasReachedDestination())
            {
                StopMoving();
                currentState = RTPorterState.CollectingIngredient;
                actionTimer = 0f;
                FaceTarget(ingredientContainer.transform.position);
            }
        }

        private void HandleCollecting()
        {
            actionTimer += Time.deltaTime;
            if (actionTimer < collectDelay) return;
            actionTimer = 0f;

            int countBefore = heldIngredients.Count;
            TakeIngredientFromContainer();
            bool pickedUp = heldIngredients.Count > countBefore;

            // Stay at the container if: a pickup just happened AND we haven't hit
            // capacity yet AND the source still has items to take.
            // (We deliberately don't block on inputContainer.IsFull here —
            // that's only checked inside TakeIngredientFromContainer itself.)
            bool canTakeMore = pickedUp &&
                               heldIngredients.Count < carryCapacity &&
                               ingredientContainer != null &&
                               ingredientContainer.StockedCount > 0;

            Debug.Log($"[RTPorterController] Collect tick: held={heldIngredients.Count}/{carryCapacity} " +
                      $"pickedUp={pickedUp} sourceStock={ingredientContainer?.StockedCount} " +
                      $"inputFull={inputContainer?.IsFull} canTakeMore={canTakeMore}");

            if (canTakeMore)
                return; // wait another collectDelay tick at the container

            // Finished collecting — head to the input container or idle.
            if (heldIngredients.Count > 0)
            {
                MoveTo(inputContainer.transform.position);
                currentState = RTPorterState.MovingToInputContainer;
                SetWalking(true);
            }
            else
            {
                GoToNearestIdleSpot();
            }
        }

        private void HandleMovingToInput()
        {
            // Drop off destination became full while walking
            if (inputContainer.IsFull)
            {
                GoToNearestIdleSpot();
                return;
            }

            if (HasReachedDestination())
            {
                StopMoving();
                currentState = RTPorterState.DeliveringIngredient;
                actionTimer = 0f;
                FaceTarget(inputContainer.transform.position);
            }
        }

        private void HandleDelivering()
        {
            actionTimer += Time.deltaTime;
            if (actionTimer < deliverDelay) return;

            DeliverIngredientToInput();

            // Decide next action
            if (ShouldFetchIngredient())
                StartMovingToSource();
            else
                GoToNearestIdleSpot();
        }

        private void HandleMovingToIdle()
        {
            // Re-deliver held ingredients as soon as the input has space,
            // rather than finishing the idle walk first.
            if (heldIngredients.Count > 0 && inputContainer != null && !inputContainer.IsFull)
            {
                MoveTo(inputContainer.transform.position);
                currentState = RTPorterState.MovingToInputContainer;
                SetWalking(true);
                return;
            }

            // Interrupt early if work is available
            searchTimer += Time.deltaTime;
            if (searchTimer >= searchInterval)
            {
                searchTimer = 0f;
                if (ShouldFetchIngredient())
                {
                    StartMovingToSource();
                    return;
                }
            }

            if (HasReachedDestination())
            {
                StopMoving();
                currentState = RTPorterState.Idle;
                searchTimer = 0f;
            }
        }

        #endregion

        #region Actions

        private bool ShouldFetchIngredient()
        {
            bool result = ingredientContainer != null && inputContainer != null &&
                          ingredientContainer.StockedCount > 0 &&
                          !inputContainer.IsFull;
            // Uncomment to spam-log every check (very verbose — enable only when debugging):
            // Debug.Log($"[RTPorterController] ShouldFetch={result}  ic={ingredientContainer?.name ?? "NULL"}  stock={ingredientContainer?.StockedCount}  input={inputContainer?.name ?? "NULL"}  full={inputContainer?.IsFull}");
            return result;
        }

        private void StartMovingToSource()
        {
            if (ingredientContainer == null) return;
            MoveTo(ingredientContainer.transform.position);
            currentState = RTPorterState.MovingToIngredientContainer;
            SetWalking(true);
        }

        private void TakeIngredientFromContainer()
        {
            if (ingredientContainer == null || ingredientContainer.StockedCount == 0) return;
            if (inputContainer == null || inputContainer.IsFull) return;
            if (heldIngredients.Count >= carryCapacity) return;

            RTIngredient ingredient = ingredientContainer.TakeTopIngredient();
            if (ingredient == null) return;

            DOTween.Kill(ingredient.transform, true);
            ingredient.transform.SetParent(carryPoint);
            ingredient.transform
                .DOLocalMove(carryStackOffset * heldIngredients.Count, collectDelay * 0.5f)
                .SetEase(Ease.OutQuad);
            ingredient.transform.DOLocalRotate(Vector3.zero, collectDelay * 0.5f);
            heldIngredients.Add(ingredient);
        }

        private void DeliverIngredientToInput()
        {
            if (heldIngredients.Count == 0 || inputContainer == null) return;

            var toDeliver = new List<RTIngredient>(heldIngredients);
            heldIngredients.Clear();

            foreach (var ingredient in toDeliver)
            {
                if (ingredient == null) continue;
                if (inputContainer.IsFull)
                {
                    // Container filled up mid-delivery — hold the rest for next attempt.
                    heldIngredients.Add(ingredient);
                    continue;
                }
                DOTween.Kill(ingredient.transform, true);
                ingredient.transform.SetParent(null);
                inputContainer.ReceiveIngredient(ingredient);
            }
        }

        private void GoToNearestIdleSpot()
        {
            if (idleSpots == null || idleSpots.Count == 0)
            {
                StopMoving();
                currentState = RTPorterState.Idle;
                return;
            }

            Transform nearest = null;
            float nearest_dist = float.MaxValue;
            foreach (var spot in idleSpots)
            {
                if (spot == null) continue;
                float d = Vector3.Distance(transform.position, spot.position);
                if (d < nearest_dist) { nearest_dist = d; nearest = spot; }
            }

            if (nearest != null)
            {
                MoveTo(nearest.position);
                currentState = RTPorterState.MovingToIdle;
                SetWalking(true);
            }
            else
            {
                StopMoving();
                currentState = RTPorterState.Idle;
            }

            searchTimer = 0f;
        }

        #endregion

        #region Navigation Helpers

        private void MoveTo(Vector3 destination)
        {
            if (agent != null && agent.isOnNavMesh)
                agent.SetDestination(destination);
        }

        private void StopMoving()
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            SetWalking(false);
        }

        private bool HasReachedDestination()
        {
            if (agent == null || !agent.isOnNavMesh) return false;
            if (agent.pathPending) return false;
            return agent.remainingDistance <= arrivalThreshold;
        }

        private void FaceTarget(Vector3 target)
        {
            Vector3 dir = (target - transform.position).normalized;
            dir.y = 0f;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        private void SetWalking(bool walking)
        {
            if (animator != null)
                animator.SetBool(walkBoolName, walking);
        }

        private void UpdateAnimation()
        {
            if (animator == null) return;

            if (IsCarryingIngredient)
            {
                // Determine if moving based on the current state
                bool isMoving = currentState == RTPorterState.MovingToIngredientContainer ||
                               currentState == RTPorterState.MovingToInputContainer ||
                               currentState == RTPorterState.MovingToIdle ||
                               currentState == RTPorterState.CollectingIngredient;

                if (isMoving)
                {
                    animator.SetBool(liftIdleParam, false);
                    animator.SetBool(liftWalkParam, true);
                }
                else
                {
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
    }
}
