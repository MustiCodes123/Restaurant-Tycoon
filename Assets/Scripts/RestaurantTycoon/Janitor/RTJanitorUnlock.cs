using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Manages the unlock state for a single RT janitor.
    /// Place on a scene GameObject alongside the RTJanitorUnlockSpot.
    /// Persists unlock state via PlayerPrefs.
    /// </summary>
    public class RTJanitorUnlock : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private RTJanitorUnlockData unlockData;
        [SerializeField] private RTJanitorUnlockSpot unlockSpot;

        [Header("Spawn Settings")]
        [Tooltip("Where the janitor spawns when unlocked.")]
        [SerializeField] private Transform spawnPoint;

        [Header("Janitor References")]
        [Tooltip("Dining area passed to the janitor on spawn.")]
        [SerializeField] private RTDiningArea diningArea;
        [Tooltip("Garbage bin transform passed to the janitor on spawn.")]
        [SerializeField] private Transform garbageBinTransform;
        [Tooltip("Idle spot transforms passed to the janitor on spawn.")]
        [SerializeField] private List<Transform> idleSpots = new List<Transform>();

        [Header("Objects to Hide on Unlock")]
        [Tooltip("Placeholder visuals, lock icons, etc.")]
        [SerializeField] private List<GameObject> objectsToHide = new List<GameObject>();

        [Header("Objects to Show on Unlock")]
        [SerializeField] private List<GameObject> objectsToShow = new List<GameObject>();

        [Header("Save Key")]
        [SerializeField] private string saveKeyOverride;

        [Header("Spawn Animation")]
        [SerializeField] private float popDuration = 0.5f;
        [SerializeField] private Ease popEase = Ease.OutBack;

        // ── Runtime ──────────────────────────────────────────────────────────
        private bool isUnlocked = false;
        private RTJanitorController spawnedJanitor;

        private string SaveKey => string.IsNullOrEmpty(saveKeyOverride)
            ? $"RTJanitorUnlock_{unlockData?.JanitorName ?? gameObject.name}"
            : saveKeyOverride;

        public bool IsUnlocked => isUnlocked;
        public RTJanitorUnlockData UnlockData => unlockData;
        public int UnlockCost => unlockData != null ? unlockData.UnlockCost : 0;
        public RTJanitorController SpawnedJanitor => spawnedJanitor;

        public event Action OnJanitorUnlocked;
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
        /// Shows or hides the unlock spot based on player level.
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
        /// Called by RTJanitorUnlockSpot when payment is complete.
        /// </summary>
        public void CompleteUnlock()
        {
            if (isUnlocked) return;

            isUnlocked = true;
            SaveUnlockState();

            HideUnlockSpot();

            foreach (var obj in objectsToHide)
                if (obj != null) obj.SetActive(false);

            foreach (var obj in objectsToShow)
                if (obj != null) ActivateWithPop(obj);

            SpawnJanitor();
            OnJanitorUnlocked?.Invoke();
            Debug.Log($"[RTJanitorUnlock] Janitor '{unlockData?.JanitorName}' unlocked!");
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void SpawnJanitor()
        {
            if (unlockData == null || unlockData.JanitorPrefab == null)
            {
                Debug.LogError("[RTJanitorUnlock] No JanitorPrefab assigned in unlock data!");
                return;
            }

            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            GameObject obj = Instantiate(unlockData.JanitorPrefab, pos, rot);
            spawnedJanitor = obj.GetComponent<RTJanitorController>();

            if (spawnedJanitor == null)
            {
                Debug.LogError("[RTJanitorUnlock] Prefab is missing RTJanitorController!");
                return;
            }

            // Wire up scene references via the Initialize method
            spawnedJanitor.Initialize(diningArea, garbageBinTransform, idleSpots, unlockData.MoveSpeed);

            ActivateWithPop(obj);
            Debug.Log($"[RTJanitorUnlock] Janitor spawned at {pos}");
        }

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
                HideUnlockSpot();

                if (spawnedJanitor == null)
                    SpawnJanitor();

                foreach (var obj in objectsToHide)
                    if (obj != null) obj.SetActive(false);

                foreach (var obj in objectsToShow)
                    if (obj != null) obj.SetActive(true);
            }
            else
            {
                foreach (var obj in objectsToHide)
                    if (obj != null) obj.SetActive(true);

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
            PlayerPrefs.SetInt(SaveKey, isUnlocked ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ActivateWithPop(GameObject obj)
        {
            if (obj == null) return;
            obj.SetActive(true);
            Transform t = obj.transform;
            Vector3 original = t.localScale;
            t.localScale = Vector3.zero;
            t.DOScale(original, popDuration).SetEase(popEase);
        }

        [ContextMenu("Reset Unlock State")]
        public void ResetUnlockState()
        {
            isUnlocked = false;
            SaveUnlockState();

            if (spawnedJanitor != null)
            {
                Destroy(spawnedJanitor.gameObject);
                spawnedJanitor = null;
            }

            ApplyUnlockState();
            CheckUnlockAvailability();
        }
    }
}
