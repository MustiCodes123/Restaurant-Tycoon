using UnityEngine;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// An ingredient item that can be picked up by the player and dropped at the cook's input.
    /// Implements IRTCarryable for the unified carry system.
    /// </summary>
    public class RTIngredient : MonoBehaviour, IRTCarryable
    {
        [Header("Type")]
        [Tooltip("The type of this ingredient. Must match the RTCookInputContainer that should accept it.")]
        [SerializeField] private RTIngredientType ingredientType;

        [Header("Animation")]
        [SerializeField] private float spawnPopScale = 1.2f;
        [SerializeField] private float spawnPopDuration = 0.3f;
        [SerializeField] private Ease spawnPopEase = Ease.OutBack;

        private bool isPickedUp = false;

        public CarryableType CarryType => CarryableType.Ingredient;
        public GameObject GameObject => gameObject;
        public bool IsPickedUp => isPickedUp;
        public RTIngredientType IngredientType => ingredientType;

        public void PlaySpawnAnimation()
        {
            Vector3 originalScale = transform.localScale;
            transform.localScale = Vector3.zero;

            transform.DOScale(originalScale * spawnPopScale, spawnPopDuration * 0.6f)
                .SetEase(spawnPopEase)
                .OnComplete(() => transform.DOScale(originalScale, spawnPopDuration * 0.4f));
        }

        /// <summary>
        /// Animate ingredient from a source point (e.g., van) to its spot on the container.
        /// </summary>
        public void AnimateToSpot(Vector3 fromPosition, Transform spotTransform, float duration, System.Action onComplete = null)
        {
            transform.position = fromPosition;
            Vector3 originalScale = transform.localScale; // preserve the prefab's set scale
            transform.localScale = Vector3.zero;

            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOScale(originalScale, duration * 0.3f)
                .SetEase(Ease.OutBack));
            seq.Join(transform.DOJump(spotTransform.position, 1f, 1, duration)
                .SetEase(Ease.OutQuad));
            seq.Join(transform.DORotate(spotTransform.eulerAngles, duration));
            seq.OnComplete(() => onComplete?.Invoke());
        }

        public void OnPickedUp(Transform carryPoint)
        {
            isPickedUp = true;
            // Parenting and jump animation handled by RTPlayerCarryController
        }

        public void OnDropped()
        {
            isPickedUp = false;
            // Un-parent handled by the receiving container
        }

        public void OnDisposed()
        {
            transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() => Destroy(gameObject));
        }
    }
}
