using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Container that auto-stocks ingredients. Ingredients animate from a source point
    /// (e.g., delivery van) to slots on the container when empty.
    /// When the player enters the trigger, ingredients transfer to the player's carry stack
    /// one at a time (top-first) with a delay between each.
    /// </summary>
    public class RTIngredientContainer : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject ingredientPrefab;

        [Header("Stock Slots")]
        [Tooltip("Points where ingredients are displayed on the container. First is bottom, last is top.")]
        [SerializeField] private List<Transform> stockSlots = new List<Transform>();

        [Header("Source Point")]
        [Tooltip("Where ingredients animate FROM (e.g., a delivery van). If null, uses container position.")]
        [SerializeField] private Transform sourcePoint;

        [Header("Auto Restock")]
        [SerializeField] private float restockDelay = 1.5f;
        [Tooltip("Delay between each individual ingredient arriving at a slot")]
        [SerializeField] private float arrivalInterval = 0.3f;
        [SerializeField] private float arrivalAnimDuration = 0.5f;

        [Header("Player Pickup")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private float pickupInterval = 0.3f;

        // Current ingredients on the container (index matches stockSlots)
        private RTIngredient[] stockedIngredients;
        private bool playerInRange = false;
        private RTPlayerCarryController playerCarryController;
        private Transform playerTransform;
        private Coroutine pickupCoroutine;
        private Coroutine restockCoroutine;
        private bool isRestocking = false;

        public int SlotCount => stockSlots.Count;
        public int StockedCount
        {
            get
            {
                int count = 0;
                if (stockedIngredients != null)
                {
                    foreach (var item in stockedIngredients)
                    {
                        if (item != null) count++;
                    }
                }
                return count;
            }
        }
        public bool IsFull => StockedCount >= stockSlots.Count;

        private void Start()
        {
            stockedIngredients = new RTIngredient[stockSlots.Count];

            // Find player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerCarryController = player.GetComponent<RTPlayerCarryController>();
                if (playerCarryController == null)
                    playerCarryController = player.GetComponentInChildren<RTPlayerCarryController>();
            }

            // Start initial production. OnEnable handles re-activation after unlock.
            StartRestock();
        }

        private void OnEnable()
        {
            // Restart production whenever this container is re-enabled (e.g. unlocked mid-game).
            // OnEnable fires BEFORE Start on first activation, so guard until the array exists.
            if (stockedIngredients == null) return;
            StartRestock();
        }

        #region Auto Restock

        /// <summary>
        /// Start restocking empty slots. Ingredients animate from sourcePoint to each empty slot.
        /// </summary>
        private void StartRestock()
        {
            if (isRestocking) return;

            if (restockCoroutine != null)
                StopCoroutine(restockCoroutine);

            restockCoroutine = StartCoroutine(RestockCoroutine());
        }

        private IEnumerator RestockCoroutine()
        {
            isRestocking = true;

            // Small delay before restocking begins
            yield return new WaitForSeconds(restockDelay);

            for (int i = 0; i < stockSlots.Count; i++)
            {
                // Only fill empty slots
                if (stockedIngredients[i] != null) continue;

                SpawnIngredientAtSlot(i);

                yield return new WaitForSeconds(arrivalInterval);
            }

            isRestocking = false;
            restockCoroutine = null;
        }

        private void SpawnIngredientAtSlot(int slotIndex)
        {
            if (ingredientPrefab == null || slotIndex < 0 || slotIndex >= stockSlots.Count) return;

            Transform slot = stockSlots[slotIndex];
            Vector3 fromPos = sourcePoint != null ? sourcePoint.position : transform.position + Vector3.up * 2f;

            GameObject obj = Instantiate(ingredientPrefab, fromPos, Quaternion.identity);
            RTIngredient ingredient = obj.GetComponent<RTIngredient>();

            if (ingredient == null)
            {
                Debug.LogError("[RTIngredientContainer] Prefab is missing RTIngredient component!");
                Destroy(obj);
                return;
            }

            stockedIngredients[slotIndex] = ingredient;

            // Animate from source to slot
            ingredient.AnimateToSpot(fromPos, slot, arrivalAnimDuration, () =>
            {
                // Snap to slot after animation
                obj.transform.position = slot.position;
                obj.transform.rotation = slot.rotation;
            });
        }

        #endregion

        #region Player Pickup

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[RTIngredientContainer] OnTriggerEnter: {other.gameObject.name}, Layer: {other.gameObject.layer}, PlayerLayer mask: {playerLayer.value}");

            if (((1 << other.gameObject.layer) & playerLayer) == 0)
            {
                Debug.Log($"[RTIngredientContainer] Layer mismatch. Object layer bit: {1 << other.gameObject.layer}, mask: {playerLayer.value}");
                return;
            }

            playerInRange = true;

            if (playerCarryController == null)
            {
                playerCarryController = other.GetComponent<RTPlayerCarryController>();
                if (playerCarryController == null)
                    playerCarryController = other.GetComponentInParent<RTPlayerCarryController>();
            }

            if (playerCarryController != null)
            {
                Debug.Log($"[RTIngredientContainer] Player detected. CanCarryMore: {playerCarryController.CanCarryMore}, CarryPoints: {playerCarryController.MaxCarryCount}, Stocked: {StockedCount}");
                StartPickup();
            }
            else
            {
                Debug.LogWarning("[RTIngredientContainer] Player entered but RTPlayerCarryController not found!");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            playerInRange = false;
            StopPickup();
        }

        private void StartPickup()
        {
            if (pickupCoroutine != null) return;
            pickupCoroutine = StartCoroutine(PickupCoroutine());
        }

        private void StopPickup()
        {
            if (pickupCoroutine != null)
            {
                StopCoroutine(pickupCoroutine);
                pickupCoroutine = null;
            }
        }

        private IEnumerator PickupCoroutine()
        {
            // Small initial delay
            yield return new WaitForSeconds(pickupInterval);

            while (playerInRange && playerCarryController != null)
            {
                if (!playerCarryController.CanCarryMore)
                {
                    // Player full, wait and check again
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                // Find the topmost stocked ingredient (highest slot index)
                RTIngredient topIngredient = null;
                int topIndex = -1;
                for (int i = stockSlots.Count - 1; i >= 0; i--)
                {
                    if (stockedIngredients[i] != null)
                    {
                        topIngredient = stockedIngredients[i];
                        topIndex = i;
                        break;
                    }
                }

                if (topIngredient == null)
                {
                    // No ingredients available, trigger restock and wait
                    if (!isRestocking) StartRestock();
                    yield return new WaitForSeconds(0.3f);
                    continue;
                }

                // Complete any running spawn/restock tweens (snaps to intended final scale)
                DOTween.Kill(topIngredient.transform, true);

                // Remove from container
                stockedIngredients[topIndex] = null;

                // Hand to player carry system
                if (playerCarryController.TryPickup(topIngredient))
                {
                    Debug.Log($"[RTIngredientContainer] Player picked up ingredient from slot {topIndex}. Container: {StockedCount}/{SlotCount}");
                }
                else
                {
                    // Failed to pick up, put it back
                    stockedIngredients[topIndex] = topIngredient;
                }

                // Trigger restock if we have empty slots
                if (!IsFull && !isRestocking)
                {
                    StartRestock();
                }

                yield return new WaitForSeconds(pickupInterval);
            }

            pickupCoroutine = null;
        }

        #endregion

        #region Porter Access

        /// <summary>
        /// Called by RTPorterController to take the topmost available ingredient.
        /// Triggers a restock after taking. Returns null if empty.
        /// </summary>
        public RTIngredient TakeTopIngredient()
        {
            for (int i = stockSlots.Count - 1; i >= 0; i--)
            {
                if (stockedIngredients[i] != null)
                {
                    RTIngredient ingredient = stockedIngredients[i];
                    DOTween.Kill(ingredient.transform, true);
                    stockedIngredients[i] = null;

                    if (!IsFull && !isRestocking)
                        StartRestock();

                    Debug.Log($"[RTIngredientContainer] Porter took ingredient from slot {i}. Remaining: {StockedCount}/{SlotCount}");
                    return ingredient;
                }
            }
            return null;
        }

        #endregion
    }
}
