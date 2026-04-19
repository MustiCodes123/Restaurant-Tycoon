using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class MissionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI missionText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private GameObject completedIcon;
    
    [Header("Removal Animation")]
    [SerializeField] private float removeDelay = 1f;
    [SerializeField] private float removeDuration = 0.3f;
    
    private MissionData missionData;
    private bool isCompleted;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    
    public bool IsCompleted => isCompleted;
    public MissionData MissionData => missionData;
    
    /// <summary>
    /// Event fired when this mission UI is about to be removed
    /// </summary>
    public event Action<MissionUI> OnRemoved;
    
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        rectTransform = GetComponent<RectTransform>();
    }
    
    public void Setup(MissionData data)
    {
        missionData = data;
        isCompleted = false;
        
        if (completedIcon != null)
            completedIcon.SetActive(false);
        
        UpdateProgress(GetCurrentValue());
    }
    
    public void UpdateProgress(int currentValue)
    {
        if (missionData == null) return;
        
        // Update text
        if (missionText != null)
            missionText.text = missionData.GetProgressText(currentValue);
        
        // Update slider
        if (progressSlider != null)
        {
            progressSlider.maxValue = missionData.targetValue;
            progressSlider.value = Mathf.Min(currentValue, missionData.targetValue);
        }
        
        // Check completion
        if (currentValue >= missionData.targetValue && !isCompleted)
        {
            isCompleted = true;
            
            if (completedIcon != null)
                completedIcon.SetActive(true);
            
            // Remove with animation after delay
            RemoveWithAnimation();
        }
    }
    
    /// <summary>
    /// Removes this mission UI with a subtle fade and slide animation
    /// </summary>
    private void RemoveWithAnimation()
    {
        // Wait a moment to let player see the completion, then animate out
        DOVirtual.DelayedCall(removeDelay, () =>
        {
            // Create a sequence for smooth removal
            Sequence removeSequence = DOTween.Sequence();
            
            // Fade out
            if (canvasGroup != null)
            {
                removeSequence.Join(canvasGroup.DOFade(0f, removeDuration).SetEase(Ease.OutQuad));
            }
            
            // Slide up slightly
            if (rectTransform != null)
            {
                removeSequence.Join(rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + 30f, removeDuration).SetEase(Ease.OutQuad));
            }
            
            // Scale down slightly
            removeSequence.Join(transform.DOScale(0.8f, removeDuration).SetEase(Ease.OutQuad));
            
            removeSequence.OnComplete(() =>
            {
                OnRemoved?.Invoke(this);
                Destroy(gameObject);
            });
        });
    }
    
    private int GetCurrentValue()
    {
        if (missionData == null || LevelManager.Instance == null) return 0;
        
        switch (missionData.missionType)
        {
            case MissionType.ServeCustomers:
                return GetCustomersServedForMission();
            
            case MissionType.TotalEarnings:
                // Use level-based earnings (earned since level started)
                return LevelManager.Instance.GetLevelEarnings();
            
            default:
                return 0;
        }
    }
    
    private int GetCustomersServedForMission()
    {
        if (LevelManager.Instance == null) return 0;
        
        switch (missionData.serviceLocation)
        {
            case ServiceLocation.Store:
                if (!string.IsNullOrEmpty(missionData.specificStoreName))
                    return LevelManager.Instance.GetLevelCustomersServedAtStore(missionData.specificStoreName);
                // For "any store", just use total customers served
                return LevelManager.Instance.GetLevelCustomersServed();
            
            case ServiceLocation.Any:
            default:
                return LevelManager.Instance.GetLevelCustomersServed();
        }
    }
}
