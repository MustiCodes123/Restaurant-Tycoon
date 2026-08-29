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
        private bool hasRestingLocalScale = false;
        private Vector3 restingLocalScale = Vector3.one;
        private Vector3 restingWorldScale = Vector3.one;
        private Tween activeAnimation;

        public CarryableType CarryType => CarryableType.Ingredient;
        public GameObject GameObject => gameObject;
        public bool IsPickedUp => isPickedUp;
        public RTIngredientType IngredientType => ingredientType;
        public Vector3 RestingWorldScale => GetRestingWorldScale();

        private void Awake()
        {
            RememberRestingScale(transform.localScale);
        }

        public void PlaySpawnAnimation()
        {
            CompleteActiveAnimation(false);
            Vector3 originalScale = GetRestingLocalScale();
            transform.localScale = Vector3.zero;

            Sequence seq = DOTween.Sequence().SetTarget(transform);
            seq.Append(transform.DOScale(originalScale * spawnPopScale, spawnPopDuration * 0.6f)
                .SetEase(spawnPopEase));
            seq.Append(transform.DOScale(originalScale, spawnPopDuration * 0.4f));
            seq.OnComplete(() =>
            {
                transform.localScale = originalScale;
                activeAnimation = null;
            });
            activeAnimation = seq;
        }

        /// <summary>
        /// Animate ingredient from a source point (e.g., van) to its spot on the container.
        /// </summary>
        public void AnimateToSpot(Vector3 fromPosition, Transform spotTransform, float duration, System.Action onComplete = null)
        {
            CompleteActiveAnimation(false);
            transform.position = fromPosition;
            Vector3 originalScale = GetRestingLocalScale();
            transform.localScale = Vector3.zero;

            Sequence seq = DOTween.Sequence().SetTarget(transform);
            seq.Append(transform.DOScale(originalScale, duration * 0.3f)
                .SetEase(Ease.OutBack));
            seq.Join(transform.DOJump(spotTransform.position, 1f, 1, duration)
                .SetEase(Ease.OutQuad));
            seq.Join(transform.DORotate(spotTransform.eulerAngles, duration));
            seq.OnComplete(() =>
            {
                transform.localScale = originalScale;
                activeAnimation = null;
                onComplete?.Invoke();
            });
            activeAnimation = seq;
        }

        public void OnPickedUp(Transform carryPoint)
        {
            isPickedUp = true;
            CompleteActiveAnimation(true);
            // Parenting and jump animation handled by RTPlayerCarryController
        }

        public void OnDropped()
        {
            isPickedUp = false;
            // Un-parent handled by the receiving container
        }

        public void OnDisposed()
        {
            CompleteActiveAnimation(false);
            transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() => Destroy(gameObject));
        }

        public void CompleteActiveAnimation(bool snapToRestingScale)
        {
            if (activeAnimation != null && activeAnimation.IsActive())
                activeAnimation.Kill(snapToRestingScale);

            activeAnimation = null;
            DOTween.Kill(transform, snapToRestingScale);

            if (snapToRestingScale)
                transform.localScale = GetRestingLocalScale();
        }

        private Vector3 GetRestingLocalScale()
        {
            if (!hasRestingLocalScale)
                RememberRestingScale(transform.localScale);

            return restingLocalScale;
        }

        private Vector3 GetRestingWorldScale()
        {
            if (!hasRestingLocalScale)
                RememberRestingScale(transform.localScale);

            return restingWorldScale;
        }

        private void RememberRestingScale(Vector3 scale)
        {
            if (scale.sqrMagnitude <= 0.0001f)
                return;

            restingLocalScale = scale;
            restingWorldScale = transform.lossyScale.sqrMagnitude > 0.0001f ? transform.lossyScale : scale;
            hasRestingLocalScale = true;
        }
    }
}
