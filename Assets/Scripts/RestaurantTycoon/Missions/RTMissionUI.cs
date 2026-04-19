using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

namespace RestaurantTycoon
{
    /// <summary>
    /// Displays a single RT mission's progress (text + slider + completed icon).
    /// </summary>
    public class RTMissionUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI missionText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private GameObject completedIcon;

        [Header("Removal Animation")]
        [SerializeField] private float removeDelay = 1f;
        [SerializeField] private float removeDuration = 0.3f;

        private RTMissionData missionData;
        private bool isCompleted;
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;

        public bool IsCompleted => isCompleted;
        public RTMissionData MissionData => missionData;

        public event Action<RTMissionUI> OnRemoved;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
        }

        public void Setup(RTMissionData data)
        {
            missionData = data;
            isCompleted = false;

            if (completedIcon != null)
                completedIcon.SetActive(false);

            UpdateProgress(GetCurrentValue());
        }

        public void UpdateProgress(int currentValue)
        {
            if (missionData == null) return;

            if (missionText != null)
                missionText.text = missionData.GetProgressText(currentValue);

            if (progressSlider != null)
            {
                progressSlider.maxValue = missionData.targetAmount;
                progressSlider.value = Mathf.Min(currentValue, missionData.targetAmount);
            }

            if (currentValue >= missionData.targetAmount && !isCompleted)
            {
                isCompleted = true;

                if (completedIcon != null)
                    completedIcon.SetActive(true);

                RemoveWithAnimation();
            }
        }

        private void RemoveWithAnimation()
        {
            DOVirtual.DelayedCall(removeDelay, () =>
            {
                Sequence seq = DOTween.Sequence();

                if (canvasGroup != null)
                    seq.Join(canvasGroup.DOFade(0f, removeDuration).SetEase(Ease.OutQuad));

                if (rectTransform != null)
                    seq.Join(rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + 30f, removeDuration).SetEase(Ease.OutQuad));

                seq.Join(transform.DOScale(0.8f, removeDuration).SetEase(Ease.OutQuad));

                seq.OnComplete(() =>
                {
                    OnRemoved?.Invoke(this);
                    Destroy(gameObject);
                });
            });
        }

        private int GetCurrentValue()
        {
            if (RTLevelManager.Instance == null) return 0;
            return RTLevelManager.Instance.LevelEarnings;
        }
    }
}
