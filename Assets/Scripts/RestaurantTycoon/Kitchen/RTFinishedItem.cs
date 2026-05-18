using UnityEngine;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// A finished item (e.g., coffee) produced by the cook.
    /// Can be picked up by the player and placed on the customer counter.
    /// Implements IRTCarryable for the unified carry system.
    /// </summary>
    public class RTFinishedItem : MonoBehaviour, IRTCarryable
    {
        [Header("Type")]
        [Tooltip("Must match the RTCustomerCounter that should accept this item.")]
        [SerializeField] private RTIngredientType itemType;

        [Header("Animation")]
        [SerializeField] private float spawnPopScale = 1.2f;
        [SerializeField] private float spawnPopDuration = 0.3f;
        [SerializeField] private Ease spawnPopEase = Ease.OutBack;

        private bool isPickedUp = false;

        public CarryableType CarryType => CarryableType.FinishedItem;
        public GameObject GameObject => gameObject;
        public bool IsPickedUp => isPickedUp;
        public RTIngredientType ItemType => itemType;

        public void PlaySpawnAnimation()
        {
            Vector3 originalScale = transform.localScale;
            transform.localScale = Vector3.zero;

            transform.DOScale(originalScale * spawnPopScale, spawnPopDuration * 0.6f)
                .SetEase(spawnPopEase)
                .OnComplete(() => transform.DOScale(originalScale, spawnPopDuration * 0.4f));
        }

        public void OnPickedUp(Transform carryPoint)
        {
            isPickedUp = true;
        }

        public void OnDropped()
        {
            isPickedUp = false;
        }

        public void OnDisposed()
        {
            transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() => Destroy(gameObject));
        }
    }
}
