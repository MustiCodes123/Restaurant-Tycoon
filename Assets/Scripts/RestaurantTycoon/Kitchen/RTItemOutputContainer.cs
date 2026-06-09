using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Container that holds finished items produced by the cook.
    /// Player enters trigger to pick them up (same pattern as RTIngredientContainer
    /// but for FinishedItem type).
    /// </summary>
    public class RTItemOutputContainer : MonoBehaviour
    {
        [Header("Slots")]
        [Tooltip("Points where finished items sit. First is bottom, last is top.")]
        [SerializeField] private List<Transform> outputSlots = new List<Transform>();

        [Header("Player Pickup")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private float pickupInterval = 0.3f;

        private RTFinishedItem[] storedItems;
        private bool playerInRange = false;
        private RTPlayerCarryController playerCarryController;
        private Coroutine pickupCoroutine;
        private Vector3 originalLocalScale;

        public int SlotCount => outputSlots.Count;

        public int StoredCount
        {
            get
            {
                int count = 0;
                if (storedItems != null)
                {
                    foreach (var item in storedItems)
                    {
                        if (item != null) count++;
                    }
                }
                return count;
            }
        }

        public bool IsFull => outputSlots.Count == 0 || StoredCount >= outputSlots.Count;
        public bool HasItems => StoredCount > 0;

        private void Start()
        {
            storedItems = new RTFinishedItem[outputSlots.Count];
            originalLocalScale = transform.localScale;

            // Find player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerCarryController = player.GetComponent<RTPlayerCarryController>();
                if (playerCarryController == null)
                    playerCarryController = player.GetComponentInChildren<RTPlayerCarryController>();
            }
        }

        #region Cook Access (Adding Items)

        /// <summary>
        /// Returns the transform of the next empty slot, or null if full.
        /// Called by RTCook to know WHERE to bounce the finished item.
        /// </summary>
        public Transform GetNextEmptySlot()
        {
            for (int i = 0; i < storedItems.Length; i++)
            {
                if (storedItems[i] == null) return outputSlots[i];
            }
            return null;
        }

        /// <summary>
        /// Plays a punch-scale pop on the container to signal a finished item is ready.
        /// </summary>
        public void PlayItemReadyAnimation()
        {
            transform.DOKill();
            transform.localScale = originalLocalScale;
            transform.DOPunchScale(new Vector3(0.18f, 0.18f, 0.18f), 0.5f, 8, 0.5f)
                .SetLink(gameObject);
        }

        /// <summary>
        /// Register a finished item in the next empty slot.
        /// Called by RTCook after spawning.
        /// </summary>
        public void AddItem(RTFinishedItem item)
        {
            for (int i = 0; i < storedItems.Length; i++)
            {
                if (storedItems[i] == null)
                {
                    storedItems[i] = item;
                    return;
                }
            }
            Debug.LogWarning("[RTItemOutputContainer] No empty slot to add item!");
        }

        #endregion

        #region Player Pickup

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
                StartPickup();
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
            yield return new WaitForSeconds(pickupInterval);

            while (playerInRange && playerCarryController != null)
            {
                if (!playerCarryController.CanCarryMore)
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                // Find the topmost stored finished item
                RTFinishedItem topItem = null;
                int topIndex = -1;
                for (int i = outputSlots.Count - 1; i >= 0; i--)
                {
                    if (storedItems[i] != null)
                    {
                        topItem = storedItems[i];
                        topIndex = i;
                        break;
                    }
                }

                if (topItem == null)
                {
                    yield return new WaitForSeconds(0.3f);
                    continue;
                }

                // Complete any running tweens (snaps to intended final scale)
                DOTween.Kill(topItem.transform, true);

                // Remove from container
                storedItems[topIndex] = null;

                // Hand to player carry system
                if (playerCarryController.TryPickup(topItem))
                {
                    Debug.Log($"[RTItemOutputContainer] Player picked up finished item from slot {topIndex}. Output: {StoredCount}/{SlotCount}");
                }
                else
                {
                    // Failed, put it back
                    storedItems[topIndex] = topItem;
                }

                yield return new WaitForSeconds(pickupInterval);
            }

            pickupCoroutine = null;
        }

        #endregion
    }
}
