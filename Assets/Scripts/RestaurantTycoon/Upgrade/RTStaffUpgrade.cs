using System;
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
        [Tooltip("The staff MonoBehaviour to upgrade. Must implement IUpgradeableStaff. " +
                 "Leave empty when upgrading a janitor — use Janitor Unlock instead.")]
        [SerializeField] private MonoBehaviour staffTarget;

        [Tooltip("Assign the RTJanitorUnlock that manages this janitor. " +
                 "The upgrade system will connect to the janitor automatically when it is spawned. " +
                 "Leave empty for non-janitor staff.")]
        [SerializeField] private RTJanitorUnlock janitorUnlock;

        [Header("Upgrade Spot")]
        [Tooltip("The RTUpgradeSpot that handles player interaction.")]
        [SerializeField] private RTUpgradeSpot upgradeSpot;
        [Tooltip("Enable only if you still want the old world-space upgrade spot to appear.")]
        [SerializeField] private bool useWorldUpgradeSpot = false;

        // ── Runtime ───────────────────────────────────────────────────────────
        private int currentLevel = 0;
        private IUpgradeableStaff staffInterface;
        private bool hasStarted = false; // guards against OnEnable running before Start
        private bool isRewardedUpgradePending;
        private bool hasLoadedState;

        private string SaveKey => $"RTStaffUpgrade_{(upgradeData != null ? upgradeData.UpgradeId : gameObject.name)}";

        public event Action OnUpgradeChanged;

        public RTStaffUpgradeData UpgradeData => upgradeData;
        public string UpgradeId => upgradeData != null ? upgradeData.UpgradeId : gameObject.name;
        public int CurrentLevel => currentLevel;
        public int MaxLevel => upgradeData != null ? upgradeData.MaxLevel : 0;
        public bool CanUpgrade => upgradeData != null && currentLevel < upgradeData.MaxLevel;
        public bool IsMaxed => upgradeData != null && currentLevel >= upgradeData.MaxLevel;
        public RTStaffUpgradeData.UpgradeLevel NextLevel => upgradeData?.GetLevel(currentLevel);
        public int RequiredPlayerLevel => NextLevel != null ? NextLevel.requiredPlayerLevel : 0;
        public bool IsUnlockedForCurrentPlayerLevel
        {
            get
            {
                if (!CanUpgrade || NextLevel == null) return false;
                int playerLevel = RTLevelManager.Instance != null ? RTLevelManager.Instance.CurrentLevel : 1;
                return playerLevel >= NextLevel.requiredPlayerLevel;
            }
        }
        public bool IsInteractionAvailable => isActiveAndEnabled && IsUnlockedForCurrentPlayerLevel;
        public bool CanAffordNextLevel =>
            CanUpgrade && CurrencyManager.Instance != null && CurrencyManager.Instance.CurrentMoney >= NextLevel.cost;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Start()
        {
            hasStarted = true;
            // RTLevelManager.Instance is guaranteed to exist by the time Start() runs
            // (all Awake() calls across the scene are complete). Subscribe here so we
            // never miss the initial OnLevelLoaded event.
            SubscribeToLevelEvents();
            CheckAvailability();
        }

        private void OnEnable()
        {
            // Janitor path: connect via RTJanitorUnlock instead of a direct drag-drop.
            if (janitorUnlock != null)
            {
                janitorUnlock.OnJanitorUnlocked -= OnJanitorSpawned;
                janitorUnlock.OnJanitorUnlocked += OnJanitorSpawned;

                // Janitor may already be unlocked and spawned (e.g. scene reload).
                if (janitorUnlock.IsUnlocked && janitorUnlock.SpawnedJanitor != null)
                    ConnectToStaff(janitorUnlock.SpawnedJanitor);
            }
            else if (staffInterface == null && staffTarget != null)
            {
                // Standard path: direct reference.
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

            // If Start() has already run (i.e. this is a re-enable after unlock),
            // re-subscribe and re-check now. Otherwise Start() will handle it.
            if (hasStarted)
            {
                SubscribeToLevelEvents();
                CheckAvailability();
            }
        }

        private void OnDisable()
        {
            if (janitorUnlock != null)
                janitorUnlock.OnJanitorUnlocked -= OnJanitorSpawned;

            UnsubscribeFromLevelEvents();

            // Remove the pending mission so it disappears from the UI when the
            // object is hidden (e.g. while locked via RTSceneObjectUnlock).
            RemoveCurrentMission();

            if (upgradeSpot != null)
                upgradeSpot.Hide();
        }

        private void OnLevelChanged(int _) => CheckAvailability();

        private void OnJanitorSpawned()
        {
            if (janitorUnlock?.SpawnedJanitor != null)
                ConnectToStaff(janitorUnlock.SpawnedJanitor);
        }

        private void ConnectToStaff(MonoBehaviour staff)
        {
            staffInterface = staff as IUpgradeableStaff;
            if (staffInterface == null)
            {
                Debug.LogError($"[RTStaffUpgrade] Spawned janitor '{staff.name}' does not implement IUpgradeableStaff!");
                return;
            }
            // Replay any already-purchased upgrades onto the freshly spawned janitor.
            ApplyCurrentUpgrade();
            Debug.Log($"[RTStaffUpgrade] Connected to spawned janitor '{staff.name}' and applied {currentLevel} upgrade(s).");
        }

        private void SubscribeToLevelEvents()
        {
            if (RTLevelManager.Instance == null) return;
            // Unsubscribe first to guard against double-subscription on repeated enables.
            RTLevelManager.Instance.OnLevelUp     -= OnLevelChanged;
            RTLevelManager.Instance.OnLevelLoaded -= OnLevelChanged;
            RTLevelManager.Instance.OnLevelUp     += OnLevelChanged;
            RTLevelManager.Instance.OnLevelLoaded += OnLevelChanged;
        }

        private void UnsubscribeFromLevelEvents()
        {
            if (RTLevelManager.Instance == null) return;
            RTLevelManager.Instance.OnLevelUp     -= OnLevelChanged;
            RTLevelManager.Instance.OnLevelLoaded -= OnLevelChanged;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by RTUpgradeSpot when the player finishes paying.</summary>
        public void CompleteUpgrade()
        {
            if (!CanUpgrade) return;

            var level = upgradeData.GetLevel(currentLevel);
            if (level == null) return;

            currentLevel++;
            SaveState();

            Debug.Log($"[RTStaffUpgrade] Upgraded '{staffTarget?.name}' to level {currentLevel}. Duration: {level.newDuration}s, Speed: {level.newMoveSpeed}, Carry: {level.newCarryCapacity}");

            // Complete the mission for the level we just finished.
            DynamicMissionManager.Instance?.CompleteStaffUpgradeMission(upgradeData.UpgradeId, currentLevel);

            ApplyCurrentUpgrade();
            CheckAvailability();
            OnUpgradeChanged?.Invoke();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(SoundEffect.CustomerServed);
        }

        public bool TryPurchaseNextLevelWithMoney()
        {
            EnsureStateLoaded();

            if (!CanUpgrade || !IsInteractionAvailable || NextLevel == null)
                return false;

            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[RTStaffUpgrade] CurrencyManager not found.");
                return false;
            }

            int cost = Mathf.Max(0, NextLevel.cost);
            if (cost > 0 && !CurrencyManager.Instance.SpendMoney(cost))
                return false;

            CompleteUpgrade();
            return true;
        }

        public void RequestRewardedAdUpgrade(Action onFinished = null)
        {
            EnsureStateLoaded();

            if (!CanUpgrade || !IsInteractionAvailable || isRewardedUpgradePending)
                return;

            if (AdsManager.Instance == null)
            {
                Debug.LogWarning("[RTStaffUpgrade] AdsManager not found in scene.");
                onFinished?.Invoke();
                return;
            }

            isRewardedUpgradePending = true;
            AdsManager.Instance.ShowRewardedAd(
                onRewardEarned: _ =>
                {
                    if (CanUpgrade)
                        CompleteUpgrade();
                },
                onClosed: () =>
                {
                    isRewardedUpgradePending = false;
                    AdsManager.Instance.LoadRewardedAd();
                    onFinished?.Invoke();
                });
        }

        public void EnsureStateLoaded()
        {
            if (hasLoadedState) return;
            if (upgradeData == null) return;
            LoadState();
        }

        // ── Private ───────────────────────────────────────────────────────────

        /// <summary>Applies all stat changes from the most recently purchased upgrade level.</summary>
        private void ApplyCurrentUpgrade()
        {
            if (staffInterface == null || currentLevel == 0) return;

            var lastPurchased = upgradeData.GetLevel(currentLevel - 1);
            if (lastPurchased == null) return;

            if (lastPurchased.newDuration > 0f)
                staffInterface.SetUpgradedDuration(lastPurchased.newDuration);

            if (lastPurchased.newMoveSpeed > 0f)
                staffInterface.SetUpgradedSpeed(lastPurchased.newMoveSpeed);

            if (lastPurchased.newCarryCapacity > 0)
                staffInterface.SetCarryCapacity(lastPurchased.newCarryCapacity);
        }

        private void CheckAvailability()
        {
            if (!CanUpgrade)
            {
                RemoveCurrentMission();
                if (upgradeSpot != null)
                    upgradeSpot.Hide();
                return;
            }

            if (IsUnlockedForCurrentPlayerLevel)
            {
                // Register the mission when the spot first becomes available.
                string staffName = upgradeData.UpgradeId;
                DynamicMissionManager.Instance?.RegisterStaffUpgradeMission(
                    upgradeData.UpgradeId, staffName, currentLevel + 1);

                if (useWorldUpgradeSpot && upgradeSpot != null)
                    upgradeSpot.Show(this);
                else if (upgradeSpot != null)
                    upgradeSpot.Hide();
            }
            else
            {
                RemoveCurrentMission();
                if (upgradeSpot != null)
                    upgradeSpot.Hide();
            }

            OnUpgradeChanged?.Invoke();
        }

        private void RemoveCurrentMission()
        {
            if (upgradeData == null || !CanUpgrade) return;
            DynamicMissionManager.Instance?.RemoveStaffUpgradeMission(upgradeData.UpgradeId, currentLevel + 1);
        }

        private void LoadState()
        {
            currentLevel = PlayerPrefs.GetInt(SaveKey, 0);
            // Clamp in case the SO was edited after save.
            if (upgradeData != null)
                currentLevel = Mathf.Clamp(currentLevel, 0, upgradeData.MaxLevel);
            hasLoadedState = true;
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
