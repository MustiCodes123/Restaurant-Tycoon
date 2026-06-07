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

        [Header("Carry")]
        [Tooltip("Point on the porter where the held ingredient is shown.")]
        [SerializeField] private Transform carryPoint;

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
        private RTIngredient heldIngredient;
        private float searchTimer;
        private float actionTimer;

        public RTPorterState State => currentState;
        public bool IsCarryingIngredient => heldIngredient != null;

        /// <summary>Reduces collect and deliver delays. Called by RTStaffUpgrade when an upgrade is purchased.</summary>
        public void SetUpgradedDuration(float newDuration)
        {
            float clamped = Mathf.Max(0.1f, newDuration);
            collectDelay = clamped;
            deliverDelay = clamped;
            Debug.Log($"[RTPorterController] Porter delays upgraded to {clamped}s");
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
        }

        #endregion

        #region State Handlers

        private void HandleIdle()
        {
            // If holding an ingredient from an aborted delivery, re-attempt as soon
            // as the input container has space — don't collect a second ingredient.
            if (heldIngredient != null)
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

            TakeIngredientFromContainer();

            if (heldIngredient != null)
            {
                MoveTo(inputContainer.transform.position);
                currentState = RTPorterState.MovingToInputContainer;
                SetWalking(true);
            }
            else
            {
                // Nothing available yet
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
            // Re-deliver a held ingredient as soon as the input has space,
            // rather than finishing the idle walk first.
            if (heldIngredient != null && inputContainer != null && !inputContainer.IsFull)
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

            RTIngredient ingredient = ingredientContainer.TakeTopIngredient();
            if (ingredient == null) return;

            heldIngredient = ingredient;

            // Parent to carry point and animate into hand
            DOTween.Kill(heldIngredient.transform, true);
            heldIngredient.transform.SetParent(carryPoint);

            heldIngredient.transform
                .DOLocalMove(Vector3.zero, collectDelay * 0.5f)
                .SetEase(Ease.OutQuad);
            heldIngredient.transform
                .DOLocalRotate(Vector3.zero, collectDelay * 0.5f);
        }

        private void DeliverIngredientToInput()
        {
            if (heldIngredient == null || inputContainer == null) return;

            // Unparent before dropping into input container
            DOTween.Kill(heldIngredient.transform, true);
            heldIngredient.transform.SetParent(null);

            inputContainer.ReceiveIngredient(heldIngredient);
            heldIngredient = null;
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

        #endregion
    }
}
