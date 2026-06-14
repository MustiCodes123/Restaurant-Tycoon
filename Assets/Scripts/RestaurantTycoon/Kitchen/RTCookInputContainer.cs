using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Container next to the cook where the player drops ingredients.
    /// When player enters trigger and top-of-stack is Ingredient, ingredients
    /// are removed one at a time and placed into slots.
    /// The cook watches this container and takes ingredients from here.
    /// </summary>
    public class RTCookInputContainer : MonoBehaviour
    {
        [Header("Ingredient Filter")]
        [Tooltip("Only ingredients of this type will be accepted. Leave empty to accept any ingredient.")]
        [SerializeField] private RTIngredientType acceptedIngredientType;

        [Header("Slots")]
        [Tooltip("The base slot position where ingredients will stack.")]
        [SerializeField] private Transform slotReference;
        [Tooltip("Gap/distance between each stacked ingredient in the queue.")]
        [SerializeField] private float gapBetweenItems = 0.3f;

        [Header("Player Detection")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private float dropInterval = 0.3f;

        [Header("Drop Animation")]
        [SerializeField] private float dropJumpHeight = 0.5f;
        [SerializeField] private float dropDuration = 0.25f;

        [Header("Cooking Animation")]
        [SerializeField] private GameObject shakeTarget;

        private const int MAX_SLOTS = 6;
        private RTIngredient[] storedIngredients;
        private bool playerInRange = false;
        private RTPlayerCarryController playerCarryController;
        private Coroutine dropCoroutine;

        private Tween cookingShakeTween;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 shakeTargetOriginalPosition;
        private Quaternion shakeTargetOriginalRotation;

        /// <summary>
        /// Fired when a new ingredient is added to the container.
        /// </summary>
        public event Action OnIngredientAdded;

        public int SlotCount => MAX_SLOTS;

        public int StoredCount
        {
            get
            {
                int count = 0;
                if (storedIngredients != null)
                {
                    foreach (var item in storedIngredients)
                    {
                        if (item != null) count++;
                    }
                }
                return count;
            }
        }

        public bool IsFull => StoredCount >= MAX_SLOTS;
        public bool HasIngredient => StoredCount > 0;

        private void Start()
        {
            storedIngredients = new RTIngredient[MAX_SLOTS];
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;

            // Store original position and rotation for shake target if assigned
            if (shakeTarget != null)
            {
                shakeTargetOriginalPosition = shakeTarget.transform.localPosition;
                shakeTargetOriginalRotation = shakeTarget.transform.localRotation;
            }

            // Find player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerCarryController = player.GetComponent<RTPlayerCarryController>();
                if (playerCarryController == null)
                    playerCarryController = player.GetComponentInChildren<RTPlayerCarryController>();
            }
        }

        /// <summary>
        /// Calculate the position for an ingredient at the given stack index.
        /// </summary>
        private Vector3 GetStackPosition(int stackIndex)
        {
            if (slotReference == null)
            {
                Debug.LogError("[RTCookInputContainer] Slot reference is not assigned!");
                return Vector3.zero;
            }
            return slotReference.position + Vector3.up * (gapBetweenItems * stackIndex);
        }

        #region Player Drop

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            playerInRange = true;

            if (playerCarryController == null)
            {
                playerCarryController = other.GetComponent<RTPlayerCarryController>();
                if (playerCarryController == null)
                    playerCarryController = other.GetComponentInParent<RTPlayerCarryController>();
            }

            if (playerCarryController != null)
            {
                StartDropping();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            playerInRange = false;
            StopDropping();
        }

        private void StartDropping()
        {
            if (dropCoroutine != null) return;
            dropCoroutine = StartCoroutine(DropCoroutine());
        }

        private void StopDropping()
        {
            if (dropCoroutine != null)
            {
                StopCoroutine(dropCoroutine);
                dropCoroutine = null;
            }
        }

        private IEnumerator DropCoroutine()
        {
            yield return new WaitForSeconds(dropInterval);

            while (playerInRange && playerCarryController != null)
            {
                // Only accept if container has room
                if (IsFull)
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                // Build a predicate: must be an Ingredient, and match the accepted subtype if one is set.
                // Searching top-down means a buried matching ingredient is found even if a different
                // ingredient type (or other item type) is sitting above it.
                System.Func<IRTCarryable, bool> match = acceptedIngredientType != null
                    ? (IRTCarryable i) => i.CarryType == CarryableType.Ingredient &&
                                         i.GameObject.GetComponent<RTIngredient>()?.IngredientType == acceptedIngredientType
                    : (IRTCarryable i) => i.CarryType == CarryableType.Ingredient;

                if (playerCarryController.PeekTopItem(match) == null)
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                // Take the first matching ingredient from the player (may not be the absolute top)
                IRTCarryable item = playerCarryController.TakeTopItem(match);
                if (item == null)
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                RTIngredient ingredient = item.GameObject.GetComponent<RTIngredient>();
                if (ingredient == null)
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                // Find first empty slot
                int slotIndex = GetFirstEmptySlot();
                if (slotIndex < 0)
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                // Place in slot with bounce animation
                storedIngredients[slotIndex] = ingredient;
                Vector3 targetPosition = GetStackPosition(slotIndex);

                // Save the world-space scale before unparenting
                Vector3 savedScale = ingredient.transform.lossyScale;

                // Complete any active tweens to their final values
                DOTween.Kill(ingredient.transform, true);

                ingredient.transform.SetParent(null);
                ingredient.transform.localScale = savedScale;

                ingredient.transform.DOJump(targetPosition, dropJumpHeight, 1, dropDuration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        ingredient.transform.position = targetPosition;
                        if (slotReference != null)
                            ingredient.transform.rotation = slotReference.rotation;
                    });

                Debug.Log($"[RTCookInputContainer] (ID:{GetInstanceID()}) Ingredient dropped into slot {slotIndex}. Stored: {StoredCount}/{SlotCount}");

                Debug.Log($"[RTCookInputContainer] (ID:{GetInstanceID()}) Firing OnIngredientAdded event. Listeners: {(OnIngredientAdded != null ? OnIngredientAdded.GetInvocationList().Length.ToString() : "0")}");
                OnIngredientAdded?.Invoke();

                yield return new WaitForSeconds(dropInterval);
            }

            dropCoroutine = null;
        }

        #endregion

        #region Cook Access

        /// <summary>
        /// Called by the cook to take the bottom ingredient (FIFO).
        /// Returns null if no ingredients available.
        /// </summary>
        public RTIngredient TakeIngredient()
        {
            for (int i = 0; i < storedIngredients.Length; i++)
            {
                if (storedIngredients[i] != null)
                {
                    RTIngredient ingredient = storedIngredients[i];
                    storedIngredients[i] = null;
                    
                    // Shift all ingredients above down by one position
                    for (int j = i; j < storedIngredients.Length - 1; j++)
                    {
                        storedIngredients[j] = storedIngredients[j + 1];
                        if (storedIngredients[j] != null)
                        {
                            // Animate the ingredient dropping down
                            Vector3 newPosition = GetStackPosition(j);
                            storedIngredients[j].transform.DOMove(newPosition, 0.2f).SetEase(Ease.OutQuad);
                        }
                    }
                    storedIngredients[storedIngredients.Length - 1] = null;
                    
                    Debug.Log($"[RTCookInputContainer] Cook took ingredient from slot {i}. Remaining: {StoredCount}/{SlotCount}");
                    return ingredient;
                }
            }
            return null;
        }

        private int GetFirstEmptySlot()
        {
            for (int i = 0; i < storedIngredients.Length; i++)
            {
                if (storedIngredients[i] == null) return i;
            }
            return -1;
        }

        #endregion

        #region Cooking Animation

        /// <summary>Starts a looping shake on the table to indicate active cooking.</summary>
        public void StartCookingAnimation()
        {
            cookingShakeTween?.Kill();

            // Use shake target if assigned, otherwise use the container itself
            Transform targetTransform = shakeTarget != null ? shakeTarget.transform : transform;
            Vector3 targetOriginalPosition = shakeTarget != null ? shakeTargetOriginalPosition : originalLocalPosition;
            Quaternion targetOriginalRotation = shakeTarget != null ? shakeTargetOriginalRotation : originalLocalRotation;

            targetTransform.localPosition = targetOriginalPosition;
            targetTransform.localRotation = targetOriginalRotation;
            cookingShakeTween = targetTransform
                .DOShakeRotation(0.6f, new Vector3(0f, 0f, 3f), 15, 90f, false)
                .SetLoops(-1, LoopType.Restart)
                .SetLink(gameObject);
        }

        /// <summary>Stops the cooking shake and resets the table to its original transform.</summary>
        public void StopCookingAnimation()
        {
            cookingShakeTween?.Kill();
            cookingShakeTween = null;

            // Reset shake target if assigned, otherwise reset the container itself
            if (shakeTarget != null)
            {
                shakeTarget.transform.localPosition = shakeTargetOriginalPosition;
                shakeTarget.transform.localRotation = shakeTargetOriginalRotation;
            }
            else
            {
                transform.localPosition = originalLocalPosition;
                transform.localRotation = originalLocalRotation;
            }
        }

        #endregion

        #region Porter Delivery

        /// <summary>
        /// Called by RTPorterController to place an ingredient directly into a slot.
        /// The ingredient is snapped/animated to the slot position.
        /// Returns false if the container is full.
        /// </summary>
        public bool ReceiveIngredient(RTIngredient ingredient)
        {
            if (ingredient == null || IsFull) return false;

            int slotIndex = GetFirstEmptySlot();
            if (slotIndex < 0) return false;

            storedIngredients[slotIndex] = ingredient;
            Vector3 targetPosition = GetStackPosition(slotIndex);

            Vector3 savedScale = ingredient.transform.lossyScale;
            DOTween.Kill(ingredient.transform, true);
            ingredient.transform.SetParent(null);
            ingredient.transform.localScale = savedScale;

            ingredient.transform
                .DOJump(targetPosition, dropJumpHeight, 1, dropDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    ingredient.transform.position = targetPosition;
                    if (slotReference != null)
                        ingredient.transform.rotation = slotReference.rotation;
                });

            Debug.Log($"[RTCookInputContainer] Porter delivered ingredient to slot {slotIndex}. Stored: {StoredCount}/{SlotCount}");
            OnIngredientAdded?.Invoke();
            return true;
        }

        #endregion
    }
}
