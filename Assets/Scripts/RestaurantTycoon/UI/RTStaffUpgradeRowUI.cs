using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestaurantTycoon
{
    public class RTStaffUpgradeRowUI : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI upgradeButtonText;
        [SerializeField] private TextMeshProUGUI adButtonText;

        [Header("Avatar")]
        [SerializeField] private Image avatarImage;

        [Header("Buttons")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button watchAdButton;

        [Header("Optional State Visuals")]
        [SerializeField] private GameObject lockedRoot;
        [SerializeField] private GameObject maxedRoot;
        [SerializeField] private CanvasGroup rowCanvasGroup;
        [SerializeField] private float lockedAlpha = 0.55f;

        private RTStaffUpgrade upgrade;
        private RTStaffUpgradePanelUI owner;

        public void Setup(RTStaffUpgrade staffUpgrade, RTStaffUpgradePanelUI panel)
        {
            if (upgrade != null)
                upgrade.OnUpgradeChanged -= Refresh;

            upgrade = staffUpgrade;
            owner = panel;

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }

            if (watchAdButton != null)
            {
                watchAdButton.onClick.RemoveListener(OnWatchAdClicked);
                watchAdButton.onClick.AddListener(OnWatchAdClicked);
            }

            if (upgrade != null)
                upgrade.OnUpgradeChanged += Refresh;

            Refresh();
        }

        private void OnDestroy()
        {
            if (upgrade != null)
                upgrade.OnUpgradeChanged -= Refresh;

            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OnUpgradeClicked);

            if (watchAdButton != null)
                watchAdButton.onClick.RemoveListener(OnWatchAdClicked);
        }

        public void Refresh()
        {
            if (upgrade == null)
            {
                gameObject.SetActive(false);
                return;
            }

            upgrade.EnsureStateLoaded();

            if (titleText != null)
                titleText.text = upgrade.UpgradeId;

            if (avatarImage != null)
            {
                Sprite avatarSprite = upgrade.UpgradeData != null ? upgrade.UpgradeData.AvatarSprite : null;
                avatarImage.sprite = avatarSprite;
                avatarImage.enabled = avatarSprite != null;
            }

            bool canUpgrade = upgrade.CanUpgrade;
            bool unlocked = upgrade.IsInteractionAvailable;
            bool maxed = upgrade.IsMaxed;

            if (descriptionText != null)
                descriptionText.text = BuildDescription();

            if (statusText != null)
                statusText.text = BuildStatusText(maxed, unlocked);

            if (upgradeButtonText != null)
                upgradeButtonText.text = canUpgrade && upgrade.NextLevel != null ? $"${upgrade.NextLevel.cost}" : "Done";

            if (adButtonText != null)
                adButtonText.text = canUpgrade ? "Free" : "Done";

            if (upgradeButton != null)
                upgradeButton.interactable = canUpgrade && unlocked && upgrade.CanAffordNextLevel;

            if (watchAdButton != null)
                watchAdButton.interactable = canUpgrade && unlocked;

            if (lockedRoot != null)
                lockedRoot.SetActive(canUpgrade && !unlocked);

            if (maxedRoot != null)
                maxedRoot.SetActive(maxed);

            if (rowCanvasGroup != null)
                rowCanvasGroup.alpha = canUpgrade && !unlocked ? lockedAlpha : 1f;
        }

        private string BuildDescription()
        {
            if (!upgrade.CanUpgrade || upgrade.NextLevel == null)
                return "All upgrades purchased.";

            var level = upgrade.NextLevel;
            var builder = new StringBuilder();

            if (level.newDuration > 0f)
                builder.AppendLine($"Duration {level.newDuration:0.##}s");

            if (level.newMoveSpeed > 0f)
                builder.AppendLine($"Speed {level.newMoveSpeed:0.##}");

            if (level.newCarryCapacity > 0)
                builder.AppendLine($"Carry {level.newCarryCapacity}");

            string description = builder.ToString().TrimEnd();
            return string.IsNullOrEmpty(description) ? level.upgradeName : description;
        }

        private string BuildStatusText(bool maxed, bool unlocked)
        {
            if (maxed)
                return "Max Level";

            if (!unlocked)
            {
                if (upgrade.isActiveAndEnabled)
                    return $"Unlocks at Level {upgrade.RequiredPlayerLevel}";

                return "Locked";
            }

            if (!upgrade.CanAffordNextLevel)
                return "Not enough cash";

            return "Available";
        }

        private void OnUpgradeClicked()
        {
            if (upgrade == null)
                return;

            upgrade.TryPurchaseNextLevelWithMoney();
            owner?.RefreshRows();
        }

        private void OnWatchAdClicked()
        {
            if (upgrade == null)
                return;

            SetButtonsInteractable(false);
            upgrade.RequestRewardedAdUpgrade(() =>
            {
                SetButtonsInteractable(true);
                owner?.RefreshRows();
            });
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (upgradeButton != null)
                upgradeButton.interactable = interactable;

            if (watchAdButton != null)
                watchAdButton.interactable = interactable;
        }
    }
}
