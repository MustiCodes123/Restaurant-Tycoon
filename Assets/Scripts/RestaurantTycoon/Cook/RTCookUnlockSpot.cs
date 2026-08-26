using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace RestaurantTycoon
{
    /// <summary>
    /// The physical unlock spot the player stands at to pay for an RT cook.
    /// Walk in → money drains at paymentRate/s → cook activates on completion.
    /// </summary>
    public class RTCookUnlockSpot : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private LayerMask playerLayer;

        [Header("UI")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI cookNameText;
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
        private RTCookUnlock cookUnlock;
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

        // ── Unity ─────────────────────────────────────────────────────────────

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
            if (!isPlayerInRange || !isPaymentActive || cookUnlock == null) return;

            paymentTimer += Time.deltaTime;
            if (paymentTimer >= paymentInterval)
            {
                paymentTimer = 0f;
                ProcessPayment();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Show(RTCookUnlock unlock)
        {
            if (unlock == null) return;

            cookUnlock = unlock;
            totalCost = unlock.UnlockCost;
            currentPayment = unlock.LoadPaymentProgress();
            isPaymentActive = false;

            if (cookNameText != null && unlock.UnlockData != null)
                cookNameText.text = unlock.UnlockData.CookName;

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

            StartPulse();
        }

        public void Hide()
        {
            RTSpotRegistry.UnregisterSpot(transform);
            StopPulse();

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

            // Reset payment state
            isPaymentActive = false;
            isPlayerInRange = false;
            playerTransform = null;
            currentPayment = 0;
            paymentTimer = 0f;
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

        // ── Payment ───────────────────────────────────────────────────────────

        private void ProcessPayment()
        {
            if (cookUnlock == null || CurrencyManager.Instance == null) return;

            int remaining = totalCost - currentPayment;
            if (remaining <= 0)
            {
                CompletePayment();
                return;
            }

            int amount = Mathf.Min(Mathf.CeilToInt(paymentRate * paymentInterval), remaining);
            amount = Mathf.Min(amount, CurrencyManager.Instance.CurrentMoney);
            if (amount <= 0) return;

        if (CurrencyManager.Instance.SpendMoney(amount))
        {
            currentPayment += amount;
            cookUnlock.SavePaymentProgress(currentPayment);
            UpdateUI();

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

            cookUnlock?.ClearPaymentProgress();
            cookUnlock?.CompleteUnlock();
        }

        // ── UI ────────────────────────────────────────────────────────────────

        private void UpdateUI()
        {
            int remaining = Mathf.Max(0, totalCost - currentPayment);

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
            pulseTween?.Kill();
            isPaymentActive = false;
        }
    }
}
