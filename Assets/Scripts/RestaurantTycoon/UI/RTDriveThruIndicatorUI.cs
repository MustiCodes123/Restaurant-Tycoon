using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        [Tooltip("Optional text label that shows the item requested by the waiting car.")]
        [SerializeField] private TextMeshProUGUI requiredItemText;

        [Header("Animation")]
        [SerializeField] private float showScaleDuration = 0.18f;
        [SerializeField] private float hideScaleDuration = 0.12f;
        [SerializeField] private float shakeDuration = 0.22f;
        [Tooltip("Rotation degrees used for layout-safe attention shake.")]
        [SerializeField] private float shakeStrength = 8f;
        [SerializeField] private float shakeInterval = 1.1f;

        private Tween visibilityTween;
        private Tween shakeTween;
        private Vector3 indicatorBaseScale = Vector3.one;
        private Vector3 indicatorBaseRotation;
        private CanvasGroup indicatorCanvasGroup;
        private LayoutElement indicatorLayoutElement;
        private bool isVisible;
        private bool isSubscribed;

        private void Awake()
        {
            if (driveThroughIndicator == null)
                driveThroughIndicator = GetComponent<RectTransform>();

            if (driveThroughIndicator != null)
            {
                indicatorBaseScale = driveThroughIndicator.localScale;
                indicatorBaseRotation = driveThroughIndicator.localEulerAngles;
                indicatorCanvasGroup = driveThroughIndicator.GetComponent<CanvasGroup>();
                if (IsIndicatorOnThisObject() && indicatorCanvasGroup == null)
                    indicatorCanvasGroup = driveThroughIndicator.gameObject.AddComponent<CanvasGroup>();

                indicatorLayoutElement = driveThroughIndicator.GetComponent<LayoutElement>();
                if (indicatorLayoutElement == null)
                    indicatorLayoutElement = driveThroughIndicator.gameObject.AddComponent<LayoutElement>();

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
            DynamicMission mission = GetWaitingDriveThroughOrder();
            UpdateRequiredItemText(mission);

            if (mission != null)
                Show();
            else
                Hide();
        }

        private DynamicMission GetWaitingDriveThroughOrder()
        {
            if (DynamicMissionManager.Instance == null)
                return null;

            var activeMissions = DynamicMissionManager.Instance.GetActiveMissions();
            for (int i = 0; i < activeMissions.Count; i++)
            {
                if (activeMissions[i].missionType == DynamicMissionType.DriveThruOrder)
                    return activeMissions[i];
            }

            return null;
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
            SetLayoutIgnored(false);
            driveThroughIndicator.localEulerAngles = indicatorBaseRotation;
            driveThroughIndicator.localScale = Vector3.zero;
            RebuildParentLayout();

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
            sequence.Append(driveThroughIndicator.DOPunchRotation(Vector3.forward * shakeStrength, shakeDuration, 8, 0.4f));
            sequence.AppendInterval(shakeInterval);
            sequence.SetLoops(-1, LoopType.Restart);
            shakeTween = sequence;
        }

        private void StopShake()
        {
            shakeTween?.Kill();
            shakeTween = null;

            if (driveThroughIndicator != null)
                driveThroughIndicator.localEulerAngles = indicatorBaseRotation;
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
            SetLayoutIgnored(true);
            driveThroughIndicator.localEulerAngles = indicatorBaseRotation;
            driveThroughIndicator.localScale = Vector3.zero;
            UpdateRequiredItemText(null);

            if (!IsIndicatorOnThisObject())
                driveThroughIndicator.gameObject.SetActive(false);

            RebuildParentLayout();
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

        private void UpdateRequiredItemText(DynamicMission mission)
        {
            if (requiredItemText == null)
                return;

            requiredItemText.text = mission != null ? ExtractRequiredItemName(mission.displayText) : string.Empty;
        }

        private string ExtractRequiredItemName(string displayText)
        {
            if (string.IsNullOrWhiteSpace(displayText))
                return string.Empty;

            const string prefix = "Drive-through:";
            const string suffix = "order waiting!";

            string itemName = displayText;
            if (itemName.StartsWith(prefix))
                itemName = itemName.Substring(prefix.Length);

            int suffixIndex = itemName.IndexOf(suffix);
            if (suffixIndex >= 0)
                itemName = itemName.Substring(0, suffixIndex);

            return itemName.Trim();
        }

        private void RebuildParentLayout()
        {
            if (driveThroughIndicator == null || driveThroughIndicator.parent == null)
                return;

            RectTransform parent = driveThroughIndicator.parent as RectTransform;
            if (parent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }

        private void SetLayoutIgnored(bool ignored)
        {
            if (indicatorLayoutElement != null)
                indicatorLayoutElement.ignoreLayout = ignored;
        }
    }
}
