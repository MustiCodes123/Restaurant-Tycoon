using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

namespace RestaurantTycoon
{
    /// <summary>
    /// Slide-down panel that shows the current level's missions.
    /// Mirrors the existing LevelPanelUI pattern but uses RT types.
    /// </summary>
    public class RTLevelPanelUI : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private RectTransform panelTransform;
        [SerializeField] private TextMeshProUGUI levelTitleText;
        [SerializeField] private Transform missionContainer;
        [SerializeField] private GameObject missionUIPrefab;

        [Header("Animation Settings")]
        [SerializeField] private float slideDuration = 0.5f;
        [SerializeField] private Ease slideInEase = Ease.OutBack;
        [SerializeField] private Ease slideOutEase = Ease.InBack;
        [SerializeField] private float hidePositionY = 300f;

        [Header("Dynamic Missions")]
        [Tooltip("Prefab with a DynamicMissionUI component — shown for unlock missions.")]
        [SerializeField] private GameObject dynamicMissionUIPrefab;

        private Vector2 showPosition;
        private Vector2 hidePosition;
        private List<RTMissionUI> activeMissionUIs = new List<RTMissionUI>();
        private List<DynamicMissionUI> activeDynamicMissionUIs = new List<DynamicMissionUI>();
        private Tween currentTween;
        private bool isPanelVisible;
        private bool isInitialized;

        public void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            showPosition = panelTransform.anchoredPosition;
            hidePosition = new Vector2(showPosition.x, showPosition.y + hidePositionY);

            panelTransform.anchoredPosition = hidePosition;
            isPanelVisible = false;

