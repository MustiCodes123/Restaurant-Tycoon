using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GoogleMobileAds.Api;

/// <summary>
/// Daily spin wheel with a 24-hour cooldown and one optional rewarded-ad extra spin per day.
///
/// SETUP IN INSPECTOR:
///   1. Assign wheelTransform  → the Transform of the wheel image that physically rotates.
///   2. Fill prizeAmounts[]    → one cash value per segment, index 0 = top segment, clockwise.
///      The count must match the number of visual segments on your wheel prefab.
///   3. Assign spinButton      → the "Spin!" button.
///   4. Assign adSpinButton    → the "Free Spin (Ad)" button.
///   5. Assign timerText       → TMP text that shows the countdown / "Spin Now!".
///   6. (Optional) assign rewardPopupObject + rewardPopupText for a "+$N" popup.
///   7. Set spinCurve to a fast-in / ease-out shape for a natural deceleration feel.
///   8. If your segment 0 is NOT at 12 o'clock when wheel Z = 0, set segmentZeroOffsetDeg
///      to the angle (degrees) that brings segment 0 to the top.
/// </summary>
public class DailySpinWheel : MonoBehaviour
{
    // ── Wheel ─────────────────────────────────────────────────────────────────

    [Header("Wheel")]
    [SerializeField] private Transform wheelTransform;

    [Tooltip("Animation curve: X = normalised time (0→1), Y = normalised angle (0→1).\nUse an ease-out / deceleration shape.")]
    [SerializeField] private AnimationCurve spinCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Total seconds the wheel spins before stopping.")]
    [SerializeField] private float spinDuration = 4f;

    [Tooltip("Number of complete 360° rotations before landing on the result.")]
    [SerializeField] private int fullRotations = 6;

    [Tooltip("Angle offset (degrees) if your segment-0 is not at 12 o'clock when the wheel Z = 0.")]
    [SerializeField] private float segmentZeroOffsetDeg = 0f;

    // ── Prizes ────────────────────────────────────────────────────────────────

    [Header("Prizes")]
    [Tooltip("Cash reward per segment. Index 0 = top segment, going clockwise.\nCount must match number of visual segments on the wheel.")]
    [SerializeField] private int[] prizeAmounts = { 50, 100, 25, 200, 50, 75, 150, 25 };

    // ── Buttons ───────────────────────────────────────────────────────────────

    [Header("Buttons")]
    [SerializeField] private Button spinButton;
    [SerializeField] private Button adSpinButton;

    // ── Timer UI ──────────────────────────────────────────────────────────────

    [Header("Timer")]
    [Tooltip("Displays the countdown or 'Spin Now!' when the cooldown is over.")]
    [SerializeField] private TextMeshProUGUI timerText;

    // ── Reward Popup ──────────────────────────────────────────────────────────

    [Header("Reward Popup (optional)")]
    [SerializeField] private GameObject rewardPopupObject;
    [SerializeField] private TextMeshProUGUI rewardPopupText;

    // ── Cooldown ──────────────────────────────────────────────────────────────

    [Header("Cooldown")]
    [Tooltip("Hours between free daily spins.")]
    [SerializeField] private double cooldownHours = 24.0;

    // ── PlayerPrefs Keys ──────────────────────────────────────────────────────

    private const string KEY_LAST_SPIN = "DailySpin_LastSpinTime";

    // ── Internal State ────────────────────────────────────────────────────────

    private bool      _isSpinning;
    private Coroutine _countdownCoroutine;
    private bool      _initialized;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        spinButton.onClick.AddListener(OnSpinPressed);
        adSpinButton.onClick.AddListener(OnAdSpinPressed);

        if (rewardPopupObject != null)
            rewardPopupObject.SetActive(false);

