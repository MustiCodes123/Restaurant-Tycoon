using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Unified carry system for the restaurant tycoon game.
    /// Single stack of up to maxCarryCount items.
    /// All carryable types (ingredients, finished items, garbage) share the same stack.
    /// Top-of-stack item type determines what can be dropped at a given container.
    /// </summary>
    public class RTPlayerCarryController : MonoBehaviour
    {
        [Header("Carry Settings")]
        [Tooltip("Single base point where the first item sits. All items stack from here.")]
        [SerializeField] private Transform carryBasePoint;
        [Tooltip("Local-space offset per stacked item (typically upward).")]
        [SerializeField] private Vector3 carryOffset = new Vector3(0, 0.3f, 0);

        [Header("Settings")]
        [SerializeField] private int maxCarryCount = 6;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string liftIdleParam = "IsLiftIdle";
        [SerializeField] private string liftWalkParam = "IsLiftWalking";

        [Header("Pickup Animation")]
        [SerializeField] private float pickupJumpHeight = 0.3f;
        [SerializeField] private float pickupDuration = 0.2f;

        // Single unified stack
        private List<IRTCarryable> carriedItems = new List<IRTCarryable>();
        private HashSet<Transform> animatingItems = new HashSet<Transform>();
        private bool isMoving = false;

        public int CarriedCount => carriedItems.Count;
        public bool IsCarrying => carriedItems.Count > 0;
        public bool CanCarryMore
        {
            get
            {
                if (carryBasePoint == null)
                {
                    Debug.LogWarning("[RTPlayerCarryController] No carryBasePoint assigned! Player cannot carry anything.");
                    return false;
                }
                return carriedItems.Count < maxCarryCount;
            }
        }
        public int MaxCarryCount => maxCarryCount;
        public int CarryPointCount => maxCarryCount;

        /// <summary>
        /// Returns the type of the topmost item on the stack, or null if empty.
        /// </summary>
        public CarryableType? TopItemType => carriedItems.Count > 0 ? carriedItems[carriedItems.Count - 1].CarryType : (CarryableType?)null;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void Update()
        {
            UpdateAnimation();
        }

        private void LateUpdate()
        {
            SnapCarriedItems();
        }

        /// <summary>
        /// Keeps all non-animating carried items locked to their correct local offsets.
        /// Prevents drift caused by player movement/rotation during or after pickup tweens.
        /// </summary>
        private void SnapCarriedItems()
        {
            for (int i = 0; i < carriedItems.Count; i++)
            {
                if (carriedItems[i]?.GameObject == null) continue;
                Transform t = carriedItems[i].GameObject.transform;
                if (animatingItems.Contains(t)) continue;
                t.localPosition = carryOffset * i;
                t.localRotation = Quaternion.identity;
            }
        }

        public void SetMoving(bool moving)
        {
            isMoving = moving;
        }

        private void UpdateAnimation()
        {
            if (animator == null) return;

            if (IsCarrying)
            {
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

        /// <summary>
        /// Pick up any carryable item and add it to the top of the stack.
        /// </summary>
        public bool TryPickup(IRTCarryable item)
        {
            if (!CanCarryMore || item == null) return false;

            int slotIndex = carriedItems.Count;
            Vector3 localTarget = carryOffset * slotIndex;

            carriedItems.Add(item);

            // Let the item handle its own pickup (parenting, visuals)
            item.OnPickedUp(carryBasePoint);

            Transform itemTransform = item.GameObject.transform;

            // Complete any active tweens so item reaches its intended final scale/position
            DOTween.Kill(itemTransform, true);

            // Parent immediately so item moves with the player during animation
            itemTransform.SetParent(carryBasePoint);

            // Mark as animating so LateUpdate doesn't interfere
            animatingItems.Add(itemTransform);

            Sequence pickupSequence = DOTween.Sequence();
            pickupSequence.Append(itemTransform.DOLocalJump(localTarget, pickupJumpHeight, 1, pickupDuration));
            pickupSequence.Join(itemTransform.DOLocalRotate(Vector3.zero, pickupDuration));
            pickupSequence.OnComplete(() =>
            {
                itemTransform.localPosition = localTarget;
                itemTransform.localRotation = Quaternion.identity;
                animatingItems.Remove(itemTransform);
            });
            pickupSequence.OnKill(() =>
            {
                animatingItems.Remove(itemTransform);
            });

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.ItemPickup);
            }

            Debug.Log($"[RTPlayerCarryController] Picked up {item.CarryType}. Stack: {carriedItems.Count}/{maxCarryCount}");
            return true;
        }

        /// <summary>
        /// Check if any item in the stack matches the requested type.
        /// Searches top-down so the nearest matching item is found first.
        /// Used by containers to know if the player can drop here.
        /// </summary>
        public bool IsTopItemType(CarryableType type)
        {
            for (int i = carriedItems.Count - 1; i >= 0; i--)
            {
                if (carriedItems[i].CarryType == type) return true;
            }
            return false;
        }

        /// <summary>
        /// Peek at the topmost item of the given type without removing it.
        /// Searches top-down so items above a different type don't block access.
        /// Returns null if no item of that type exists in the stack.
        /// </summary>
        public IRTCarryable PeekTopItem(CarryableType type)
        {
            for (int i = carriedItems.Count - 1; i >= 0; i--)
            {
                if (carriedItems[i].CarryType == type) return carriedItems[i];
            }
            return null;
        }

        /// <summary>
        /// Take the topmost item of the given type from the stack, searching top-down.
        /// Items of a different type sitting above it do not block the drop.
        /// After removal, the remaining stack is reorganized to close any visual gap.
        /// Returns null if no item of that type exists.
        /// </summary>
        public IRTCarryable TakeTopItem(CarryableType type)
        {
            if (carriedItems.Count == 0) return null;

            for (int i = carriedItems.Count - 1; i >= 0; i--)
            {
                if (carriedItems[i].CarryType != type) continue;

                IRTCarryable item = carriedItems[i];
                carriedItems.RemoveAt(i);
                animatingItems.Remove(item.GameObject.transform);
                item.OnDropped();

                // If removal left a gap in the middle or bottom, shift remaining items down.
                if (i < carriedItems.Count)
                    ReorganizeStack();

                Debug.Log($"[RTPlayerCarryController] Dropped {type} from index {i}. Stack: {carriedItems.Count}/{maxCarryCount}");
                return item;
            }

            return null;
        }

        /// <summary>
        /// Peek at the first item (top-down) that satisfies the predicate.
        /// Returns null if no matching item exists.
        /// </summary>
        public IRTCarryable PeekTopItem(System.Func<IRTCarryable, bool> predicate)
        {
            for (int i = carriedItems.Count - 1; i >= 0; i--)
            {
                if (predicate(carriedItems[i])) return carriedItems[i];
            }
            return null;
        }

        /// <summary>
        /// Take the first item (top-down) that satisfies the predicate.
        /// After removal, the remaining stack is reorganized to close any visual gap.
        /// Returns null if no matching item exists.
        /// </summary>
        public IRTCarryable TakeTopItem(System.Func<IRTCarryable, bool> predicate)
        {
            for (int i = carriedItems.Count - 1; i >= 0; i--)
            {
                if (!predicate(carriedItems[i])) continue;

                IRTCarryable item = carriedItems[i];
                carriedItems.RemoveAt(i);
                animatingItems.Remove(item.GameObject.transform);
                item.OnDropped();

                if (i < carriedItems.Count)
                    ReorganizeStack();

                Debug.Log($"[RTPlayerCarryController] Dropped item via predicate from index {i}. Stack: {carriedItems.Count}/{maxCarryCount}");
                return item;
            }
            return null;
        }

        /// <summary>
        /// Take the top item regardless of type.
        /// </summary>
        public IRTCarryable TakeTopItem()
        {
            if (carriedItems.Count == 0) return null;

            int topIndex = carriedItems.Count - 1;
            IRTCarryable topItem = carriedItems[topIndex];
            carriedItems.RemoveAt(topIndex);
            animatingItems.Remove(topItem.GameObject.transform);
            topItem.OnDropped();

            Debug.Log($"[RTPlayerCarryController] Dropped top item ({topItem.CarryType}). Stack: {carriedItems.Count}/{maxCarryCount}");
            return topItem;
        }

        /// <summary>
        /// Count how many items of a specific type are in the stack.
        /// </summary>
        public int CountOfType(CarryableType type)
        {
            int count = 0;
            foreach (var item in carriedItems)
            {
                if (item.CarryType == type) count++;
            }
            return count;
        }

        /// <summary>
        /// Count how many consecutive items from the top match the given type.
        /// E.g., if stack is [Ingredient, Ingredient, Garbage], ConsecutiveTopCount(Garbage) = 1
        /// </summary>
        public int ConsecutiveTopCount(CarryableType type)
        {
            int count = 0;
            for (int i = carriedItems.Count - 1; i >= 0; i--)
            {
                if (carriedItems[i].CarryType == type)
                    count++;
                else
                    break;
            }
            return count;
        }

        /// <summary>
        /// Dispose all carried items (e.g., at garbage bin).
        /// </summary>
        public int DisposeAll()
        {
            int count = carriedItems.Count;
            for (int i = carriedItems.Count - 1; i >= 0; i--)
            {
                animatingItems.Remove(carriedItems[i].GameObject.transform);
                carriedItems[i].OnDisposed();
            }
            carriedItems.Clear();

            Debug.Log($"[RTPlayerCarryController] Disposed all {count} items");
            return count;
        }

        /// <summary>
        /// Dispose all items of a specific type.
        /// </summary>
        public int DisposeAllOfType(CarryableType type)
        {
            int count = 0;
            for (int i = carriedItems.Count - 1; i >= 0; i--)
            {
                if (carriedItems[i].CarryType == type)
                {
                    animatingItems.Remove(carriedItems[i].GameObject.transform);
                    carriedItems[i].OnDisposed();
                    carriedItems.RemoveAt(i);
                    count++;
                }
            }

            // Re-parent remaining items to correct carry points (stack may have gaps)
            ReorganizeStack();

            Debug.Log($"[RTPlayerCarryController] Disposed {count} items of type {type}. Stack: {carriedItems.Count}/{maxCarryCount}");
            return count;
        }

        /// <summary>
        /// After removing items from the middle of the stack, re-parent everything
        /// to the correct carry points so there are no visual gaps.
        /// </summary>
        private void ReorganizeStack()
        {
            for (int i = 0; i < carriedItems.Count; i++)
            {
                Vector3 localTarget = carryOffset * i;
                GameObject obj = carriedItems[i].GameObject;
                if (obj.transform.parent != carryBasePoint)
                    obj.transform.SetParent(carryBasePoint);
                obj.transform.DOLocalMove(localTarget, 0.15f);
                obj.transform.DOLocalRotate(Vector3.zero, 0.15f);
            }
        }
    }
}
