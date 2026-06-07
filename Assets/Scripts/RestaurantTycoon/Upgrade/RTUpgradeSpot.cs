using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Physical interaction spot for staff upgrades.
    /// Player walks in, stands still while the radial fills, upgrade is purchased.
    /// Exposes Show()/Hide() so RTStaffUpgrade can control visibility;
    /// uses OnEnable/OnDisable so RTSceneObjectUnlock toggling works correctly.
    /// </summary>
    public class RTUpgradeSpot : MonoBehaviour
    {
        [Header("Player Detection")]
        // Detects RTPlayerController — no layer mask needed.

        [Header("UI")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private GameObject uiRoot;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI durationText;

        [Header("Radial Progress")]
        [Tooltip("RadialProgressUI on this spot (or a child).")]
        [SerializeField] private RadialProgressUI radialProgressUI;
        [Tooltip("Seconds the player must stand still to purchase.")]
        [SerializeField] private float interactDuration = 2f;

        [Header("Pulse Animation")]
        [SerializeField] private float pulseMin = 0.95f;
        [SerializeField] private float pulseMax = 1.05f;
        [SerializeField] private float pulseDuration = 0.7f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private RTStaffUpgrade owner;
        private bool playerInRange;
        private bool isPurchasing;
        private Coroutine purchaseCoroutine;
        private Tween pulseTween;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (radialProgressUI == null)
                radialProgressUI = GetComponentInChildren<RadialProgressUI>();

            HideUI();
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            playerInRange = false;
            isPurchasing = false;
        }

        private void OnDisable()
        {
            CancelPurchase();
            StopPulse();
        }

        private void OnDestroy()
        {
            pulseTween?.Kill();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<RTPlayerController>() == null) return;
            playerInRange = true;

            if (owner != null && owner.CanUpgrade)
                BeginPurchase();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<RTPlayerController>() == null) return;
            playerInRange = false;
            CancelPurchase();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Activates and configures the spot for the given upgrade state.</summary>
        public void Show(RTStaffUpgrade upgrade)
        {
            owner = upgrade;
            gameObject.SetActive(true);
            RefreshUI();
            StartPulse();

            // If player is already standing in the trigger, start immediately.
            if (playerInRange && owner.CanUpgrade)
                BeginPurchase();
        }

        /// <summary>Hides and deactivates the spot.</summary>
        public void Hide()
        {
            CancelPurchase();
            StopPulse();
            gameObject.SetActive(false);
            owner = null;
        }

        /// <summary>Call after an upgrade completes so UI refreshes or spot hides.</summary>
        public void OnUpgradeCompleted()
        {
            if (owner == null || !owner.CanUpgrade)
            {
                Hide();
                return;
            }

            RefreshUI();

            // If player is still in range, start next upgrade immediately.
            if (playerInRange)
                BeginPurchase();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void BeginPurchase()
        {
            if (isPurchasing || owner == null || !owner.CanUpgrade) return;

            isPurchasing = true;
            StopPulse();

            if (radialProgressUI != null)
            {
                radialProgressUI.SetFillDuration(interactDuration);
                radialProgressUI.StartProgress();
            }

            purchaseCoroutine = StartCoroutine(PurchaseRoutine());
        }

        private System.Collections.IEnumerator PurchaseRoutine()
        {
            yield return new WaitForSeconds(interactDuration);

            if (!isPurchasing) yield break; // Cancelled while waiting.

            if (radialProgressUI != null)
                radialProgressUI.StopProgress();

            isPurchasing = false;
            purchaseCoroutine = null;

            owner?.CompleteUpgrade();
            OnUpgradeCompleted();
        }

        private void CancelPurchase()
        {
            if (!isPurchasing) return;

            isPurchasing = false;

            if (purchaseCoroutine != null)
            {
                StopCoroutine(purchaseCoroutine);
                purchaseCoroutine = null;
            }

            if (radialProgressUI != null)
                radialProgressUI.StopProgress();

            StartPulse();
        }

        private void RefreshUI()
        {
            if (owner == null) return;

            var nextLevel = owner.NextLevel;
            if (nextLevel == null) { HideUI(); return; }

            ShowUI();

            if (costText != null)
                costText.text = $"${nextLevel.cost}";

            if (levelText != null)
                levelText.text = $"Lvl {owner.CurrentLevel + 1}";

            if (durationText != null)
                durationText.text = $"{nextLevel.newDuration:0.0}s";
        }

        private void ShowUI()
        {
            if (uiRoot != null) uiRoot.SetActive(true);
            if (worldCanvas != null) worldCanvas.enabled = true;
        }

        private void HideUI()
        {
            if (uiRoot != null) uiRoot.SetActive(false);
            if (worldCanvas != null) worldCanvas.enabled = false;
        }

        private void StartPulse()
        {
            if (uiRoot == null) return;
            pulseTween?.Kill();
            uiRoot.transform.localScale = Vector3.one;
            pulseTween = uiRoot.transform
                .DOScale(Vector3.one * pulseMax, pulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopPulse()
        {
            pulseTween?.Kill();
            if (uiRoot != null)
                uiRoot.transform.localScale = Vector3.one;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