            if (DynamicMissionManager.Instance != null)
            {
                DynamicMissionManager.Instance.OnMissionAdded     += OnDynamicMissionAdded;
                DynamicMissionManager.Instance.OnMissionCompleted += OnDynamicMissionCompleted;
                DynamicMissionManager.Instance.OnMissionRemoved   += OnDynamicMissionRemoved;
                Debug.Log("[RTLevelPanelUI] Subscribed to DynamicMissionManager events.");
            }
            else
            {
                Debug.LogWarning("[RTLevelPanelUI] DynamicMissionManager.Instance is NULL during Initialize! Dynamic missions will not show.");
            }
        }

        private void OnDestroy()
        {
            currentTween?.Kill();

            if (DynamicMissionManager.Instance != null)
            {
                DynamicMissionManager.Instance.OnMissionAdded     -= OnDynamicMissionAdded;
                DynamicMissionManager.Instance.OnMissionCompleted -= OnDynamicMissionCompleted;
                DynamicMissionManager.Instance.OnMissionRemoved   -= OnDynamicMissionRemoved;
            }
        }

        public void ShowLevel(RTLevelData levelData, int levelNumber = -1)
        {
            int displayNumber = levelNumber > 0 ? levelNumber : levelData.levelNumber;

            ClearMissions();

            if (levelTitleText != null)
                levelTitleText.text = $"Level {displayNumber}";

            if (levelData.missions != null)
            {
                foreach (var mission in levelData.missions)
                {
                    if (mission == null) continue;

                    GameObject obj = Instantiate(missionUIPrefab, missionContainer);
                    obj.SetActive(true);
                    RTMissionUI missionUI = obj.GetComponent<RTMissionUI>();
                    if (missionUI != null)
                    {
                        missionUI.Setup(mission);
                        missionUI.OnRemoved += OnMissionUIRemoved;
                        activeMissionUIs.Add(missionUI);
                    }
                }
            }

            // Load dynamic (unlock) missions already registered
            if (DynamicMissionManager.Instance != null)
            {
                var activeDMs = DynamicMissionManager.Instance.GetActiveMissions();
                Debug.Log($"[RTLevelPanelUI] ShowLevel: DynamicMissionManager found. Active dynamic missions: {activeDMs.Count}");
                foreach (var dm in activeDMs)
                {
                    Debug.Log($"[RTLevelPanelUI] Loading existing dynamic mission: '{dm.missionId}' | '{dm.displayText}'");
                    SpawnDynamicMissionUI(dm);
                }
            }
            else
            {
                Debug.LogWarning("[RTLevelPanelUI] ShowLevel: DynamicMissionManager.Instance is NULL — no dynamic missions loaded.");
            }

            UpdateAllMissions();
            SlideIn();
        }

        public void Hide(System.Action onComplete = null)
        {
            if (!isPanelVisible)
            {
                onComplete?.Invoke();
                return;
            }
            SlideOut(onComplete);
        }

        public void UpdateAllMissions()
        {
            if (RTLevelManager.Instance == null) return;
            int earnings = RTLevelManager.Instance.LevelEarnings;

            foreach (var missionUI in activeMissionUIs)
            {
                if (missionUI != null)
                    missionUI.UpdateProgress(earnings);
            }
        }

        public bool AreAllMissionsCompleted()
        {
            foreach (var missionUI in activeMissionUIs)
            {
                if (missionUI != null && !missionUI.IsCompleted)
                    return false;
            }

            foreach (var dynamicMissionUI in activeDynamicMissionUIs)
            {
                if (dynamicMissionUI != null && !dynamicMissionUI.IsCompleted)
                    return false;
            }

            return activeMissionUIs.Count > 0 || activeDynamicMissionUIs.Count > 0;
        }

        private void OnMissionUIRemoved(RTMissionUI missionUI)
        {
            if (missionUI != null)
            {
                missionUI.OnRemoved -= OnMissionUIRemoved;
                activeMissionUIs.Remove(missionUI);
            }
        }

        // ── Dynamic Mission Handlers ──────────────────────────────────────────

        private void OnDynamicMissionAdded(DynamicMission mission)
        {
            Debug.Log($"[RTLevelPanelUI] OnDynamicMissionAdded: '{mission.missionId}' | '{mission.displayText}'");
            SpawnDynamicMissionUI(mission);
        }

        private void OnDynamicMissionCompleted(DynamicMission mission)
        {
            foreach (var ui in activeDynamicMissionUIs)
            {
                if (ui != null && ui.MissionId == mission.missionId)
                {
                    ui.MarkCompleted();
                    if (RTLevelManager.Instance != null)
                        RTLevelManager.Instance.CheckMissionProgress();
                    return;
                }
            }
        }

        private void OnDynamicMissionRemoved(string missionId)
        {
            for (int i = activeDynamicMissionUIs.Count - 1; i >= 0; i--)
            {
                var ui = activeDynamicMissionUIs[i];
                if (ui != null && ui.MissionId == missionId)
                {
                    activeDynamicMissionUIs.RemoveAt(i);
                    Destroy(ui.gameObject);
                    return;
                }
            }
        }

        private void SpawnDynamicMissionUI(DynamicMission mission)
        {
            if (dynamicMissionUIPrefab == null)
            {
                Debug.LogError("[RTLevelPanelUI] dynamicMissionUIPrefab is NULL — assign it in the Inspector on RTLevelPanelUI.");
                return;
            }
            if (missionContainer == null)
            {
                Debug.LogError("[RTLevelPanelUI] missionContainer is NULL.");
                return;
            }

            // Prevent duplicates
            foreach (var existing in activeDynamicMissionUIs)
                if (existing != null && existing.MissionId == mission.missionId)
                {
                    Debug.Log($"[RTLevelPanelUI] SpawnDynamicMissionUI: skipping duplicate '{mission.missionId}'");
                    return;
                }

            GameObject obj = Instantiate(dynamicMissionUIPrefab, missionContainer);
            obj.SetActive(true);
            DynamicMissionUI ui = obj.GetComponent<DynamicMissionUI>();
            if (ui != null)
            {
                ui.Setup(mission);
                activeDynamicMissionUIs.Add(ui);
                Debug.Log($"[RTLevelPanelUI] Spawned DynamicMissionUI for '{mission.missionId}'");
            }
            else
            {
                Debug.LogError($"[RTLevelPanelUI] Instantiated prefab is missing DynamicMissionUI component! Prefab: {dynamicMissionUIPrefab.name}");
            }
        }

        private void ClearMissions()
        {
            foreach (var missionUI in activeMissionUIs)
            {
                if (missionUI != null)
                    missionUI.OnRemoved -= OnMissionUIRemoved;
            }
            activeMissionUIs.Clear();
            activeDynamicMissionUIs.Clear();

            for (int i = missionContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = missionContainer.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private void SlideIn()
        {
            currentTween?.Kill();
            isPanelVisible = true;
            currentTween = panelTransform
                .DOAnchorPos(showPosition, slideDuration)
                .SetEase(slideInEase);
        }

        private void SlideOut(System.Action onComplete = null)
        {
            currentTween?.Kill();
            isPanelVisible = false;
            currentTween = panelTransform
                .DOAnchorPos(hidePosition, slideDuration)
                .SetEase(slideOutEase)
                .OnComplete(() => onComplete?.Invoke());
        }
    }
}
