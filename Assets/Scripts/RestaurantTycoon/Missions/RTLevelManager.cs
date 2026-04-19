using UnityEngine;
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

        private RTLevelData currentLevelData;
        private int currentLevelIndex;
        private bool allLevelsCompleted;
        private bool isCompletingLevel;

        // Level-scoped earnings (reset each level)
        private int levelEarnings;

        public int CurrentLevel => currentLevelIndex + 1;
        public int LevelEarnings => levelEarnings;

        public event Action<int> OnLevelUp;
        public event Action OnMissionProgressUpdated;

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
            if (levelPanelUI != null)
                levelPanelUI.Initialize();

            if (levelCompleteUI != null)
                levelCompleteUI.Initialize();

            LoadCurrentLevel();
        }

        private void LoadCurrentLevel()
        {
            isCompletingLevel = false;
            levelEarnings = 0;

            int savedLevel = DataManager.Instance != null ? DataManager.Instance.CurrentLevel : 1;
            currentLevelIndex = savedLevel - 1;

            if (currentLevelIndex >= 0 && currentLevelIndex < allLevels.Count)
            {
                allLevelsCompleted = false;
                currentLevelData = allLevels[currentLevelIndex];

                if (levelPanelUI != null)
                    levelPanelUI.ShowLevel(currentLevelData, savedLevel);

                if (levelText != null)
                    levelText.text = $"Level {savedLevel}";

                Log($"Loaded Level {savedLevel}: {currentLevelData.levelName}");
            }
            else
            {
                allLevelsCompleted = true;
                currentLevelData = null;

                if (levelText != null)
                    levelText.text = "Complete";

                Log("All levels completed or no levels configured!");
            }
        }

        /// <summary>
        /// Called when the player earns money (e.g. cashier serves a customer).
        /// Adds to both global currency and level-scoped tracking.
        /// </summary>
        public void RegisterMoneyEarned(int amount)
        {
            if (amount <= 0) return;

            // Global wallet
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.AddMoney(amount);

            // Level-scoped tracking
            levelEarnings += amount;

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

        private void CompleteLevel()
        {
            if (isCompletingLevel) return;
            isCompletingLevel = true;

            int completedLevel = CurrentLevel;
            Log($"Level {completedLevel} Complete!");

            currentLevelData = null;

            if (DataManager.Instance != null)
                DataManager.Instance.CurrentLevel++;

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
    }
}
