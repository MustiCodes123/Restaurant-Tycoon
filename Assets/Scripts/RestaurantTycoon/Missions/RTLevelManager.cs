using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;

namespace RestaurantTycoon
{
    /// <summary>
    /// Manages level progression for the restaurant tycoon.
    /// Tracks money earned per level. When all missions are complete, advances to the next level.
    /// </summary>
    public class RTLevelManager : MonoBehaviour
    {
        public static RTLevelManager Instance { get; private set; }

        [Header("Level Data")]
        [SerializeField] private List<RTLevelData> allLevels = new List<RTLevelData>();

        [Header("UI")]
        [SerializeField] private RTLevelPanelUI levelPanelUI;
        [SerializeField] private LevelCompleteUI levelCompleteUI;
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        [Tooltip("Assign the debug button here. On click it instantly completes all money missions for the current level.")]
        [SerializeField] private Button debugCompleteCashMissionButton;

        private RTLevelData currentLevelData;
        private int currentLevelIndex;
        private bool allLevelsCompleted;
        private bool isCompletingLevel;

        // Level-scoped earnings (reset each level)
        private int levelEarnings;
        private string LevelEarningsKey => $"RTLevelEarnings_Level_{CurrentLevel}";

        public int CurrentLevel => currentLevelIndex + 1;
        public int LevelEarnings => levelEarnings;

        public event Action<int> OnLevelUp;
        public event Action OnMissionProgressUpdated;
        /// <summary>Fires after the level has fully loaded. Unlock scripts use this to run their first availability check.</summary>
        public event Action<int> OnLevelLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (debugCompleteCashMissionButton != null)
                debugCompleteCashMissionButton.onClick.AddListener(DebugCompleteCashMission);
            if (levelPanelUI != null)
                levelPanelUI.Initialize();

            if (levelCompleteUI != null)
                levelCompleteUI.Initialize();

            LoadCurrentLevel();
        }

        private void LoadCurrentLevel()
        {
            isCompletingLevel = false;
            int savedLevel = DataManager.Instance != null ? DataManager.Instance.CurrentLevel : 1;
            currentLevelIndex = savedLevel - 1;
            levelEarnings = PlayerPrefs.GetInt(LevelEarningsKey, 0);

            if (currentLevelIndex >= 0 && currentLevelIndex < allLevels.Count)
            {
                allLevelsCompleted = false;
                currentLevelData = allLevels[currentLevelIndex];

                if (levelPanelUI != null)
                    levelPanelUI.ShowLevel(currentLevelData, savedLevel);

                if (levelText != null)
                    levelText.text = $"Level {savedLevel}";

                Log($"Loaded Level {savedLevel}: {currentLevelData.levelName}");
                OnLevelLoaded?.Invoke(CurrentLevel);
            }
            else
            {
                allLevelsCompleted = true;
                currentLevelData = null;

                if (levelText != null)
                    levelText.text = "Endless";

                Log("All levels completed or no levels configured!");
            }
        }

        /// <summary>
        /// Debug: instantly fills levelEarnings to the highest money mission target,
        /// completing all cash-based missions on the current level.
        /// </summary>
        public void DebugCompleteCashMission()
        {
            if (currentLevelData == null) return;

            int maxTarget = 0;
            foreach (var m in currentLevelData.missions)
                if (m != null && m.targetAmount > maxTarget)
                    maxTarget = m.targetAmount;

            if (maxTarget <= levelEarnings)
            {
                Log("[Debug] Cash mission already complete.");
                return;
            }

            int needed = maxTarget - levelEarnings;
            Log($"[Debug] Auto-completing cash mission: adding ${needed}");
            RegisterMoneyEarned(needed);
        }

        /// <summary>
        /// Called when the player earns money (e.g. cashier serves a customer).
        /// Adds to both global currency and level-scoped tracking.
        /// </summary>
        public void RegisterMoneyEarned(int amount)
        {
            if (amount <= 0) return;

            amount = RTRewardedAdSystem.ApplyMoneyMultiplier(amount);

            // Global wallet
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.AddMoney(amount);

            // Level-scoped tracking
            levelEarnings += amount;
            SaveLevelEarnings();

            Log($"Money earned: ${amount}. Level total: ${levelEarnings}");

            UpdateMissionProgress();
        }

        private void UpdateMissionProgress()
        {
            if (allLevelsCompleted || currentLevelData == null || isCompletingLevel)
                return;

            OnMissionProgressUpdated?.Invoke();

            if (levelPanelUI != null)
            {
                levelPanelUI.UpdateAllMissions();

                if (levelPanelUI.AreAllMissionsCompleted())
                    CompleteLevel();
            }
        }

        /// <summary>
        /// Re-checks mission completion after non-money mission state changes.
        /// </summary>
        public void CheckMissionProgress()
        {
            UpdateMissionProgress();
        }

        private void CompleteLevel()
        {
            if (isCompletingLevel) return;
            isCompletingLevel = true;

            int completedLevel = CurrentLevel;
            Log($"Level {completedLevel} Complete!");

            currentLevelData = null;

            if (DataManager.Instance != null)
                DataManager.Instance.CurrentLevel++;
            PaymentProgressStore.Clear($"RTLevelEarnings_Level_{completedLevel}");

            OnLevelUp?.Invoke(CurrentLevel);

            if (levelPanelUI != null)
                levelPanelUI.Hide();

            if (levelCompleteUI != null)
            {
                levelCompleteUI.ShowLevelComplete(completedLevel, () =>
                {
                    LoadCurrentLevel();
                });
            }
            else
            {
                LoadCurrentLevel();
            }
        }

        private void Log(string message)
        {
            if (showDebugLogs)
                Debug.Log($"[RTLevelManager] {message}");
        }

        private void SaveLevelEarnings()
        {
            PlayerPrefs.SetInt(LevelEarningsKey, levelEarnings);
            PlayerPrefs.Save();
        }
    }

}
