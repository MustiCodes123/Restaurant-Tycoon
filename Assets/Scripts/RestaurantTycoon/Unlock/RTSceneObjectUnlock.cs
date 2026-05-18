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

        [Header("Save Key")]
        [SerializeField] private string saveKeyOverride;

        [Header("Appear Animation")]
        [SerializeField] private float popDuration = 0.5f;
        [SerializeField] private Ease popEase = Ease.OutBack;

        // ── Runtime ──────────────────────────────────────────────────────────
        private bool isUnlocked = false;

        private string SaveKey => string.IsNullOrEmpty(saveKeyOverride)
            ? $"RTSceneObjectUnlock_{unlockData?.UnlockName ?? gameObject.name}"
            : saveKeyOverride;

        public bool IsUnlocked => isUnlocked;
        public RTSceneObjectUnlockData UnlockData => unlockData;
        public int UnlockCost => unlockData != null ? unlockData.UnlockCost : 0;

        public event Action OnUnlocked;
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

        /// <summary>Called by RTSceneObjectUnlockSpot when payment is complete.</summary>
        public void CompleteUnlock()
        {
            if (isUnlocked) return;

            isUnlocked = true;
            SaveUnlockState();

            HideUnlockSpot();

            // Hide placeholder objects
            foreach (var obj in objectsToHide)
                if (obj != null) obj.SetActive(false);

            // Enable unlocked objects with pop animation
            foreach (var obj in objectsToEnable)
                if (obj != null) ActivateWithPop(obj);

            OnUnlocked?.Invoke();
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
            }
            else
            {
                // Disable until unlocked
                foreach (var obj in objectsToEnable)
                    if (obj != null) obj.SetActive(false);

                foreach (var obj in objectsToHide)
                    if (obj != null) obj.SetActive(true);
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
                .OnComplete(() => obj.transform.DOPunchRotation(new Vector3(0, 0, 5f), 0.3f, 10, 1f));
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
