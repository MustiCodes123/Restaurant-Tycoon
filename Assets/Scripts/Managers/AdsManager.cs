using System;
using UnityEngine;
using GoogleMobileAds.Api;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    [Header("App IDs (set in AdMob Settings via Assets > Google Mobile Ads)")]

    [Header("Banner Ad")]
    [SerializeField] private AdPosition bannerPosition = AdPosition.Bottom;
#if UNITY_ANDROID
    [SerializeField] private string bannerAdUnitId = "ca-app-pub-3940256099942544/6300978111"; // Test ID
#elif UNITY_IPHONE
    [SerializeField] private string bannerAdUnitId = "ca-app-pub-3940256099942544/2934735716"; // Test ID
#else
    [SerializeField] private string bannerAdUnitId = "unused";
#endif

    [Header("Interstitial Ad")]
    [SerializeField] private float interstitialIntervalSeconds = 300f; // 5 minutes default
#if UNITY_ANDROID
    [SerializeField] private string interstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712"; // Test ID
#elif UNITY_IPHONE
    [SerializeField] private string interstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910"; // Test ID
#else
    [SerializeField] private string interstitialAdUnitId = "unused";
#endif

    [Header("Rewarded Ad")]
#if UNITY_EDITOR || UNITY_ANDROID
    [SerializeField] private string rewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917"; // Test ID
#elif UNITY_IPHONE
    [SerializeField] private string rewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313"; // Test ID
#else
    [SerializeField] private string rewardedAdUnitId = "unused";
