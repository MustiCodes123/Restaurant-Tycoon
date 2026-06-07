using UnityEngine;
using System.Collections;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Cook NPC that stays in place. Watches the input container for ingredients,
    /// takes one, plays cooking animation with RadialProgressUI, then produces
    /// a finished item that bounces to the output container.
    /// </summary>
    public class RTCook : MonoBehaviour, IUpgradeableStaff
    {
        [Header("References")]
        [SerializeField] private RTCookInputContainer inputContainer;
        [SerializeField] private RTItemOutputContainer outputContainer;

        [Header("Cook Point")]
        [Tooltip("Where the ingredient animates TO when the cook grabs it (cook's hands/station)")]
        [SerializeField] private Transform cookPoint;

        [Header("Cooking")]
        [SerializeField] private float cookDuration = 2f;
        [SerializeField] private float checkInterval = 0.3f;

        [Header("Finished Item")]
        [SerializeField] private GameObject finishedItemPrefab;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string servingParam = "IsServing";

        [Header("Radial Progress UI")]
        [Tooltip("RadialProgressUI as a child of the cook")]
        [SerializeField] private RadialProgressUI radialProgressUI;

        [Header("Bounce Animation")]
        [SerializeField] private float ingredientBounceHeight = 0.5f;
        [SerializeField] private float ingredientBounceDuration = 0.3f;
        [SerializeField] private float outputBounceHeight = 0.5f;
        [SerializeField] private float outputBounceDuration = 0.3f;

        private bool isCooking = false;
        private RTIngredient currentIngredient;
        private Coroutine cookLoopCoroutine;

        public bool IsCooking => isCooking;

        /// <summary>Reduces the cook time. Called by RTStaffUpgrade when an upgrade is purchased.</summary>
        public void SetUpgradedDuration(float newDuration)
        {
            cookDuration = Mathf.Max(0.1f, newDuration);
            if (radialProgressUI != null)
                radialProgressUI.SetFillDuration(cookDuration);
            Debug.Log($"[RTCook] Cook duration upgraded to {cookDuration}s");
        }

        private void Start()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (radialProgressUI == null)
                radialProgressUI = GetComponentInChildren<RadialProgressUI>();

            // Set cook duration on the radial UI
            if (radialProgressUI != null)
                radialProgressUI.SetFillDuration(cookDuration);

            // Auto-find containers if not assigned
            if (inputContainer == null)
                inputContainer = FindObjectOfType<RTCookInputContainer>();
            if (outputContainer == null)
                outputContainer = FindObjectOfType<RTItemOutputContainer>();

            Debug.Log($"[RTCook] Start - inputContainer: {(inputContainer != null ? inputContainer.name + " (ID:" + inputContainer.GetInstanceID() + ")" : "NULL")}, outputContainer: {(outputContainer != null ? outputContainer.name + " (ID:" + outputContainer.GetInstanceID() + ")" : "NULL")}, animator: {(animator != null)}, radialUI: {(radialProgressUI != null)}, cookPoint: {(cookPoint != null)}, finishedItemPrefab: {(finishedItemPrefab != null)}");

            // Subscribe to input container events for immediate response
            if (inputContainer != null)
                inputContainer.OnIngredientAdded += OnIngredientAvailable;

            // Start the cook loop
            cookLoopCoroutine = StartCoroutine(CookLoop());
        }

        private void OnDestroy()
        {
            if (inputContainer != null)
                inputContainer.OnIngredientAdded -= OnIngredientAvailable;
        }

        private void OnIngredientAvailable()
        {
            Debug.Log($"[RTCook] OnIngredientAvailable fired! isCooking: {isCooking}");
            // If not already cooking and loop isn't running, restart it
            if (!isCooking && cookLoopCoroutine == null)
            {
                cookLoopCoroutine = StartCoroutine(CookLoop());
            }
        }

        private IEnumerator CookLoop()
        {
            Debug.Log("[RTCook] CookLoop started");
            while (true)
            {
                // Wait until we're not cooking
                while (isCooking)
                {
                    yield return null;
                }

                bool hasInput = inputContainer != null && inputContainer.HasIngredient;
                bool hasOutputSpace = outputContainer != null && !outputContainer.IsFull;

                if (hasInput && hasOutputSpace)
                {
                    Debug.Log($"[RTCook] Starting to cook! Input has: {inputContainer.StoredCount}, Output space: {outputContainer.SlotCount - outputContainer.StoredCount}");
                    yield return StartCoroutine(CookOneItem());
                }
                else
                {
                    Debug.Log($"[RTCook] Waiting... hasInput: {hasInput}, hasOutputSpace: {hasOutputSpace}, inputStored: {(inputContainer != null ? inputContainer.StoredCount.ToString() : "N/A")}, outputFull: {(outputContainer != null ? outputContainer.IsFull.ToString() : "N/A")}, outputSlots: {(outputContainer != null ? outputContainer.SlotCount.ToString() : "N/A")}, outputStored: {(outputContainer != null ? outputContainer.StoredCount.ToString() : "N/A")}");
                    yield return new WaitForSeconds(checkInterval);
                }
            }
        }

        private IEnumerator CookOneItem()
        {
            isCooking = true;
            Debug.Log("[RTCook] CookOneItem started");

            // 1. Take ingredient from input container
            currentIngredient = inputContainer.TakeIngredient();
            if (currentIngredient == null)
            {
                Debug.LogWarning("[RTCook] TakeIngredient returned null!");
                isCooking = false;
                yield break;
            }
            Debug.Log($"[RTCook] Took ingredient: {currentIngredient.name}");

            // 2. Save scale, kill tweens, and animate ingredient bouncing to cook point
            Vector3 savedScale = currentIngredient.transform.lossyScale;
            DOTween.Kill(currentIngredient.transform, true);
            currentIngredient.transform.SetParent(null);
            currentIngredient.transform.localScale = savedScale;
            bool bounceComplete = false;

            currentIngredient.transform
                .DOJump(cookPoint != null ? cookPoint.position : transform.position, ingredientBounceHeight, 1, ingredientBounceDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => bounceComplete = true);

            while (!bounceComplete)
                yield return null;

            // 3. Hide the ingredient (it's being "cooked")
            currentIngredient.gameObject.SetActive(false);

            // 4. Start cooking animation + radial progress
            SetServing(true);

            if (radialProgressUI != null)
            {
                radialProgressUI.SetFillDuration(cookDuration);
                radialProgressUI.StartProgress();
            }

            // 5. Wait for cook duration
            yield return new WaitForSeconds(cookDuration);

            // 6. Stop cooking animation + radial progress
            SetServing(false);

            if (radialProgressUI != null)
            {
                radialProgressUI.StopProgress();
            }

            // 7. Destroy the consumed ingredient
            if (currentIngredient != null)
            {
                Destroy(currentIngredient.gameObject);
                currentIngredient = null;
            }

            // 8. Spawn finished item and bounce to output container
            if (outputContainer != null && !outputContainer.IsFull)
            {
                SpawnFinishedItem();
            }

            isCooking = false;
        }

        private void SpawnFinishedItem()
        {
            if (finishedItemPrefab == null)
            {
                Debug.LogError("[RTCook] finishedItemPrefab is not assigned!");
                return;
            }

            // Find the target slot on the output container
            Transform targetSlot = outputContainer.GetNextEmptySlot();
            if (targetSlot == null)
            {
                Debug.LogWarning("[RTCook] Output container has no empty slot!");
                return;
            }

            // Spawn at cook point
            Vector3 spawnPos = cookPoint != null ? cookPoint.position : transform.position;
            GameObject obj = Instantiate(finishedItemPrefab, spawnPos, Quaternion.identity);
            RTFinishedItem finishedItem = obj.GetComponent<RTFinishedItem>();

            if (finishedItem == null)
            {
                Debug.LogError("[RTCook] finishedItemPrefab is missing RTFinishedItem component!");
                Destroy(obj);
                return;
            }

            // Register on output container before animation (reserves the slot)
            outputContainer.AddItem(finishedItem);

            // Bounce animate to output slot
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

            Debug.Log($"[RTCook] Finished item produced and sent to output. Output: {outputContainer.StoredCount}/{outputContainer.SlotCount}");
        }

        private void SetServing(bool serving)
        {
            if (animator != null)
            {
                animator.SetBool(servingParam, serving);
            }
        }
    }
}