        _initialized = true;
        RefreshState();
    }

    private void OnEnable()
    {
        // Re-evaluate every time the panel is opened (e.g. returning from background).
        if (_initialized)
            RefreshState();
    }

    private void OnDisable()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
    }

    // ── State Management ──────────────────────────────────────────────────────

    /// <summary>Reads PlayerPrefs and sets all UI elements to the correct state.</summary>
    private void RefreshState()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }

        TimeSpan? remaining = GetCooldownRemaining(KEY_LAST_SPIN);

        if (remaining == null)
        {
            // Cooldown expired or player has never spun.
            EnableSpin();
            SetTimerText("Spin Now!");
        }
        else
        {
            DisableSpin(showAdButton: true);
            _countdownCoroutine = StartCoroutine(CountdownCoroutine(remaining.Value));
        }
    }

    /// <summary>
    /// Returns the time remaining in the cooldown, or null when it has expired / never started.
    /// </summary>
    private TimeSpan? GetCooldownRemaining(string prefsKey)
    {
        string saved = PlayerPrefs.GetString(prefsKey, "");
        if (string.IsNullOrEmpty(saved))
            return null;

        if (!DateTime.TryParse(saved, null, DateTimeStyles.RoundtripKind, out DateTime lastTime))
            return null;

        TimeSpan elapsed  = DateTime.UtcNow - lastTime;
        TimeSpan cooldown = TimeSpan.FromHours(cooldownHours);
        return elapsed >= cooldown ? null : cooldown - elapsed;
    }

    private void EnableSpin()
    {
        spinButton.interactable = true;
        adSpinButton.gameObject.SetActive(false);
    }

    private void DisableSpin(bool showAdButton)
    {
        spinButton.interactable = false;
        adSpinButton.gameObject.SetActive(showAdButton);
    }

    private void SetTimerText(string msg)
    {
        if (timerText != null)
            timerText.text = msg;
    }

    // ── Countdown Coroutine ───────────────────────────────────────────────────

    private IEnumerator CountdownCoroutine(TimeSpan remaining)
    {
        while (remaining.TotalSeconds > 0.0)
        {
            SetTimerText($"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}");
            yield return new WaitForSeconds(1f);
            remaining -= TimeSpan.FromSeconds(1.0);
        }

        _countdownCoroutine = null;
        EnableSpin();
        SetTimerText("Spin Now!");
    }

    // ── Button Handlers ───────────────────────────────────────────────────────

    private void OnSpinPressed()
    {
        if (_isSpinning) return;
        StartCoroutine(SpinCoroutine(adGranted: false));
    }

    private void OnAdSpinPressed()
    {
        if (_isSpinning) return;

        if (AdsManager.Instance == null)
        {
            Debug.LogWarning("[DailySpinWheel] AdsManager not found. Make sure it is in the scene.");
            return;
        }

        // Pre-load then show; AdsManager handles the "not ready" case gracefully.
        AdsManager.Instance.LoadRewardedAd();
        AdsManager.Instance.ShowRewardedAd(
            onRewardEarned: _ => OnAdRewardGranted(),
            onClosed: null
        );
    }

    private void OnAdRewardGranted()
    {
        adSpinButton.gameObject.SetActive(false);
        spinButton.interactable = true;
        StartCoroutine(SpinCoroutine(adGranted: true));
    }

    // ── Spin Coroutine ────────────────────────────────────────────────────────

    private IEnumerator SpinCoroutine(bool adGranted)
    {
        if (prizeAmounts == null || prizeAmounts.Length == 0)
        {
            Debug.LogError("[DailySpinWheel] prizeAmounts is empty. Assign values in the Inspector.");
            yield break;
        }

        _isSpinning = true;
        spinButton.interactable = false;
        adSpinButton.gameObject.SetActive(false);
        if (rewardPopupObject != null) rewardPopupObject.SetActive(false);

        // ── Determine winning segment ─────────────────────────────────────────
        int   segCount  = prizeAmounts.Length;
        int   winIndex  = UnityEngine.Random.Range(0, segCount);
        float segAngle  = 360f / segCount;

        float startZ = wheelTransform.eulerAngles.z;

        // Compute the exact extra degrees to spin (beyond full rotations) so that
        // segment winIndex lands centred under the top pointer.
        // Uses the wheel's CURRENT Z so the result is correct across multiple spins.
        //
        //   After a CW rotation of X degrees, a segment at angle A moves to (A + X) % 360.
        //   For it to reach 0° (top): X = (360 - A % 360) % 360.
        //   A = winIndex * segAngle, adjusted by current orientation (startZ, CCW positive).
        float segCurrentAngle = (winIndex * segAngle - startZ % 360f + 360f) % 360f;
        float targetExtraAngle = (360f - segCurrentAngle + segmentZeroOffsetDeg) % 360f;
        float spinAmount = fullRotations * 360f + targetExtraAngle;
        float endZ = startZ - spinAmount; // negative Z = clockwise in Unity

        // ── Animate ───────────────────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < spinDuration)
        {
            float t      = elapsed / spinDuration;
            float curveT = spinCurve.Evaluate(t);
            wheelTransform.eulerAngles = new Vector3(
                0f,
                0f,
                Mathf.LerpUnclamped(startZ, endZ, curveT)
            );
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap to exact final angle to eliminate floating-point drift.
        wheelTransform.eulerAngles = new Vector3(0f, 0f, endZ);
        _isSpinning = false;

        // ── Grant reward ──────────────────────────────────────────────────────
        int prize = prizeAmounts[winIndex];
        CurrencyManager.Instance.AddMoney(prize);
        ShowRewardPopup(prize);

        Debug.Log($"[DailySpinWheel] Won segment {winIndex} → +${prize}");

        // ── Update cooldown state ─────────────────────────────────────────────
        if (!adGranted)
        {
            // Regular daily spin: start the 24 h cooldown.
            PlayerPrefs.SetString(KEY_LAST_SPIN, DateTime.UtcNow.ToString("O"));
            PlayerPrefs.Save();

            DisableSpin(showAdButton: true);
            _countdownCoroutine = StartCoroutine(
                CountdownCoroutine(TimeSpan.FromHours(cooldownHours))
            );
        }
        else
        {
            // Ad-granted spin: daily cooldown continues; keep ad button available.
            DisableSpin(showAdButton: true);
        }
    }

    // ── Reward Popup ──────────────────────────────────────────────────────────

    private void ShowRewardPopup(int amount)
    {
        if (rewardPopupObject == null) return;
        if (rewardPopupText != null) rewardPopupText.text = $"+${amount}";
        rewardPopupObject.SetActive(true);
    }

    /// <summary>
    /// Closes the reward popup. Bind this to the popup's OK / close button.
    /// </summary>
    public void CloseRewardPopup()
    {
        if (rewardPopupObject != null)
            rewardPopupObject.SetActive(false);
    }

    // ── Debug / Context-Menu Helpers ──────────────────────────────────────────

    [ContextMenu("Debug — Reset All Spin Cooldowns")]
    private void DEBUG_ResetCooldown()
    {
        PlayerPrefs.DeleteKey(KEY_LAST_SPIN);
        PlayerPrefs.Save();
        RefreshState();
        Debug.Log("[DailySpinWheel] All cooldowns reset.");
    }

    [ContextMenu("Debug — Simulate Spin Used (Start Cooldown Now)")]
    private void DEBUG_SimulateSpinUsed()
    {
        PlayerPrefs.SetString(KEY_LAST_SPIN, DateTime.UtcNow.ToString("O"));
        PlayerPrefs.Save();
        RefreshState();
        Debug.Log("[DailySpinWheel] Simulated a spin — cooldown started.");
    }
}
