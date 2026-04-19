using UnityEngine;
using GoogleMobileAds.Api;
using System;

public class AdMobManager : MonoBehaviour
{
    public static AdMobManager Instance { get; private set; }
    
    [Header("Ad Settings")]
    [SerializeField] private float interstitialAdIntervalSeconds = 60f;
    
    [Header("Ad Unit IDs")]
#if UNITY_ANDROID
    [SerializeField] private string androidInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712"; // Test ID
#elif UNITY_IOS
    [SerializeField] private string iosInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910"; // Test ID
#else
    private string adUnitId = "unused";
#endif
    
    private InterstitialAd interstitialAd;
    private float adTimer;
    private bool isInitialized = false;
    
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
        InitializeAds();
        adTimer = interstitialAdIntervalSeconds;
    }
    
    private void Update()
    {
        if (!isInitialized) return;
        
        adTimer -= Time.deltaTime;
        
        if (adTimer <= 0f)
        {
            ShowInterstitialAd();
            adTimer = interstitialAdIntervalSeconds;
        }
    }
    
    private void InitializeAds()
    {
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("[AdMobManager] Mobile Ads initialized.");
            isInitialized = true;
            LoadInterstitialAd();
        });
    }
    
    private void LoadInterstitialAd()
    {
        // Clean up the old ad before loading a new one
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }
        
        Debug.Log("[AdMobManager] Loading interstitial ad.");
        
        string adUnitId;
#if UNITY_ANDROID
        adUnitId = androidInterstitialAdUnitId;
#elif UNITY_IOS
        adUnitId = iosInterstitialAdUnitId;
#else
        adUnitId = "unused";
#endif
        
        var adRequest = new AdRequest();
        
        InterstitialAd.Load(adUnitId, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError($"[AdMobManager] Interstitial ad failed to load with error: {error}");
                return;
            }
            
            Debug.Log("[AdMobManager] Interstitial ad loaded successfully!");
            interstitialAd = ad;
            RegisterEventHandlers(interstitialAd);
        });
    }
    
    public void ShowInterstitialAd()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            Debug.Log("[AdMobManager] Showing interstitial ad.");
            interstitialAd.Show();
        }
        else
        {
            Debug.LogWarning("[AdMobManager] Interstitial ad is not ready yet.");
            LoadInterstitialAd();
        }
    }
    
    private void RegisterEventHandlers(InterstitialAd ad)
    {
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log($"[AdMobManager] Interstitial ad paid {adValue.Value} {adValue.CurrencyCode}.");
        };
        
        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("[AdMobManager] Interstitial ad recorded an impression.");
        };
        
        ad.OnAdClicked += () =>
        {
            Debug.Log("[AdMobManager] Interstitial ad was clicked.");
        };
        
        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[AdMobManager] Interstitial ad full screen content opened.");
        };
        
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[AdMobManager] Interstitial ad full screen content closed.");
            // Preload the next ad
            LoadInterstitialAd();
        };
        
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError($"[AdMobManager] Interstitial ad failed to open with error: {error}");
            // Preload the next ad
            LoadInterstitialAd();
        };
    }
    
    private void OnDestroy()
    {
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
        }
    }
}
