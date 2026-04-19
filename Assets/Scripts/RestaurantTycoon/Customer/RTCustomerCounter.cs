using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Counter where the player drops finished items for customers.
    /// When the player enters the trigger and top-of-stack is FinishedItem,
    /// items transfer one at a time into counter slots.
    /// Customers queue up and take items from here.
    /// </summary>
    public class RTCustomerCounter : MonoBehaviour
    {
        [Header("Item Slots")]
        [Tooltip("Points where finished items sit on the counter. First is bottom.")]
        [SerializeField] private List<Transform> itemSlots = new List<Transform>();

        [Header("Queue Settings")]
        [SerializeField] private int maxQueueSize = 4;
        [SerializeField] private float queueSpacing = 1.5f;
        [SerializeField] private Transform queueStartPoint;
        [SerializeField] private Transform queueDirection;

        [Header("Player Detection")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private float dropInterval = 0.3f;

        [Header("Drop Animation")]
        [SerializeField] private float dropJumpHeight = 0.5f;
        [SerializeField] private float dropDuration = 0.25f;

        private RTFinishedItem[] storedItems;
        private bool playerInRange = false;
        private RTPlayerCarryController playerCarryController;
        private Coroutine dropCoroutine;

        // Queue
        private List<RTCustomer> customerQueue = new List<RTCustomer>();

        /// <summary>Fired when a new finished item is placed on the counter.</summary>
        public event Action OnItemPlaced;

        public int SlotCount => itemSlots.Count;
        public int MaxQueueSize => maxQueueSize;

        public int StoredCount
        {
            get
            {
                int count = 0;
                if (storedItems != null)
                    foreach (var item in storedItems)
                        if (item != null) count++;
                return count;
            }
        }

        public bool IsFull => StoredCount >= itemSlots.Count;
        public bool HasItem => StoredCount > 0;
        public int QueueCount => customerQueue.Count;
        public bool CanAcceptCustomer => customerQueue.Count < maxQueueSize;

        private void Start()
        {
            storedItems = new RTFinishedItem[itemSlots.Count];

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerCarryController = player.GetComponent<RTPlayerCarryController>();
                if (playerCarryController == null)
                    playerCarryController = player.GetComponentInChildren<RTPlayerCarryController>();
            }
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
                StartDropping();
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
                if (IsFull || !playerCarryController.IsTopItemType(CarryableType.FinishedItem))
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                IRTCarryable item = playerCarryController.TakeTopItem(CarryableType.FinishedItem);
                if (item == null)
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                RTFinishedItem finishedItem = item.GameObject.GetComponent<RTFinishedItem>();
                if (finishedItem == null)
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                int slotIndex = GetFirstEmptySlot();
                if (slotIndex < 0)
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                storedItems[slotIndex] = finishedItem;
                Transform slot = itemSlots[slotIndex];

                // Save the world-space scale before unparenting
                Vector3 savedScale = finishedItem.transform.lossyScale;

                // Complete any active tweens to their final values
                DOTween.Kill(finishedItem.transform, true);

                finishedItem.transform.SetParent(null);
                finishedItem.transform.localScale = savedScale;

                finishedItem.transform.DOJump(slot.position, dropJumpHeight, 1, dropDuration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        finishedItem.transform.position = slot.position;
                        finishedItem.transform.rotation = slot.rotation;
                    });

                Debug.Log($"[RTCustomerCounter] Item placed in slot {slotIndex}. Counter: {StoredCount}/{SlotCount}");

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(SoundEffect.ItemPlace);

                OnItemPlaced?.Invoke();

                yield return new WaitForSeconds(dropInterval);
            }

            dropCoroutine = null;
        }

        private int GetFirstEmptySlot()
        {
            for (int i = 0; i < storedItems.Length; i++)
                if (storedItems[i] == null) return i;
            return -1;
        }

        #endregion

        #region Customer Queue

        /// <summary>
        /// Add a customer to the queue. Returns queue position or -1 if full.
        /// </summary>
        public int AddCustomerToQueue(RTCustomer customer)
        {
            if (customer == null || customerQueue.Count >= maxQueueSize) return -1;

            customerQueue.Add(customer);
            int position = customerQueue.Count - 1;
            Debug.Log($"[RTCustomerCounter] Customer queued at position {position}. Queue: {customerQueue.Count}/{maxQueueSize}");
            return position;
        }

        /// <summary>
        /// Get the world position for a queue slot.
        /// </summary>
        public Vector3 GetQueueWorldPosition(int position)
        {
            if (queueStartPoint == null) return transform.position;

            if (position == 0)
                return queueStartPoint.position;

            Vector3 dir;
            if (queueDirection != null)
                dir = (queueDirection.position - queueStartPoint.position).normalized;
            else
                dir = -transform.forward;

            return queueStartPoint.position + dir * (position * queueSpacing);
        }

        /// <summary>
        /// Remove a customer from the queue and advance others.
        /// </summary>
        public void RemoveCustomer(RTCustomer customer)
        {
            int index = customerQueue.IndexOf(customer);
            if (index < 0) return;

            customerQueue.RemoveAt(index);
            Debug.Log($"[RTCustomerCounter] Customer removed from position {index}. Queue: {customerQueue.Count}");

            for (int i = index; i < customerQueue.Count; i++)
                customerQueue[i].OnCounterQueuePositionChanged(i);
        }

        /// <summary>
        /// Called by the front customer to take the first available item.
        /// Returns null if no items available.
        /// </summary>
        public RTFinishedItem TakeItem()
        {
            for (int i = 0; i < storedItems.Length; i++)
            {
                if (storedItems[i] != null)
                {
                    RTFinishedItem item = storedItems[i];
                    storedItems[i] = null;
                    Debug.Log($"[RTCustomerCounter] Customer took item from slot {i}. Remaining: {StoredCount}/{SlotCount}");
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// Get the front customer if they're waiting.
        /// </summary>
        public RTCustomer GetFrontCustomer()
        {
            return customerQueue.Count > 0 ? customerQueue[0] : null;
        }

        #endregion
    }
}
