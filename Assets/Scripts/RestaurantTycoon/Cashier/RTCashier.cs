using UnityEngine;
using System;
using System.Collections.Generic;

namespace RestaurantTycoon
{
    /// <summary>
    /// Cashier counter where the player services customers after they finish eating.
    /// Customers queue up, player enters trigger, stands still → radial progress fills → customer served and leaves.
    /// </summary>
    public class RTCashier : MonoBehaviour
    {
        [Header("Queue")]
        [SerializeField] private int maxQueueSize = 4;
        [SerializeField] private float queueSpacing = 1.5f;
        [SerializeField] private Transform queueStartPoint;
        [SerializeField] private Transform queueDirection;

        [Header("Player Detection")]
        // No layer mask needed — player detected via RTPlayerController component

        [Header("Money Drop")]
        [Tooltip("Prefab with MoneyDrop script — spawned on the ground for the player to walk over and collect.")]
        [SerializeField] private GameObject moneyDropPrefab;
        [Tooltip("Vertical offset applied when spawning the money prefab so it sits above the ground.")]
        [SerializeField] private float moneyDropYOffset = 0.5f;
        [Tooltip("Horizontal offset applied when spawning the money prefab (along the world X axis).")]
        [SerializeField] private float moneyDropXOffset = 0f;

        private List<RTCustomer> queue = new List<RTCustomer>();
        private bool playerInRange;
        private bool isBeingServiced;
        private Transform playerTransform;

        public int QueueCount => queue.Count;
        public int MaxQueueSize => maxQueueSize;
        public bool CanAcceptCustomer => queue.Count < maxQueueSize;

        /// <summary>
        /// True when a customer is at the front and has physically arrived.
        /// Used by RTPlayerController to know when to start the radial progress.
        /// </summary>
        public bool CanServe
        {
            get
            {
                if (queue.Count == 0) return false;
                return queue[0].IsWaitingAtCashier;
            }
        }

        /// <summary>Fired when the front customer is fully served (radial complete).</summary>
        public event Action OnCustomerServed;

        #region Customer Queue

        public int AddCustomerToQueue(RTCustomer customer)
        {
            if (customer == null || queue.Count >= maxQueueSize) return -1;

            queue.Add(customer);
            int pos = queue.Count - 1;
            Debug.Log($"[RTCashier] Customer queued at position {pos}. Queue: {queue.Count}/{maxQueueSize}");
            return pos;
        }

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

        public void RemoveCustomer(RTCustomer customer)
        {
            int index = queue.IndexOf(customer);
            if (index < 0) return;

            queue.RemoveAt(index);
            isBeingServiced = false;
            Debug.Log($"[RTCashier] Customer removed from position {index}. Queue: {queue.Count}");

            for (int i = index; i < queue.Count; i++)
                queue[i].OnCashierQueuePositionChanged(i);
        }

        public RTCustomer GetFrontCustomer()
        {
            return queue.Count > 0 ? queue[0] : null;
        }

        #endregion

        #region Player Service

        /// <summary>Called by RTPlayerController when radial progress starts.</summary>
        public void StartService()
        {
            isBeingServiced = true;
        }

        /// <summary>Called by RTPlayerController when radial progress completes.</summary>
        public void CompleteService()
        {
            if (queue.Count == 0) return;

            RTCustomer front = queue[0];
            int moneyAmount = front.MoneyPerCustomer;

            // Spawn the money drop prefab on the ground at the customer's feet.
            // MoneyDrop.Initialize sets the amount; MoneyDrop itself handles
            // proximity pickup, animation, and money registration on collection.
            if (moneyDropPrefab != null)
            {
                Vector3 spawnPos = front.transform.position + new Vector3(moneyDropXOffset, moneyDropYOffset, 0f);
                GameObject drop = Instantiate(moneyDropPrefab, spawnPos, Quaternion.identity);
                MoneyDrop moneyDrop = drop.GetComponent<MoneyDrop>();
                if (moneyDrop != null)
                    moneyDrop.Initialize(moneyAmount);
                else
                    Debug.LogError("[RTCashier] moneyDropPrefab is missing a MoneyDrop component!");
            }
            else
            {
                Debug.LogError("[RTCashier] moneyDropPrefab is not assigned in the Inspector! Please assign the money drop prefab.");
                // Fallback: register money directly so the game is not broken
                if (RTLevelManager.Instance != null)
                    RTLevelManager.Instance.RegisterMoneyEarned(moneyAmount);
                else if (CurrencyManager.Instance != null)
                    CurrencyManager.Instance.AddMoney(moneyAmount);
            }

            front.OnServedAtCashier();

            OnCustomerServed?.Invoke();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SoundEffect.CustomerServed);

            Debug.Log($"[RTCashier] Customer served. Money: ${moneyAmount}");
        }

        /// <summary>
        /// Lets an NPC cashier character register its transform so the money flow
        /// effect targets it instead of the player.
        /// </summary>
        public void SetServiceTransform(Transform serviceTransform)
        {
            playerTransform = serviceTransform;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<RTPlayerController>() == null) return;
            playerInRange = true;
            playerTransform = other.transform;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<RTPlayerController>() == null) return;
            playerInRange = false;
        }

        public bool IsPlayerInRange => playerInRange;

        #endregion

        private void OnDrawGizmos()
        {
            Gizmos.color = queue.Count > 0 ? Color.yellow : Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            if (queueStartPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(queueStartPoint.position, 0.3f);

                Gizmos.color = Color.yellow;
                for (int i = 0; i < maxQueueSize; i++)
                {
                    Vector3 pos = GetQueueWorldPosition(i);
                    Gizmos.DrawWireSphere(pos, 0.2f);
                    if (i > 0)
                        Gizmos.DrawLine(GetQueueWorldPosition(i - 1), pos);
                }
            }
        }
    }
}
