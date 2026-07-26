using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// The physical unlock spot the player stands at to pay for the RT janitor.
    /// Walk in → payment ticks down → janitor unlocks.
    /// Walk away → payment pauses (progress is kept).
    /// </summary>
    public class RTJanitorUnlockSpot : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private LayerMask playerLayer;

        [Header("UI")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image progressFillImage;

        [Header("Payment")]
        [Tooltip("Money deducted per second while the player stands here.")]
        [SerializeField] private float paymentRate = 50f;
        [SerializeField] private float paymentInterval = 0.1f;

        [Header("Money Flow Effect")]
        [SerializeField] private MoneyFlowEffect moneyFlowEffect;
        [SerializeField] private float moneyFlowInterval = 0.15f;

        [Header("Visual Feedback")]
        [SerializeField] private float pulseScale = 1.1f;
        [SerializeField] private float pulseDuration = 0.5f;
        [SerializeField] private Ease pulseEase = Ease.InOutSine;

        [Header("Show / Hide Animation")]
        [SerializeField] private float showDuration = 0.25f;
        [SerializeField] private float hideDuration = 0.2f;
        [SerializeField] private Ease showEase = Ease.OutBack;
        [SerializeField] private Ease hideEase = Ease.InBack;

        // ── Runtime ───────────────────────────────────────────────────────────
        private RTJanitorUnlock janitorUnlock;
        private int currentPayment;
        private int totalCost;
        private bool isPlayerInRange;
        private bool isPaymentActive;
        private float paymentTimer;
        private float lastMoneyFlowTime;
        private Tween pulseTween;
        private Tween showHideTween;
        private Transform playerTransform;
        private Vector3 originalCanvasScale;

        // ── Unity ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (canvas != null)
            {
                originalCanvasScale = canvas.transform.localScale;
                canvas.transform.localScale = Vector3.zero;
                canvas.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        private void Start()
        {
            if (progressFillImage != null)
                progressFillImage.fillAmount = 0f;
        }

        private void Update()
        {
            if (!isPlayerInRange || !isPaymentActive || janitorUnlock == null) return;

            paymentTimer += Time.deltaTime;
            if (paymentTimer >= paymentInterval)
            {
                paymentTimer = 0f;
                ProcessPayment();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Show(RTJanitorUnlock unlock)
        {
            if (unlock == null) return;

            janitorUnlock = unlock;
            totalCost = unlock.UnlockCost;
            currentPayment = 0;
            isPaymentActive = false;

            if (nameText != null && unlock.UnlockData != null)
                nameText.text = unlock.UnlockData.JanitorName;

            UpdateUI();

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
        }

        public void Hide()
        {
            RTSpotRegistry.UnregisterSpot(transform);
            StopPayment();

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
        }

        // ── Trigger ───────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            isPlayerInRange = true;
            playerTransform = other.transform;
            paymentTimer = 0f;
            StartPayment();
        }

        private void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            isPlayerInRange = false;
            playerTransform = null;
            StopPayment();
        }

        // ── Payment ───────────────────────────────────────────────────────────

        private void StartPayment()
        {
            if (isPaymentActive || janitorUnlock == null || janitorUnlock.IsUnlocked) return;
            isPaymentActive = true;
            StartPulse();
        }

        private void StopPayment()
        {
            isPaymentActive = false;
            StopPulse();
        }

        private void ProcessPayment()
        {
            if (janitorUnlock == null || CurrencyManager.Instance == null) return;

            int remaining = totalCost - currentPayment;
            if (remaining <= 0)
            {
                CompletePayment();
                return;
            }

            int amount = Mathf.Min(Mathf.CeilToInt(paymentRate * paymentInterval), remaining);
            if (amount <= 0) return;

            if (CurrencyManager.Instance.SpendMoney(amount))
            {
                currentPayment += amount;
                UpdateUI();

                // Money flow effect
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

            janitorUnlock?.CompleteUnlock();
        }

        // ── UI ────────────────────────────────────────────────────────────────

        private void UpdateUI()
        {
            int remaining = totalCost - currentPayment;

            if (costText != null)
                costText.text = $"${remaining}";

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

        private void OnDisable()
        {
            StopPayment();
        }
    }
}
