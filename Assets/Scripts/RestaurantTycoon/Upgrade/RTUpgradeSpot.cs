using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// Physical interaction spot for staff upgrades.
    /// Player walks in → money drains at paymentRate/s with a money flow animation
    /// → image fill tracks progress → upgrade completes when fully paid.
    /// Mirrors RTCookUnlockSpot's payment pattern exactly.
    /// </summary>
    public class RTUpgradeSpot : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private LayerMask playerLayer;

        [Header("UI")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI durationText;
        [SerializeField] private Image progressFillImage;

        [Header("Payment")]
        [Tooltip("Money deducted per second while the player stands here.")]
        [SerializeField] private float paymentRate = 50f;
        [SerializeField] private float paymentInterval = 0.1f;

        [Header("Money Flow Effect")]
        [SerializeField] private MoneyFlowEffect moneyFlowEffect;
        [SerializeField] private float moneyFlowInterval = 0.15f;

        [Header("Show / Hide Animation")]
        [SerializeField] private float showDuration = 0.25f;
        [SerializeField] private float hideDuration = 0.2f;
        [SerializeField] private Ease showEase = Ease.OutBack;
        [SerializeField] private Ease hideEase = Ease.InBack;

        [Header("Pulse Animation")]
        [SerializeField] private float pulseScale = 1.1f;
        [SerializeField] private float pulseDuration = 0.5f;
        [SerializeField] private Ease pulseEase = Ease.InOutSine;

        // ── Runtime ───────────────────────────────────────────────────────────
        private RTStaffUpgrade owner;
        private int totalCost;
        private int currentPayment;
        private bool isPlayerInRange;
        private bool isPaymentActive;
        private float paymentTimer;
        private float lastMoneyFlowTime;
        private Transform playerTransform;
        private Tween pulseTween;
        private Tween showHideTween;
        private Vector3 originalCanvasScale;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (canvas != null)
            {
                originalCanvasScale = canvas.transform.localScale;
                canvas.transform.localScale = Vector3.zero;
                canvas.gameObject.SetActive(false);
            }

            if (progressFillImage != null)
                progressFillImage.fillAmount = 0f;

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isPlayerInRange || !isPaymentActive || owner == null) return;

            paymentTimer += Time.deltaTime;
            if (paymentTimer >= paymentInterval)
            {
                paymentTimer = 0f;
                ProcessPayment();
            }
        }

        private void OnDestroy()
        {
            pulseTween?.Kill();
            showHideTween?.Kill();
        }

        // ── Trigger ───────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            isPlayerInRange = true;
            playerTransform = other.transform;
            paymentTimer = 0f;
            isPaymentActive = true;
            StopPulse();
        }

        private void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            isPlayerInRange = false;
            playerTransform = null;
            isPaymentActive = false;
            StartPulse();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Activates and configures the spot for the given upgrade state.</summary>
        public void Show(RTStaffUpgrade upgrade)
        {
            if (upgrade == null) return;

            // Clean up any leftover money icons from the previous payment cycle.
            moneyFlowEffect?.ForceCleanup();

            owner = upgrade;
            totalCost = upgrade.NextLevel != null ? upgrade.NextLevel.cost : 0;
            currentPayment = upgrade.LoadPaymentProgress();
            isPaymentActive = false;

            RefreshUI();

            RTSpotRegistry.RegisterSpot(transform);
            gameObject.SetActive(true);

            if (canvas != null)
            {
                canvas.gameObject.SetActive(true);
                showHideTween?.Kill();
                showHideTween = canvas.transform
                    .DOScale(originalCanvasScale, showDuration)
                    .SetEase(showEase);
            }

            StartPulse();
        }

        /// <summary>Hides and deactivates the spot.</summary>
        public void Hide()
        {
            RTSpotRegistry.UnregisterSpot(transform);
            StopPulse();
            moneyFlowEffect?.ForceCleanup();
            isPaymentActive = false;
            isPlayerInRange = false;
            playerTransform = null;
            currentPayment = 0;
            paymentTimer = 0f;

            if (canvas != null && canvas.gameObject.activeSelf)
            {
                showHideTween?.Kill();
                showHideTween = canvas.transform
                    .DOScale(Vector3.zero, hideDuration)
                    .SetEase(hideEase)
                    .OnComplete(() =>
                    {
                        canvas.gameObject.SetActive(false);
                        gameObject.SetActive(false);
                    });
            }
            else
            {
                gameObject.SetActive(false);
            }

            owner = null;
        }

        // ── Payment ───────────────────────────────────────────────────────────

        private void ProcessPayment()
        {
            if (owner == null || CurrencyManager.Instance == null) return;

            int remaining = totalCost - currentPayment;
            if (remaining <= 0) { CompletePayment(); return; }

            int amount = Mathf.Min(Mathf.CeilToInt(paymentRate * paymentInterval), remaining);
            amount = Mathf.Min(amount, CurrencyManager.Instance.CurrentMoney);
            if (amount <= 0) return;

        if (CurrencyManager.Instance.SpendMoney(amount))
        {
            currentPayment += amount;
            owner.SavePaymentProgress(currentPayment);
            RefreshUI();

                if (moneyFlowEffect != null && playerTransform != null &&
                    Time.time - lastMoneyFlowTime >= moneyFlowInterval)
                {
                    lastMoneyFlowTime = Time.time;
                    moneyFlowEffect.SpawnMoneyToTarget(
                        playerTransform.position,
                        canvas != null ? canvas.transform.position : transform.position,
                        amount);
                }

                if (currentPayment >= totalCost)
                    CompletePayment();
            }
        }

        private void CompletePayment()
        {
            isPaymentActive = false;
            StopPulse();

            if (canvas != null)
            {
                showHideTween?.Kill();
                canvas.transform.DOScale(Vector3.zero, hideDuration).SetEase(hideEase);
            }

            owner?.CompleteUpgrade();
        }

        // ── UI ────────────────────────────────────────────────────────────────

        private void RefreshUI()
        {
            if (owner == null) return;

            var nextLevel = owner.NextLevel;
            if (nextLevel == null) return;

            int remaining = Mathf.Max(0, totalCost - currentPayment);

            if (costText != null)
                costText.text = $"${remaining}";

            if (levelText != null)
                levelText.text = $"Lvl {owner.CurrentLevel + 1}";

            if (durationText != null)
                durationText.text = $"{nextLevel.newDuration:0.0}s";

            if (progressFillImage != null)
                progressFillImage.fillAmount = totalCost > 0 ? (float)currentPayment / totalCost : 0f;
        }

        // ── Animation ─────────────────────────────────────────────────────────

        private void StartPulse()
        {
            if (canvas == null) return;
            pulseTween?.Kill();
            pulseTween = canvas.transform
                .DOScale(originalCanvasScale * pulseScale, pulseDuration)
                .SetEase(pulseEase)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopPulse()
        {
            pulseTween?.Kill();
            if (canvas != null)
                canvas.transform.localScale = originalCanvasScale;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
