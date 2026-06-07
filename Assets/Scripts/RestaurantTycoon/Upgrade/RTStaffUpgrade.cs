using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// Manages upgrade state for one staff member (cook, porter, or cashier).
    /// Attach this alongside or near the staff GameObject, assign the
    /// RTStaffUpgradeData ScriptableObject and drag the staff MonoBehaviour
    /// into staffTarget. RTUpgradeSpot (child or reference) is shown/hidden
    /// from here.
    ///
    /// Designed to work with RTSceneObjectUnlock:
    ///   - OnEnable  → load saved state, apply upgrades, show spot if available.
    ///   - OnDisable → hide spot.
    /// </summary>
    public class RTStaffUpgrade : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("ScriptableObject that defines upgrade levels (cost, newDuration).")]
        [SerializeField] private RTStaffUpgradeData upgradeData;

        [Header("Staff Target")]
        [Tooltip("The staff MonoBehaviour to upgrade. Must be an RTCook, RTPorterController, " +
                 "or RTCashierCharacter (any MonoBehaviour that implements IUpgradeableStaff).")]
        [SerializeField] private MonoBehaviour staffTarget;

        [Header("Upgrade Spot")]
        [Tooltip("The RTUpgradeSpot that handles player interaction.")]
        [SerializeField] private RTUpgradeSpot upgradeSpot;

        // ── Runtime ───────────────────────────────────────────────────────────
        private int currentLevel = 0;
        private IUpgradeableStaff staffInterface;

        private string SaveKey => $"RTStaffUpgrade_{(upgradeData != null ? upgradeData.UpgradeId : gameObject.name)}";

        public int CurrentLevel => currentLevel;
        public bool CanUpgrade => upgradeData != null && currentLevel < upgradeData.MaxLevel;
        public RTStaffUpgradeData.UpgradeLevel NextLevel => upgradeData?.GetLevel(currentLevel);

        // ── Unity ─────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            // Resolve the staff interface once.
            if (staffInterface == null && staffTarget != null)
            {
                staffInterface = staffTarget as IUpgradeableStaff;
                if (staffInterface == null)
                    Debug.LogError($"[RTStaffUpgrade] '{staffTarget.name}' does not implement IUpgradeableStaff!");
            }

            if (upgradeData == null)
            {
                Debug.LogError("[RTStaffUpgrade] No RTStaffUpgradeData assigned!");
                return;
            }

            LoadState();
            ApplyCurrentUpgrade();
            CheckAvailability();
        }

        private void OnDisable()
        {
            if (upgradeSpot != null)
                upgradeSpot.Hide();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by RTUpgradeSpot when the player finishes paying.</summary>
        public void CompleteUpgrade()
        {
            if (!CanUpgrade) return;

            var level = upgradeData.GetLevel(currentLevel);
            if (level == null) return;

            // Spend money.
            if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendMoney(level.cost))
            {
                Debug.LogWarning("[RTStaffUpgrade] Not enough money to complete upgrade!");
                return;
            }

            currentLevel++;
            SaveState();

            Debug.Log($"[RTStaffUpgrade] Upgraded '{staffTarget?.name}' to level {currentLevel}. New duration: {level.newDuration}s");

            ApplyCurrentUpgrade();
            CheckAvailability();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SoundEffect.CustomerServed);
        }

        // ── Private ───────────────────────────────────────────────────────────

        /// <summary>Applies the most recently purchased upgrade duration to the staff.</summary>
        private void ApplyCurrentUpgrade()
        {
            if (staffInterface == null || currentLevel == 0) return;

            // The last purchased level is at index currentLevel - 1.
            var lastPurchased = upgradeData.GetLevel(currentLevel - 1);
            if (lastPurchased != null)
                staffInterface.SetUpgradedDuration(lastPurchased.newDuration);
        }

        private void CheckAvailability()
        {
            if (upgradeSpot == null) return;

            if (CanUpgrade)
                upgradeSpot.Show(this);
            else
                upgradeSpot.Hide();
        }

        private void LoadState()
        {
            currentLevel = PlayerPrefs.GetInt(SaveKey, 0);
            // Clamp in case the SO was edited after save.
            if (upgradeData != null)
                currentLevel = Mathf.Clamp(currentLevel, 0, upgradeData.MaxLevel);
        }

        private void SaveState()
        {
            PlayerPrefs.SetInt(SaveKey, currentLevel);
            PlayerPrefs.Save();
        }

        [ContextMenu("Reset Upgrade State")]
        private void ResetState()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            currentLevel = 0;
            ApplyCurrentUpgrade();
            CheckAvailability();
        }
    }
}
