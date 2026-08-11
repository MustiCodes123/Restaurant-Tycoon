using DG.Tweening;
using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// Shows a HUD icon while a drive-through order is waiting, replacing the
    /// dynamic mission row that used to appear in the top mission panel.
    /// </summary>
    public class RTDriveThruIndicatorUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Icon or image GameObject to show when a drive-through order is waiting.")]
        [SerializeField] private RectTransform driveThroughIndicator;

        [Header("Animation")]
        [SerializeField] private float showScaleDuration = 0.18f;
        [SerializeField] private float hideScaleDuration = 0.12f;
        [SerializeField] private float shakeDuration = 0.22f;
        [SerializeField] private float shakeStrength = 8f;
        [SerializeField] private float shakeInterval = 1.1f;

        private Tween visibilityTween;
        private Tween shakeTween;
        private Vector3 indicatorBaseScale = Vector3.one;
        private Vector2 indicatorBasePosition;
        private CanvasGroup indicatorCanvasGroup;
        private bool isVisible;
        private bool isSubscribed;

        private void Awake()
        {
            if (driveThroughIndicator == null)
                driveThroughIndicator = GetComponent<RectTransform>();

            if (driveThroughIndicator != null)
            {
                indicatorBaseScale = driveThroughIndicator.localScale;
                indicatorBasePosition = driveThroughIndicator.anchoredPosition;
                indicatorCanvasGroup = driveThroughIndicator.GetComponent<CanvasGroup>();
                if (IsIndicatorOnThisObject() && indicatorCanvasGroup == null)
                    indicatorCanvasGroup = driveThroughIndicator.gameObject.AddComponent<CanvasGroup>();

                HideImmediate();
            }
        }

        private void OnEnable()
        {
            TrySubscribe();
            RefreshVisibility();
        }

        private void Start()
        {
            TrySubscribe();
            RefreshVisibility();
        }

        private void Update()
        {
            if (isSubscribed)
                return;

            TrySubscribe();
            RefreshVisibility();
        }

        private void OnDisable()
        {
            Unsubscribe();

            StopAnimations();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            StopAnimations();
        }

        private void OnMissionAdded(DynamicMission mission)
        {
            if (mission.missionType != DynamicMissionType.DriveThruOrder)
                return;

            RefreshVisibility();
        }

        private void OnMissionCompleted(DynamicMission mission)
        {
            if (mission.missionType != DynamicMissionType.DriveThruOrder)
                return;

            RefreshVisibility();
        }

        private void OnMissionRemoved(string missionId)
        {
            if (!missionId.StartsWith("DriveThru_"))
                return;

            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (HasWaitingDriveThroughOrder())
                Show();
            else
                Hide();
        }

        private bool HasWaitingDriveThroughOrder()
        {
            if (DynamicMissionManager.Instance == null)
                return false;

            var activeMissions = DynamicMissionManager.Instance.GetActiveMissions();
            for (int i = 0; i < activeMissions.Count; i++)
            {
                if (activeMissions[i].missionType == DynamicMissionType.DriveThruOrder)
                    return true;
            }

            return false;
        }

        private void Show()
        {
            if (driveThroughIndicator == null || isVisible)
                return;

            isVisible = true;
            visibilityTween?.Kill();
            if (!driveThroughIndicator.gameObject.activeSelf)
                driveThroughIndicator.gameObject.SetActive(true);

            SetCanvasGroupVisible(true);
            driveThroughIndicator.anchoredPosition = indicatorBasePosition;
            driveThroughIndicator.localScale = Vector3.zero;

            visibilityTween = driveThroughIndicator
                .DOScale(indicatorBaseScale, showScaleDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(StartShake);
        }

        private void Hide()
        {
            if (driveThroughIndicator == null || !isVisible)
                return;

            isVisible = false;
            visibilityTween?.Kill();
            StopShake();

            visibilityTween = driveThroughIndicator
                .DOScale(Vector3.zero, hideScaleDuration)
                .SetEase(Ease.InBack)
                .OnComplete(HideImmediate);
        }

        private void StartShake()
        {
            if (driveThroughIndicator == null || !isVisible)
                return;

            StopShake();

            Sequence sequence = DOTween.Sequence();
            sequence.Append(driveThroughIndicator.DOShakeAnchorPos(shakeDuration, shakeStrength, 10, 45f, false, true));
            sequence.AppendInterval(shakeInterval);
            sequence.SetLoops(-1, LoopType.Restart);
            shakeTween = sequence;
        }

        private void StopShake()
        {
            shakeTween?.Kill();
            shakeTween = null;

            if (driveThroughIndicator != null)
                driveThroughIndicator.anchoredPosition = indicatorBasePosition;
        }

        private void StopAnimations()
        {
            visibilityTween?.Kill();
            visibilityTween = null;
            StopShake();
        }

        private void TrySubscribe()
        {
            if (isSubscribed || DynamicMissionManager.Instance == null)
                return;

            DynamicMissionManager.Instance.OnMissionAdded += OnMissionAdded;
            DynamicMissionManager.Instance.OnMissionCompleted += OnMissionCompleted;
            DynamicMissionManager.Instance.OnMissionRemoved += OnMissionRemoved;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
                return;

            if (DynamicMissionManager.Instance != null)
            {
                DynamicMissionManager.Instance.OnMissionAdded -= OnMissionAdded;
                DynamicMissionManager.Instance.OnMissionCompleted -= OnMissionCompleted;
                DynamicMissionManager.Instance.OnMissionRemoved -= OnMissionRemoved;
            }

            isSubscribed = false;
        }

        private void HideImmediate()
        {
            if (driveThroughIndicator == null)
                return;

            SetCanvasGroupVisible(false);
            driveThroughIndicator.anchoredPosition = indicatorBasePosition;
            driveThroughIndicator.localScale = Vector3.zero;

            if (!IsIndicatorOnThisObject())
                driveThroughIndicator.gameObject.SetActive(false);
        }

        private void SetCanvasGroupVisible(bool visible)
        {
            if (indicatorCanvasGroup == null)
                return;

            indicatorCanvasGroup.alpha = visible ? 1f : 0f;
            indicatorCanvasGroup.interactable = visible;
            indicatorCanvasGroup.blocksRaycasts = visible;
        }

        private bool IsIndicatorOnThisObject()
        {
            return driveThroughIndicator != null && driveThroughIndicator.gameObject == gameObject;
        }
    }
}
