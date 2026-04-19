using UnityEngine;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Player-operated cooking spot. When the player stands here and there are
    /// ingredients in the input container (and output has room), the player cooks.
    /// Replaces the NPC RTCook with manual player interaction.
    /// Follows the same CanServe / StartService / CompleteService pattern as RTCashier.
    /// </summary>
    public class RTCookingSpot : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RTCookInputContainer inputContainer;
        [SerializeField] private RTItemOutputContainer outputContainer;

        [Header("Cook Point")]
        [Tooltip("Where the ingredient animates TO when cooking starts")]
        [SerializeField] private Transform cookPoint;

        [Header("Cooking")]
        [SerializeField] private float cookDuration = 2f;

        [Header("Finished Item")]
        [SerializeField] private GameObject finishedItemPrefab;

        [Header("Bounce Animation")]
        [SerializeField] private float outputBounceHeight = 0.5f;
        [SerializeField] private float outputBounceDuration = 0.3f;

        private bool isBeingServiced = false;

        /// <summary>
        /// True when there's an ingredient to cook and room in the output.
        /// RTPlayerController checks this each frame (same pattern as RTCashier.CanServe).
        /// </summary>
        public bool CanCook
        {
            get
            {
                if (isBeingServiced) return false;
                bool hasInput = inputContainer != null && inputContainer.HasIngredient;
                bool hasSpace = outputContainer != null && !outputContainer.IsFull;
                return hasInput && hasSpace;
            }
        }

        public float CookDuration => cookDuration;

        /// <summary>
        /// Called by RTPlayerController when the player stops on the spot and CanCook is true.
        /// </summary>
        public void StartCooking()
        {
            isBeingServiced = true;
        }

        /// <summary>
        /// Called by RTPlayerController when the player moves away before cooking finishes.
        /// Resets without consuming an ingredient.
        /// </summary>
        public void CancelCooking()
        {
            isBeingServiced = false;
        }

        /// <summary>
        /// Called by RTPlayerController after the radial progress finishes.
        /// Takes one ingredient from input, destroys it, spawns a finished item
        /// that bounces to the output container.
        /// </summary>
        public void CompleteCooking()
        {
            isBeingServiced = false;

            if (inputContainer == null || outputContainer == null) return;

            // Take ingredient from input (FIFO)
            RTIngredient ingredient = inputContainer.TakeIngredient();
            if (ingredient == null) return;

            // Destroy the consumed ingredient
            Destroy(ingredient.gameObject);

            // Spawn finished item at cook point
            SpawnFinishedItem();
        }

        private void SpawnFinishedItem()
        {
            if (finishedItemPrefab == null)
            {
                Debug.LogError("[RTCookingSpot] finishedItemPrefab is not assigned!");
                return;
            }

            Transform targetSlot = outputContainer.GetNextEmptySlot();
            if (targetSlot == null)
            {
                Debug.LogWarning("[RTCookingSpot] Output container has no empty slot!");
                return;
            }

            Vector3 spawnPos = cookPoint != null ? cookPoint.position : transform.position;
            GameObject obj = Instantiate(finishedItemPrefab, spawnPos, Quaternion.identity);
            RTFinishedItem finishedItem = obj.GetComponent<RTFinishedItem>();

            if (finishedItem == null)
            {
                Debug.LogError("[RTCookingSpot] finishedItemPrefab is missing RTFinishedItem component!");
                Destroy(obj);
                return;
            }

            // Reserve the slot
            outputContainer.AddItem(finishedItem);

            // Bounce to output slot
            obj.transform.DOJump(targetSlot.position, outputBounceHeight, 1, outputBounceDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    obj.transform.position = targetSlot.position;
                    obj.transform.rotation = targetSlot.rotation;
                });

            finishedItem.PlaySpawnAnimation();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SoundEffect.CookingDone);
        }
    }
}
