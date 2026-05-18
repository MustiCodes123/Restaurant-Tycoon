using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// A dining table with seats. Tracks dirty dishes after customers eat.
    /// Player enters trigger to pick up dirty dishes into carry stack.
    /// Shows garbage UI canvas when dirty dishes are present.
    /// </summary>
    public class RTDiningTable : MonoBehaviour
    {
        [Header("Seats")]
        [SerializeField] private List<RTDiningSeat> seats = new List<RTDiningSeat>();

        [Header("Item Placement")]
        [Tooltip("Base point where food items stack on the table surface")]
        [SerializeField] private Transform itemPlacementPoint;
        [SerializeField] private Vector3 itemStackOffset = new Vector3(0, 0.15f, 0);

        [Header("Dirty Dishes")]
        [SerializeField] private GameObject dirtyDishPrefab;

        [Header("Garbage UI")]
        [Tooltip("Canvas shown when table has dirty dishes")]
        [SerializeField] private Canvas garbageUICanvas;

        [Header("Player Pickup")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private float pickupInterval = 0.3f;

        private List<RTDirtyDish> dirtyDishes = new List<RTDirtyDish>();
        private bool playerInRange;
        private RTPlayerCarryController playerCarry;
        private Coroutine pickupCoroutine;

        public List<RTDiningSeat> Seats => seats;
        public Transform ItemPlacementPoint => itemPlacementPoint;
        public Vector3 ItemStackOffset => itemStackOffset;
        public bool HasDirtyDishes => dirtyDishes.Count > 0;
        public int DirtyDishCount => dirtyDishes.Count;

        public event Action OnDishesCleared;

        private void Awake()
        {
            if (seats.Count == 0)
                seats.AddRange(GetComponentsInChildren<RTDiningSeat>());

            foreach (var seat in seats)
                seat.Initialize(this);

            HideGarbageUI();
        }

        #region Seat Management

        public RTDiningSeat GetAvailableSeat()
        {
            if (HasDirtyDishes) return null;
            foreach (var seat in seats)
                if (seat.IsAvailable) return seat;
            return null;
        }

        public bool HasAvailableSeat()
        {
            if (HasDirtyDishes) return false;
            foreach (var seat in seats)
                if (seat.IsAvailable) return true;
            return false;
        }

        public int GetAvailableSeatCount()
        {
            if (HasDirtyDishes) return 0;
            int count = 0;
            foreach (var seat in seats)
                if (seat.IsAvailable) count++;
            return count;
        }

        #endregion

        #region Dirty Dishes

        /// <summary>
        /// Called by the RT janitor to take all dirty dishes at once.
        /// Removes them from the internal list and marks the table clean.
        /// </summary>
        public List<RTDirtyDish> TakeAllDirtyDishes()
        {
            List<RTDirtyDish> taken = new List<RTDirtyDish>(dirtyDishes);
            dirtyDishes.Clear();
            CheckCleanState();
            return taken;
        }

        /// <summary>
        /// Spawn a dirty dish at the given world position.
        /// Called by RTCustomer during eating as each food item is consumed.
        /// </summary>
        public RTDirtyDish SpawnDirtyDishAt(Vector3 position, Quaternion rotation)
        {
            if (dirtyDishPrefab == null)
            {
                Debug.LogWarning("[RTDiningTable] No dirtyDishPrefab assigned!");
                return null;
            }

            GameObject obj = Instantiate(dirtyDishPrefab, position, rotation);
            RTDirtyDish dish = obj.GetComponent<RTDirtyDish>();
            if (dish == null)
                dish = obj.AddComponent<RTDirtyDish>();

            dish.Initialize(this);
            dirtyDishes.Add(dish);

            ShowGarbageUI();

            Debug.Log($"[RTDiningTable] Dirty dish spawned. Total: {dirtyDishes.Count}");
            return dish;
        }

        /// <summary>
        /// Called internally when a dirty dish is removed from tracking.
        /// </summary>
        private void CheckCleanState()
        {
            if (dirtyDishes.Count == 0)
            {
                HideGarbageUI();
                OnDishesCleared?.Invoke();
                Debug.Log("[RTDiningTable] All dishes cleared, table is clean");
            }
        }

        #endregion

        #region Player Pickup

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            playerInRange = true;

            if (playerCarry == null)
            {
                playerCarry = other.GetComponent<RTPlayerCarryController>();
                if (playerCarry == null)
                    playerCarry = other.GetComponentInParent<RTPlayerCarryController>();
            }

            if (playerCarry != null && HasDirtyDishes)
                StartPickup();
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

            while (playerInRange && playerCarry != null && HasDirtyDishes)
            {
                if (!playerCarry.CanCarryMore)
                {
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                int lastIndex = dirtyDishes.Count - 1;
                RTDirtyDish dish = dirtyDishes[lastIndex];

                if (dish == null)
                {
                    dirtyDishes.RemoveAt(lastIndex);
                    CheckCleanState();
                    continue;
                }

                dirtyDishes.RemoveAt(lastIndex);

                bool picked = playerCarry.TryPickup(dish);
                if (!picked)
                {
                    dirtyDishes.Add(dish);
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                CheckCleanState();

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(SoundEffect.ItemPickup);

                yield return new WaitForSeconds(pickupInterval);
            }

            pickupCoroutine = null;
        }

        #endregion

        #region UI

        private void ShowGarbageUI()
        {
            if (garbageUICanvas != null)
                garbageUICanvas.enabled = true;
        }

        private void HideGarbageUI()
        {
            if (garbageUICanvas != null)
                garbageUICanvas.enabled = false;
        }

        #endregion

        private void OnDrawGizmos()
        {
            Gizmos.color = HasDirtyDishes ? Color.red : Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(1f, 0.1f, 1f));

            if (itemPlacementPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(itemPlacementPoint.position, 0.1f);
            }
        }
    }
}
