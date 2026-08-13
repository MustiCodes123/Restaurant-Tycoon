using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    public enum RTRewardedAdRewardType
    {
        RefillShelfStock,
        RefillIngredientStorage,
        DoubleMoney,
        CharacterSpeedBoost
    }

    #pragma warning disable 0649
    /// <summary>
    /// Controls rewarded-ad reward icons on the gameplay HUD and applies the
    /// earned rewards after the rewarded ad reports success.
    /// </summary>
    public class RTRewardedAdSystem : MonoBehaviour
    {
        [System.Serializable]
        private class RewardIconBinding
        {
            public RTRewardedAdRewardType rewardType;
            public GameObject root;
            public Button button;
            public RectTransform shakeTarget;
        }

        [System.Serializable]
        private class ShelfRefillTarget
        {
            [Tooltip("Optional. If assigned, this shelf refills only while this counter GameObject is active/unlocked.")]
            public RTCustomerCounter availabilityCounter;
            public RTItemOutputContainer outputContainer;
            public GameObject finishedItemPrefab;
        }

        [System.Serializable]
        private class IngredientRefillTarget
        {
            [Tooltip("The stall counter that represents whether this cook input is unlocked.")]
            public RTCustomerCounter availabilityCounter;
            [Tooltip("Used to infer the stall, cook input, and ingredient prefab when those fields are not assigned.")]
            public RTIngredientContainer ingredientContainer;
            [Tooltip("The cook input container to refill. If empty, this is inferred from the stall that owns Ingredient Container.")]
            public RTCookInputContainer cookInputContainer;
            [Tooltip("Optional override. If empty, the prefab from Ingredient Container is used.")]
            public GameObject ingredientPrefab;
        }

        [Header("Reward Icons")]
        [SerializeField] private List<RewardIconBinding> rewardIcons = new List<RewardIconBinding>();
        [SerializeField] private float randomRewardInterval = 120f;
        [SerializeField] private float randomRewardVisibleDuration = 30f;

        [Header("Icon Animation")]
        [SerializeField] private float showScaleDuration = 0.18f;
        [SerializeField] private float hideScaleDuration = 0.12f;
        [SerializeField] private float shakeDuration = 0.22f;
        [SerializeField] private float shakeStrength = 8f;
        [SerializeField] private float shakeInterval = 1.1f;

        [Header("Reward Confirmation")]
        [SerializeField] private GameObject confirmationPanelRoot;
        [SerializeField] private Button confirmationWatchAdButton;
        [SerializeField] private Button confirmationCancelButton;
        [SerializeField] private TextMeshProUGUI confirmationDescriptionText;
        [SerializeField] private string refillShelfDescription = "Watch an ad to refill shelf stock.";
        [SerializeField] private string refillIngredientDescription = "Watch an ad to refill the cook input.";
        [SerializeField] private string doubleMoneyDescription = "Watch an ad to earn 2x money for 3 minutes.";
        [SerializeField] private string speedBoostDescription = "Watch an ad to boost all characters for 30 seconds.";

        [Header("Reward Targets")]
        [Tooltip("Output shelves to refill. Assign the shelf and the finished item prefab for that shelf.")]
        [SerializeField] private List<ShelfRefillTarget> shelfRefillTargets = new List<ShelfRefillTarget>();
        [Tooltip("Cook input containers to refill. Assign the matching stall counter so locked stalls stay empty.")]
        [SerializeField] private List<IngredientRefillTarget> ingredientRefillTargets = new List<IngredientRefillTarget>();
        [Tooltip("Legacy fallback. These ingredient containers are used to infer their stall cook input and ingredient prefab.")]
        [SerializeField] private List<RTIngredientContainer> ingredientContainers = new List<RTIngredientContainer>();
        [SerializeField] private bool autoFindMissingRewardTargets = true;

        [Header("Timed Rewards")]
        [SerializeField] private float doubleMoneyDuration = 180f;
        [SerializeField] private float moneyMultiplierAmount = 2f;
        [SerializeField] private float speedBoostDuration = 30f;
        [SerializeField] private float speedBoostMultiplier = 2f;

        private readonly Dictionary<RewardIconBinding, Tween> shakeTweens = new Dictionary<RewardIconBinding, Tween>();
        private RewardIconBinding activeRewardIcon;
        private Coroutine randomRewardCoroutine;
        private Coroutine doubleMoneyCoroutine;
        private Coroutine speedBoostCoroutine;
        private RTRewardedAdRewardType pendingRewardType;
        private bool hasPendingReward;

        public static float MoneyMultiplier { get; private set; } = 1f;
        public static float CharacterSpeedMultiplier { get; private set; } = 1f;

        public static int ApplyMoneyMultiplier(int amount)
        {
            return Mathf.Max(0, Mathf.RoundToInt(amount * MoneyMultiplier));
        }

        private void Awake()
        {
            CacheMissingRewardTargets();
            ConfigureButtons();
            ConfigureConfirmationPanel();
            HideAllRewardsImmediate();
            HideConfirmationPanel();
        }

        private void OnEnable()
        {
            randomRewardCoroutine = StartCoroutine(RandomRewardLoop());

            if (AdsManager.Instance != null)
                AdsManager.Instance.LoadRewardedAd();
        }

        private void OnDisable()
        {
            if (randomRewardCoroutine != null)
            {
                StopCoroutine(randomRewardCoroutine);
                randomRewardCoroutine = null;
            }

            StopAllIconAnimations();
            HideAllRewardsImmediate();
            HideConfirmationPanel();
        }

        private void OnDestroy()
        {
            if (doubleMoneyCoroutine != null)
            {
                StopCoroutine(doubleMoneyCoroutine);
                MoneyMultiplier = 1f;
            }

            if (speedBoostCoroutine != null)
            {
                StopCoroutine(speedBoostCoroutine);
                CharacterSpeedMultiplier = 1f;
                ApplySpeedMultiplierToActiveCharacters();
            }
        }

        public void RequestShelfStockReward()
        {
            RequestReward(RTRewardedAdRewardType.RefillShelfStock);
        }

        public void RequestIngredientStorageReward()
        {
            RequestReward(RTRewardedAdRewardType.RefillIngredientStorage);
        }

        public void RequestDoubleMoneyReward()
        {
            RequestReward(RTRewardedAdRewardType.DoubleMoney);
        }

        public void RequestSpeedBoostReward()
        {
            RequestReward(RTRewardedAdRewardType.CharacterSpeedBoost);
        }

        private IEnumerator RandomRewardLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(randomRewardInterval);

                ShowRandomReward();

                yield return new WaitForSeconds(randomRewardVisibleDuration);
                HideActiveReward();
            }
        }

        private void ConfigureButtons()
        {
            foreach (var icon in rewardIcons)
            {
                if (icon == null || icon.button == null)
                    continue;

                RTRewardedAdRewardType rewardType = icon.rewardType;
                icon.button.onClick.AddListener(() => RequestReward(rewardType));
            }
        }

        private void RequestReward(RTRewardedAdRewardType rewardType)
        {
            ShowConfirmationPanel(rewardType);
        }

        private void ExecutePendingReward()
        {
            if (!hasPendingReward)
                return;

            RTRewardedAdRewardType rewardType = pendingRewardType;
            HideConfirmationPanel();
            RequestRewardedAd(rewardType);
        }

        private void RequestRewardedAd(RTRewardedAdRewardType rewardType)
        {
            if (AdsManager.Instance == null)
            {
                Debug.LogWarning("[RTRewardedAdSystem] AdsManager not found in scene.");
                return;
            }

            AdsManager.Instance.ShowRewardedAd(
                onRewardEarned: _ => GrantReward(rewardType),
                onClosed: () => AdsManager.Instance.LoadRewardedAd());
        }

        private void ConfigureConfirmationPanel()
        {
            if (confirmationWatchAdButton != null)
            {
                confirmationWatchAdButton.onClick.RemoveListener(ExecutePendingReward);
                confirmationWatchAdButton.onClick.AddListener(ExecutePendingReward);
            }

            if (confirmationCancelButton != null)
            {
                confirmationCancelButton.onClick.RemoveListener(HideConfirmationPanel);
                confirmationCancelButton.onClick.AddListener(HideConfirmationPanel);
            }
        }

        private void ShowConfirmationPanel(RTRewardedAdRewardType rewardType)
        {
            pendingRewardType = rewardType;
            hasPendingReward = true;

            if (confirmationDescriptionText != null)
                confirmationDescriptionText.text = GetConfirmationDescription(rewardType);

            if (confirmationPanelRoot != null)
            {
                confirmationPanelRoot.SetActive(true);
                return;
            }

            ExecutePendingReward();
        }

        private void HideConfirmationPanel()
        {
            hasPendingReward = false;

            if (confirmationPanelRoot != null)
                confirmationPanelRoot.SetActive(false);
        }

        private string GetConfirmationDescription(RTRewardedAdRewardType rewardType)
        {
            switch (rewardType)
            {
                case RTRewardedAdRewardType.RefillShelfStock:
                    return refillShelfDescription;
                case RTRewardedAdRewardType.RefillIngredientStorage:
                    return refillIngredientDescription;
                case RTRewardedAdRewardType.DoubleMoney:
                    return doubleMoneyDescription;
                case RTRewardedAdRewardType.CharacterSpeedBoost:
                    return speedBoostDescription;
                default:
                    return "Watch an ad to claim this reward.";
            }
        }

        private void GrantReward(RTRewardedAdRewardType rewardType)
        {
            switch (rewardType)
            {
                case RTRewardedAdRewardType.RefillShelfStock:
                    RefillShelfStock();
                    break;
                case RTRewardedAdRewardType.RefillIngredientStorage:
                    RefillIngredientStorage();
                    break;
                case RTRewardedAdRewardType.DoubleMoney:
                    ActivateDoubleMoney();
                    break;
                case RTRewardedAdRewardType.CharacterSpeedBoost:
                    ActivateSpeedBoost();
                    break;
            }

            HideActiveReward();
        }

        private void RefillShelfStock()
        {
            foreach (var target in shelfRefillTargets)
            {
                if (target == null || target.outputContainer == null)
                    continue;

                if (!IsShelfTargetUnlocked(target))
                    continue;

                target.outputContainer.RefillAllEmptySlots(target.finishedItemPrefab);
            }
        }

        private void RefillIngredientStorage()
        {
            foreach (var target in ingredientRefillTargets)
            {
                if (target == null)
                    continue;

                if (!IsIngredientTargetUnlocked(target))
                    continue;

                RTCookInputContainer cookInput = ResolveCookInputContainer(target);
                GameObject ingredientPrefab = ResolveIngredientPrefab(target);
                RefillCookInput(cookInput, ingredientPrefab, target.ingredientContainer);
            }

            foreach (var container in ingredientContainers)
            {
                if (container == null || !IsIngredientContainerUnlocked(container))
                    continue;

                RTStall stall = FindStallForIngredientContainer(container);
                RefillCookInput(stall != null ? stall.CookInputContainer : null, container.IngredientPrefab, container);
            }
        }

        private bool IsShelfTargetUnlocked(ShelfRefillTarget target)
        {
            if (target.availabilityCounter != null)
                return IsCounterUnlocked(target.availabilityCounter);

            RTCustomerCounter inferredCounter = FindCounterForFinishedItemPrefab(target.finishedItemPrefab);
            if (inferredCounter != null)
                return IsCounterUnlocked(inferredCounter);

            return target.outputContainer.gameObject.activeInHierarchy;
        }

        private bool IsIngredientTargetUnlocked(IngredientRefillTarget target)
        {
            if (target.availabilityCounter != null)
                return IsCounterUnlocked(target.availabilityCounter);

            if (target.ingredientContainer != null)
                return IsIngredientContainerUnlocked(target.ingredientContainer);

            RTStall stall = FindStallForCookInputContainer(target.cookInputContainer);
            if (stall != null && stall.CustomerCounter != null)
                return IsCounterUnlocked(stall.CustomerCounter);

            return target.cookInputContainer != null && target.cookInputContainer.gameObject.activeInHierarchy;
        }

        private bool IsIngredientContainerUnlocked(RTIngredientContainer container)
        {
            RTStall stall = FindStallForIngredientContainer(container);
            if (stall != null && stall.CustomerCounter != null)
                return IsCounterUnlocked(stall.CustomerCounter);

            return container != null && container.gameObject.activeInHierarchy;
        }

        private bool IsCounterUnlocked(RTCustomerCounter counter)
        {
            return counter != null && counter.gameObject.activeInHierarchy;
        }

        private RTCustomerCounter FindCounterForFinishedItemPrefab(GameObject finishedItemPrefab)
        {
            if (finishedItemPrefab == null)
                return null;

            RTFinishedItem item = finishedItemPrefab.GetComponent<RTFinishedItem>();
            if (item == null || item.ItemType == null)
                return null;

            RTCustomerCounter inactiveMatch = null;
            foreach (var counter in FindObjectsByType<RTCustomerCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (counter == null || counter.AcceptedItemType != item.ItemType)
                    continue;

                if (counter.gameObject.activeInHierarchy)
                    return counter;

                inactiveMatch = counter;
            }

            return inactiveMatch;
        }

        private RTStall FindStallForIngredientContainer(RTIngredientContainer container)
        {
            if (container == null)
                return null;

            foreach (var stall in FindObjectsByType<RTStall>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (stall != null && stall.IngredientContainer == container)
                    return stall;
            }

            return null;
        }

        private RTStall FindStallForCookInputContainer(RTCookInputContainer cookInputContainer)
        {
            if (cookInputContainer == null)
                return null;

            foreach (var stall in FindObjectsByType<RTStall>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (stall != null && stall.CookInputContainer == cookInputContainer)
                    return stall;
            }

            return null;
        }

        private RTCookInputContainer ResolveCookInputContainer(IngredientRefillTarget target)
        {
            if (target == null)
                return null;

            if (target.cookInputContainer != null)
                return target.cookInputContainer;

            RTStall stall = FindStallForIngredientContainer(target.ingredientContainer);
            return stall != null ? stall.CookInputContainer : null;
        }

        private GameObject ResolveIngredientPrefab(IngredientRefillTarget target)
        {
            if (target == null)
                return null;

            if (target.ingredientPrefab != null)
                return target.ingredientPrefab;

            return target.ingredientContainer != null ? target.ingredientContainer.IngredientPrefab : null;
        }

        private void RefillCookInput(RTCookInputContainer cookInputContainer, GameObject ingredientPrefab, RTIngredientContainer sourceContainer)
        {
            if (cookInputContainer == null || ingredientPrefab == null || !cookInputContainer.gameObject.activeInHierarchy)
                return;

            for (int i = 0; i < cookInputContainer.SlotCount && !cookInputContainer.IsFull; i++)
            {
                Vector3 spawnPosition = sourceContainer != null
                    ? sourceContainer.transform.position + Vector3.up * 1.5f
                    : cookInputContainer.transform.position + Vector3.up * 1.5f;

                GameObject obj = Instantiate(ingredientPrefab, spawnPosition, Quaternion.identity);
                RTIngredient ingredient = obj.GetComponent<RTIngredient>();
                if (ingredient == null)
                {
                    Debug.LogWarning($"[RTRewardedAdSystem] Ingredient refill prefab '{ingredientPrefab.name}' is missing RTIngredient.");
                    Destroy(obj);
                    return;
                }

                if (!cookInputContainer.ReceiveIngredient(ingredient))
                {
                    Destroy(obj);
                    return;
                }
            }
        }

        private void ActivateDoubleMoney()
        {
            if (doubleMoneyCoroutine != null)
                StopCoroutine(doubleMoneyCoroutine);

            doubleMoneyCoroutine = StartCoroutine(DoubleMoneyRoutine());
        }

        private IEnumerator DoubleMoneyRoutine()
        {
            MoneyMultiplier = moneyMultiplierAmount;
            yield return new WaitForSeconds(doubleMoneyDuration);
            MoneyMultiplier = 1f;
            doubleMoneyCoroutine = null;
        }

        private void ActivateSpeedBoost()
        {
            if (speedBoostCoroutine != null)
                StopCoroutine(speedBoostCoroutine);

            speedBoostCoroutine = StartCoroutine(SpeedBoostRoutine());
        }

        private IEnumerator SpeedBoostRoutine()
        {
            CharacterSpeedMultiplier = speedBoostMultiplier;
            ApplySpeedMultiplierToActiveCharacters();

            yield return new WaitForSeconds(speedBoostDuration);

            CharacterSpeedMultiplier = 1f;
            ApplySpeedMultiplierToActiveCharacters();
            speedBoostCoroutine = null;
        }

        private void ApplySpeedMultiplierToActiveCharacters()
        {
            foreach (var customer in FindObjectsByType<RTCustomer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                customer.ApplyRewardSpeedMultiplier();

            foreach (var porter in FindObjectsByType<RTPorterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                porter.ApplyRewardSpeedMultiplier();

            foreach (var janitor in FindObjectsByType<RTJanitorController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                janitor.ApplyRewardSpeedMultiplier();
        }

        private void ShowRandomReward()
        {
            List<RewardIconBinding> validIcons = new List<RewardIconBinding>();
            foreach (var icon in rewardIcons)
            {
                if (icon != null && icon.root != null)
                    validIcons.Add(icon);
            }

            if (validIcons.Count == 0)
                return;

            ShowReward(validIcons[UnityEngine.Random.Range(0, validIcons.Count)]);
        }

        private void ShowReward(RewardIconBinding icon)
        {
            HideActiveReward();
            activeRewardIcon = icon;

            RectTransform target = GetShakeTarget(icon);
            icon.root.SetActive(true);

            if (target != null)
            {
                target.DOKill();
                target.localScale = Vector3.zero;
                target.DOScale(Vector3.one, showScaleDuration)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => StartIconShake(icon));
            }
            else
            {
                StartIconShake(icon);
            }
        }

        private void HideActiveReward()
        {
            if (activeRewardIcon == null)
                return;

            RewardIconBinding icon = activeRewardIcon;
            activeRewardIcon = null;

            StopIconShake(icon);
            RectTransform target = GetShakeTarget(icon);
            if (target != null)
            {
                target.DOKill();
                target.DOScale(Vector3.zero, hideScaleDuration)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => icon.root.SetActive(false));
            }
            else if (icon.root != null)
            {
                icon.root.SetActive(false);
            }
        }

        private void HideAllRewardsImmediate()
        {
            foreach (var icon in rewardIcons)
            {
                if (icon == null || icon.root == null)
                    continue;

                RectTransform target = GetShakeTarget(icon);
                if (target != null)
                {
                    target.DOKill();
                    target.localScale = Vector3.zero;
                }

                icon.root.SetActive(false);
            }

            activeRewardIcon = null;
        }

        private void StartIconShake(RewardIconBinding icon)
        {
            RectTransform target = GetShakeTarget(icon);
            if (target == null)
                return;

            StopIconShake(icon);

            Sequence sequence = DOTween.Sequence();
            sequence.Append(target.DOShakeAnchorPos(shakeDuration, shakeStrength, 10, 45f, false, true));
            sequence.AppendInterval(shakeInterval);
            sequence.SetLoops(-1, LoopType.Restart);
            shakeTweens[icon] = sequence;
        }

        private void StopIconShake(RewardIconBinding icon)
        {
            if (icon == null)
                return;

            if (shakeTweens.TryGetValue(icon, out Tween tween))
            {
                tween.Kill();
                shakeTweens.Remove(icon);
            }
        }

        private void StopAllIconAnimations()
        {
            foreach (var tween in shakeTweens.Values)
                tween?.Kill();

            shakeTweens.Clear();
        }

        private RectTransform GetShakeTarget(RewardIconBinding icon)
        {
            if (icon == null)
                return null;

            if (icon.shakeTarget != null)
                return icon.shakeTarget;

            return icon.root != null ? icon.root.GetComponent<RectTransform>() : null;
        }

        private void CacheMissingRewardTargets()
        {
            if (!autoFindMissingRewardTargets)
                return;

            if (ingredientContainers.Count == 0)
                ingredientContainers.AddRange(FindObjectsByType<RTIngredientContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }
    }
    #pragma warning restore 0649
}