#endif

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // ── Internal ──────────────────────────────────────────────────────────────
    private BannerView    _bannerView;
    private InterstitialAd _interstitialAd;
    private RewardedAd    _rewardedAd;

    private float _interstitialTimer;
    private bool  _isBannerLoaded;
    private bool  _isInitialized;

    // Rewarded callback (set by the caller before showing the ad)
    private Action<Reward> _onRewardEarned;
    private Action         _onRewardedAdClosed;
    private Action<Reward> _pendingRewardedEarned;
    private Action         _pendingRewardedClosed;
    private bool           _isRewardedAdLoading;
    private bool           _pendingRewardedShow;

    public bool IsRewardedAdReady => _rewardedAd != null && _rewardedAd.CanShowAd();

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        MobileAds.Initialize(initStatus =>
        {
            _isInitialized = true;
            Log("MobileAds initialized.");
            LoadBannerAd();
            LoadInterstitialAd();
            LoadRewardedAd();
        });

        _interstitialTimer = interstitialIntervalSeconds;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        _interstitialTimer -= Time.deltaTime;
        if (_interstitialTimer <= 0f)
        {
            _interstitialTimer = interstitialIntervalSeconds;
            ShowInterstitialAd();
        }
    }

    // ── Banner ────────────────────────────────────────────────────────────────

    private void LoadBannerAd()
    {
        if (_bannerView != null)
        {
            _bannerView.Destroy();
            _bannerView = null;
        }

        _bannerView = new BannerView(bannerAdUnitId, AdSize.Banner, bannerPosition);

        _bannerView.OnBannerAdLoaded += () =>
        {
            _isBannerLoaded = true;
            Log("Banner ad loaded.");
        };

        _bannerView.OnBannerAdLoadFailed += error =>
        {
            _isBannerLoaded = false;
            LogWarning("Banner ad failed to load: " + error);
        };

        _bannerView.LoadAd(new AdRequest());
    }

    /// <summary>Shows the banner. Call this when you want the banner visible.</summary>
    public void ShowBannerAd()
    {
        if (_bannerView == null)
        {
            LoadBannerAd();
            return;
        }
        _bannerView.Show();
    }

    /// <summary>Hides the banner without destroying it.</summary>
    public void HideBannerAd()
    {
        _bannerView?.Hide();
    }

    /// <summary>Destroys the banner and reloads a fresh one.</summary>
    public void ReloadBannerAd()
    {
        LoadBannerAd();
    }

    // ── Interstitial ──────────────────────────────────────────────────────────

    private void LoadInterstitialAd()
    {
        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }

        InterstitialAd.Load(interstitialAdUnitId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                LogWarning("Interstitial ad failed to load: " + error);
                return;
            }

            _interstitialAd = ad;
            RegisterInterstitialEvents(_interstitialAd);
            Log("Interstitial ad loaded.");
        });
    }

    private void RegisterInterstitialEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            Log("Interstitial ad closed. Reloading...");
            LoadInterstitialAd();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            LogWarning("Interstitial ad failed to show: " + error);
            LoadInterstitialAd();
        };
    }

    /// <summary>
    /// Shows the interstitial ad if one is ready.
    /// Also called automatically every <see cref="interstitialIntervalSeconds"/> seconds.
    /// </summary>
    public void ShowInterstitialAd()
    {
        if (_interstitialAd != null && _interstitialAd.CanShowAd())
        {
            Log("Showing interstitial ad.");
            _interstitialAd.Show();
        }
        else
        {
            Log("Interstitial ad not ready, reloading...");
            LoadInterstitialAd();
        }
    }

    /// <summary>Resets the interstitial timer (e.g. after a manual show).</summary>
    public void ResetInterstitialTimer()
    {
        _interstitialTimer = interstitialIntervalSeconds;
    }

    // ── Rewarded (reserved for future use) ───────────────────────────────────

    /// <summary>Pre-loads a rewarded ad so it's ready when needed.</summary>
    public void LoadRewardedAd()
    {
        if (!_isInitialized)
        {
            Log("Rewarded ad load requested before MobileAds initialization.");
            return;
        }

        if (_isRewardedAdLoading)
            return;

        _isRewardedAdLoading = true;

        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        RewardedAd.Load(rewardedAdUnitId, new AdRequest(), (ad, error) =>
        {
            _isRewardedAdLoading = false;

            if (error != null || ad == null)
            {
                LogWarning("Rewarded ad failed to load: " + error);
                return;
            }

            _rewardedAd = ad;
            Log("Rewarded ad loaded.");

            if (_pendingRewardedShow)
            {
                ShowLoadedRewardedAd(_pendingRewardedEarned, _pendingRewardedClosed);
                _pendingRewardedShow = false;
                _pendingRewardedEarned = null;
                _pendingRewardedClosed = null;
            }
        });
    }

    /// <summary>
    /// Shows a rewarded ad.
    /// </summary>
    /// <param name="onRewardEarned">Called with the reward when the user earns it.</param>
    /// <param name="onClosed">Called when the ad closes (reward may or may not have been earned).</param>
    public void ShowRewardedAd(Action<Reward> onRewardEarned, Action onClosed = null)
    {
        if (!_isInitialized)
        {
            Log("Rewarded ad show requested before MobileAds initialization. Queuing request.");
            QueueRewardedShow(onRewardEarned, onClosed);
            return;
        }

        if (!IsRewardedAdReady)
        {
            Log("Rewarded ad is not ready. Loading and queuing show request.");
            QueueRewardedShow(onRewardEarned, onClosed);
            LoadRewardedAd();
            return;
        }

        ShowLoadedRewardedAd(onRewardEarned, onClosed);
    }

    private void QueueRewardedShow(Action<Reward> onRewardEarned, Action onClosed)
    {
        _pendingRewardedEarned = onRewardEarned;
        _pendingRewardedClosed = onClosed;
        _pendingRewardedShow = true;
    }

    private void ShowLoadedRewardedAd(Action<Reward> onRewardEarned, Action onClosed)
    {
        if (!IsRewardedAdReady)
        {
            QueueRewardedShow(onRewardEarned, onClosed);
            LoadRewardedAd();
            return;
        }

        _onRewardEarned   = onRewardEarned;
        _onRewardedAdClosed = onClosed;

        _rewardedAd.OnAdFullScreenContentClosed += () =>
        {
            Log("Rewarded ad closed. Reloading...");
            _onRewardedAdClosed?.Invoke();
            LoadRewardedAd();
        };

        _rewardedAd.OnAdFullScreenContentFailed += error =>
        {
            LogWarning("Rewarded ad failed to show: " + error);
            _onRewardedAdClosed?.Invoke();
            LoadRewardedAd();
        };

        _rewardedAd.Show(reward =>
        {
            Log($"Reward earned: {reward.Amount} {reward.Type}");
            _onRewardEarned?.Invoke(reward);
        });
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        _bannerView?.Destroy();
        _interstitialAd?.Destroy();
        _rewardedAd?.Destroy();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Log(string msg)
    {
        if (showDebugLogs) Debug.Log($"[AdsManager] {msg}");
    }

    private void LogWarning(string msg)
    {
        if (showDebugLogs) Debug.LogWarning($"[AdsManager] {msg}");
    }
}
