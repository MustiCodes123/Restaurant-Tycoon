using UnityEngine;
using System;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Manages the unlock state for a single pre-placed RT cook.
    /// The cook GameObject must already exist in the scene (disabled).
    /// On unlock it is simply activated — no instantiation needed.
    /// </summary>
    public class RTCookUnlock : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private RTCookUnlockData unlockData;
        [SerializeField] private RTCookUnlockSpot unlockSpot;

        [Header("Cook Reference")]
        [Tooltip("The RTCook GameObject already placed in the scene (disabled by default).")]
        [SerializeField] private RTCook cook;

        [Header("Objects to Hide on Unlock")]
        [Tooltip("Placeholder visuals, lock icons, etc.")]
        [SerializeField] private GameObject[] objectsToHide;

        [Header("Objects to Show on Unlock")]
        [SerializeField] private GameObject[] objectsToShow;

        [Header("Save Key")]
        [SerializeField] private string saveKeyOverride;

        [Header("Appear Animation")]
        [SerializeField] private float popDuration = 0.5f;
        [SerializeField] private Ease popEase = Ease.OutBack;

        // ── Runtime ──────────────────────────────────────────────────────────
        private bool isUnlocked = false;

        private string SaveKey => string.IsNullOrEmpty(saveKeyOverride)
            ? $"RTCookUnlock_{unlockData?.CookName ?? gameObject.name}"
            : saveKeyOverride;

        public bool IsUnlocked => isUnlocked;
        public RTCookUnlockData UnlockData => unlockData;
        public int UnlockCost => unlockData != null ? unlockData.UnlockCost : 0;

        public event Action OnCookUnlocked;
        public event Action OnUnlockAvailable;
        public event Action OnUnlockUnavailable;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Start()
        {
            LoadUnlockState();
            ApplyUnlockState();

            if (RTLevelManager.Instance != null)
                RTLevelManager.Instance.OnLevelUp += OnPlayerLevelUp;

            CheckUnlockAvailability();
        }

        private void OnDestroy()
        {
            if (RTLevelManager.Instance != null)
                RTLevelManager.Instance.OnLevelUp -= OnPlayerLevelUp;
        }

        private void OnPlayerLevelUp(int newLevel) => CheckUnlockAvailability();

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Shows or hides the unlock spot based on current player level.
        /// </summary>
        public void CheckUnlockAvailability()
        {
            if (isUnlocked)
            {
                HideUnlockSpot();
                return;
            }

            if (unlockData == null)
            {
                HideUnlockSpot();
                return;
            }

            int playerLevel = RTLevelManager.Instance != null ? RTLevelManager.Instance.CurrentLevel : 1;

            if (playerLevel >= unlockData.RequiredPlayerLevel)
            {
                ShowUnlockSpot();
                OnUnlockAvailable?.Invoke();
            }
            else
            {
                HideUnlockSpot();
                OnUnlockUnavailable?.Invoke();
            }
        }

        /// <summary>
        /// Called by RTCookUnlockSpot when payment is complete.
        /// </summary>
        public void CompleteUnlock()
        {
            if (isUnlocked) return;

            isUnlocked = true;
            SaveUnlockState();

            HideUnlockSpot();

            // Hide placeholder objects
            if (objectsToHide != null)
                foreach (var obj in objectsToHide)
                    if (obj != null) obj.SetActive(false);

            // Show additional objects with pop animation
            if (objectsToShow != null)
                foreach (var obj in objectsToShow)
                    if (obj != null) ActivateWithPop(obj);

            // Activate the cook with pop animation
            if (cook != null)
                ActivateWithPop(cook.gameObject);
            else
                Debug.LogWarning("[RTCookUnlock] No RTCook reference assigned!");

            OnCookUnlocked?.Invoke();
            Debug.Log($"[RTCookUnlock] Cook '{unlockData?.CookName}' unlocked!");
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
                // Restore unlocked state silently (no pop animation on load)
                if (cook != null) cook.gameObject.SetActive(true);

                if (objectsToHide != null)
                    foreach (var obj in objectsToHide)
                        if (obj != null) obj.SetActive(false);

                if (objectsToShow != null)
                    foreach (var obj in objectsToShow)
                        if (obj != null) obj.SetActive(true);
            }
            else
            {
                // Ensure cook stays off until unlocked
                if (cook != null) cook.gameObject.SetActive(false);

                if (objectsToHide != null)
                    foreach (var obj in objectsToHide)
                        if (obj != null) obj.SetActive(true);

                if (objectsToShow != null)
                    foreach (var obj in objectsToShow)
                        if (obj != null) obj.SetActive(false);
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

        private void ActivateWithPop(GameObject obj)
        {
            if (obj == null) return;

            Vector3 originalScale = obj.transform.localScale;
            obj.transform.localScale = Vector3.zero;
            obj.SetActive(true);

            obj.transform.DOScale(originalScale, popDuration)
                .SetEase(popEase)
                .OnComplete(() =>
                {
                    obj.transform.DOPunchRotation(new Vector3(0, 0, 5f), 0.3f, 10, 1f);
                });
        }

        [ContextMenu("Reset Unlock State")]
        public void ResetUnlockState()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            isUnlocked = false;
            ApplyUnlockState();
            CheckUnlockAvailability();
        }
    }
}
