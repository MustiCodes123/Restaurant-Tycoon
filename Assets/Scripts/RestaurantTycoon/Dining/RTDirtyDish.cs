using UnityEngine;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// A dirty dish left on a table after a customer finishes eating.
    /// Implements IRTCarryable so the player can pick it up and carry it to the garbage bin.
    /// </summary>
    public class RTDirtyDish : MonoBehaviour, IRTCarryable
    {
        [Header("Animation")]
        [SerializeField] private float spawnPopScale = 1.2f;
        [SerializeField] private float spawnPopDuration = 0.3f;
        [SerializeField] private Ease spawnPopEase = Ease.OutBack;

        private RTDiningTable sourceTable;
        private bool isPickedUp;

        public CarryableType CarryType => CarryableType.Garbage;
        public GameObject GameObject => gameObject;

        public void Initialize(RTDiningTable table)
        {
            sourceTable = table;
            PlaySpawnAnimation();
        }

        private void PlaySpawnAnimation()
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
