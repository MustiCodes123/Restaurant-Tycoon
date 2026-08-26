using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Generic unlock that enables a list of pre-placed scene GameObjects
    /// when the player pays at an RTSceneObjectUnlockSpot.
    /// The objects are disabled on Start if not yet unlocked, and enabled on unlock.
    /// </summary>
    public class RTSceneObjectUnlock : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private RTSceneObjectUnlockData unlockData;
        [SerializeField] private RTSceneObjectUnlockSpot unlockSpot;

        [Header("Objects to Enable on Unlock")]
        [Tooltip("Scene GameObjects that will be activated when this unlock is completed.")]
        [SerializeField] private List<GameObject> objectsToEnable = new List<GameObject>();

        [Header("Objects to Hide on Unlock")]
        [Tooltip("Placeholder visuals, lock icons, etc. that disappear on unlock.")]
        [SerializeField] private List<GameObject> objectsToHide = new List<GameObject>();

        [Header("Future Unlock Preview")]
        [Tooltip("Subtle locked tile / lock icon preview objects shown before this area is unlocked.")]
        [SerializeField] private List<GameObject> lockedPreviewObjects = new List<GameObject>();
        [Tooltip("Show preview before the required player level is reached.")]
        [SerializeField] private bool showPreviewBeforeRequiredLevel = true;
        [Tooltip("Keep preview visible while the unlock spot is available, until the player completes the unlock.")]
        [SerializeField] private bool showPreviewWhenUnlockAvailable = true;

        [Header("Cinematic Tutorial")]
        [Tooltip("Optional explicit targets to show after unlock. If empty, RTStall objects are auto-detected from Objects To Enable.")]
        [SerializeField] private List<Transform> cinematicFocusTargets = new List<Transform>();

        [Header("Save Key")]
        [SerializeField] private string saveKeyOverride;

        [Header("Appear Animation")]
        [SerializeField] private float popDuration = 0.5f;
        [SerializeField] private Ease popEase = Ease.OutBack;

        // ── Runtime ──────────────────────────────────────────────────────────
        private bool isUnlocked = false;
        private bool hasPlayedAvailabilityFocus = false;

        private string SaveKey => string.IsNullOrEmpty(saveKeyOverride)
            ? $"RTSceneObjectUnlock_{unlockData?.UnlockName ?? gameObject.name}"
            : saveKeyOverride;

        public bool IsUnlocked => isUnlocked;
        public RTSceneObjectUnlockData UnlockData => unlockData;
        public int UnlockCost => unlockData != null ? unlockData.UnlockCost : 0;
        public string PaymentProgressKey => $"{SaveKey}_PaymentProgress";

        public event Action OnUnlocked;
        public event Action OnUnlockAvailable;
        public event Action OnUnlockUnavailable;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Start()
        {
            LoadUnlockState();

            if (isUnlocked)
            {
                // Already unlocked: apply state immediately.
                ApplyUnlockState();
            }
            else
            {
                // Show hide-objects (lock icons, placeholders) right away.
                foreach (var obj in objectsToHide)
                    if (obj != null) obj.SetActive(true);

                // Delay disabling objectsToEnable by 2 frames.
                // NavMeshAgents (and other components) on those objects must run
                // Awake/OnEnable at least once so they register with Unity systems.
                // If we SetActive(false) immediately they never register, and when
                // re-enabled on unlock they fail with "not close enough to NavMesh".
                StartCoroutine(DelayedHideLockedObjects());
            }

            if (RTLevelManager.Instance != null)
            {
                RTLevelManager.Instance.OnLevelUp     += OnPlayerLevelUp;
                RTLevelManager.Instance.OnLevelLoaded += OnPlayerLevelUp;
            }

            if (DynamicMissionManager.Instance != null)
                DynamicMissionManager.Instance.OnMissionShown += OnDynamicMissionShown;

            CheckUnlockAvailability();
            PlayAvailabilityFocusIfMissionAlreadyShown();
        }

        private System.Collections.IEnumerator DelayedHideLockedObjects()
        {
            // Wait long enough for NavMeshAgents (and other deferred systems) to fully register.
            // A fixed frame count was too few at lower frame rates or during busy scene loads;
            // a time-based wait is more reliable.
            yield return new WaitForSeconds(0.2f);

            if (!isUnlocked)
            {
                foreach (var obj in objectsToEnable)
                    if (obj != null) obj.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (RTLevelManager.Instance != null)
            {
                RTLevelManager.Instance.OnLevelUp     -= OnPlayerLevelUp;
                RTLevelManager.Instance.OnLevelLoaded -= OnPlayerLevelUp;
            }

            if (DynamicMissionManager.Instance != null)
                DynamicMissionManager.Instance.OnMissionShown -= OnDynamicMissionShown;
        }

        private void OnPlayerLevelUp(int newLevel) => CheckUnlockAvailability();

        // ── Public API ────────────────────────────────────────────────────────

        public void CheckUnlockAvailability()
        {
            if (isUnlocked)
            {
                HideUnlockSpot();
                RefreshLockedPreview();
                return;
            }

            if (unlockData == null)
            {
                HideUnlockSpot();
                RefreshLockedPreview();
                return;
            }

            int playerLevel = RTLevelManager.Instance != null ? RTLevelManager.Instance.CurrentLevel : 1;

            if (playerLevel >= unlockData.RequiredPlayerLevel)
            {
                ShowUnlockSpot();
                Debug.Log($"[RTSceneObjectUnlock] '{unlockData.UnlockName}' available at level {playerLevel} (required {unlockData.RequiredPlayerLevel}). Registering mission. DynamicMissionManager: {(DynamicMissionManager.Instance != null ? "found" : "NULL")}");
                DynamicMissionManager.Instance?.RegisterSceneObjectUnlockMission(
                    unlockData.UnlockName,
                    unlockData.UnlockName);
                OnUnlockAvailable?.Invoke();
            }
            else
            {
                HideUnlockSpot();
                Debug.Log($"[RTSceneObjectUnlock] '{unlockData.UnlockName}' not yet available. Level {playerLevel} / {unlockData.RequiredPlayerLevel}");
                OnUnlockUnavailable?.Invoke();
            }

            RefreshLockedPreview(playerLevel);
        }

        /// <summary>Called by RTSceneObjectUnlockSpot when payment is complete.</summary>
        public void CompleteUnlock()
        {
            if (isUnlocked) return;

            isUnlocked = true;
            ClearPaymentProgress();
            SaveUnlockState();

            HideUnlockSpot();

            // Hide placeholder objects
            foreach (var obj in objectsToHide)
                if (obj != null) obj.SetActive(false);

            HideLockedPreview();

            // Enable unlocked objects with pop animation
            foreach (var obj in objectsToEnable)
                if (obj != null) ActivateWithPop(obj);

            PlayCinematicCameraTutorial();

            OnUnlocked?.Invoke();
            DynamicMissionManager.Instance?.CompleteSceneObjectUnlockMission(unlockData?.UnlockName);
            Debug.Log($"[RTSceneObjectUnlock] '{unlockData?.UnlockName ?? gameObject.name}' unlocked!");
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void ShowUnlockSpot()
        {
            if (unlockSpot != null) unlockSpot.Show(this);
        }

        private void HideUnlockSpot()
        {
            if (unlockSpot != null) unlockSpot.Hide();
        }

        private void ApplyUnlockState()
        {
            if (isUnlocked)
            {
                foreach (var obj in objectsToEnable)
                    if (obj != null) obj.SetActive(true);

                foreach (var obj in objectsToHide)
                    if (obj != null) obj.SetActive(false);

                HideLockedPreview();
            }
            else
            {
                // Disable until unlocked
                foreach (var obj in objectsToEnable)
                    if (obj != null) obj.SetActive(false);

                foreach (var obj in objectsToHide)
                    if (obj != null) obj.SetActive(true);

                RefreshLockedPreview();
            }
        }

        private void RefreshLockedPreview(int playerLevel = -1)
        {
            if (lockedPreviewObjects.Count == 0)
                return;

            if (isUnlocked || unlockData == null)
            {
                HideLockedPreview();
                return;
            }

            if (playerLevel < 0)
                playerLevel = RTLevelManager.Instance != null ? RTLevelManager.Instance.CurrentLevel : 1;

            bool isAvailable = playerLevel >= unlockData.RequiredPlayerLevel;
            bool shouldShow = isAvailable ? showPreviewWhenUnlockAvailable : showPreviewBeforeRequiredLevel;
            SetLockedPreviewVisible(shouldShow);
        }

        private void HideLockedPreview()
        {
            SetLockedPreviewVisible(false);
        }

        private void SetLockedPreviewVisible(bool visible)
        {
            foreach (var obj in lockedPreviewObjects)
            {
                if (obj == null)
                    continue;

                if (visible)
                    obj.SetActive(true);

                obj.SendMessage(
                    visible ? "ShowLocked" : "HideLocked",
                    SendMessageOptions.DontRequireReceiver);

                if (!visible)
                    obj.SetActive(false);
            }
        }

        private void LoadUnlockState()
        {
            isUnlocked = PlayerPrefs.GetInt(SaveKey, 0) == 1;
        }

        private void SaveUnlockState()
        {
            PlayerPrefs.SetInt(SaveKey, 1);
            PlayerPrefs.Save();
        }

        public int LoadPaymentProgress()
        {
            return PaymentProgressStore.Load(PaymentProgressKey, UnlockCost);
        }

        public void SavePaymentProgress(int amount)
        {
            PaymentProgressStore.Save(PaymentProgressKey, amount, UnlockCost);
        }

        public void ClearPaymentProgress()
        {
            PaymentProgressStore.Clear(PaymentProgressKey);
        }

        private void ActivateWithPop(GameObject obj)
        {
            if (obj == null) return;

            // Enable first so NavMeshAgent (and other components) re-register
            // from the correct world position before any scale manipulation.
            obj.SetActive(true);

            // Kill any leftover tweens from a previous enable/disable cycle so that
            // localScale reflects the intended resting value before we read it.
            DOTween.Kill(obj.transform, true);

            Vector3 originalScale = obj.transform.localScale;
            obj.transform.localScale = Vector3.zero;

            obj.transform.DOScale(originalScale, popDuration)
                .SetEase(popEase)
                .OnComplete(() => obj.transform.DOPunchRotation(new Vector3(0, 0, 5f), 0.3f, 10, 1f));
        }

        private void PlayAvailabilityCameraTutorial()
        {
            if (hasPlayedAvailabilityFocus) return;

            hasPlayedAvailabilityFocus = true;
            PlayCinematicCameraTutorial();
        }

        private void OnDynamicMissionShown(DynamicMission mission)
        {
            if (mission == null || isUnlocked || unlockData == null) return;

            if (mission.missionId == MissionId)
                PlayAvailabilityCameraTutorial();
        }

        private void PlayAvailabilityFocusIfMissionAlreadyShown()
        {
            if (isUnlocked || unlockData == null || DynamicMissionManager.Instance == null) return;

            if (DynamicMissionManager.Instance.HasShownMission(MissionId))
                PlayAvailabilityCameraTutorial();
        }

        private string MissionId => $"UnlockSceneObject_{unlockData.UnlockName}";

        private void PlayCinematicCameraTutorial()
        {
            List<Transform> targets = new List<Transform>();

            foreach (Transform target in cinematicFocusTargets)
                if (target != null)
                    targets.Add(target);

            if (targets.Count == 0)
            {
                foreach (GameObject obj in objectsToEnable)
                {
                    if (obj == null) continue;

                    RTStall stall = obj.GetComponentInChildren<RTStall>(true);
                    if (stall != null)
                    {
                        AddTarget(targets, stall.IngredientContainer);
                        AddTarget(targets, stall.CookInputContainer);
                        AddTarget(targets, stall.CookingSpot);
                    }
                    else
                    {
                        AddTarget(targets, obj.transform);
                    }
                }
            }

            RTTutorialCameraFocus.PlaySequence(targets);
        }

        private static void AddTarget(List<Transform> targets, Component component)
        {
            if (component != null)
                AddTarget(targets, component.transform);
        }

        private static void AddTarget(List<Transform> targets, Transform target)
        {
            if (target != null && !targets.Contains(target))
                targets.Add(target);
        }

        [ContextMenu("Reset Unlock State")]
        public void ResetUnlockState()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            ClearPaymentProgress();
            isUnlocked = false;
            hasPlayedAvailabilityFocus = false;
            ApplyUnlockState();
            CheckUnlockAvailability();
        }
    }
}
