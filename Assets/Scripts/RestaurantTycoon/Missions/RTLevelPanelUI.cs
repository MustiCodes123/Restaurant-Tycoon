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

        private Vector2 showPosition;
        private Vector2 hidePosition;
        private List<RTMissionUI> activeMissionUIs = new List<RTMissionUI>();
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
        }

        private void OnDestroy()
        {
            currentTween?.Kill();
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
            if (activeMissionUIs.Count == 0) return false;

            foreach (var missionUI in activeMissionUIs)
            {
                if (!missionUI.IsCompleted)
                    return false;
            }
            return true;
        }

        private void OnMissionUIRemoved(RTMissionUI missionUI)
        {
            if (missionUI != null)
            {
                missionUI.OnRemoved -= OnMissionUIRemoved;
                activeMissionUIs.Remove(missionUI);
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
