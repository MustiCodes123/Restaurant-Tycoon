using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// UI component for displaying dynamic missions (unlock/upgrade tasks).
/// Unlike regular missions, these show as complete/incomplete without progress bars.
/// </summary>
public class DynamicMissionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI missionText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private GameObject completedIcon;
    
    [Header("Animation")]
    [SerializeField] private float completionPunchScale = 0.1f;
    [SerializeField] private float completionPunchDuration = 0.3f;
    
    private DynamicMission missionData;
    private bool isCompleted;
    
    public bool IsCompleted => isCompleted;
    public DynamicMission MissionData => missionData;
    public string MissionId => missionData?.missionId;
    
    private void FindReferences()
    {
        // Try to find references if not set (in case component was added at runtime)
        if (missionText == null)
        {
            // Try to find any TextMeshProUGUI in children
            missionText = GetComponentInChildren<TextMeshProUGUI>();
        }
        
        if (progressSlider == null)
        {
            progressSlider = GetComponentInChildren<Slider>();
        }
        
        if (completedIcon == null)
        {
            // Try to find a GameObject named "Completed" or similar
            Transform completedTransform = transform.Find("CompletedIcon");
            if (completedTransform == null)
                completedTransform = transform.Find("Completed");
            if (completedTransform == null)
                completedTransform = transform.Find("CheckIcon");
            if (completedTransform != null)
                completedIcon = completedTransform.gameObject;
        }
    }
    
    public void Setup(DynamicMission data)
    {
        FindReferences();
        
        missionData = data;
        isCompleted = data.isCompleted;
        
        if (missionText != null)
            missionText.text = data.displayText;
        
        // Set slider to 0 (not started) or full (completed)
        if (progressSlider != null)
        {
            progressSlider.maxValue = 1;
            progressSlider.value = isCompleted ? 1 : 0;
        }
        
        if (completedIcon != null)
            completedIcon.SetActive(isCompleted);
    }
    
    public void MarkCompleted()
    {
        if (isCompleted) return;
        
        isCompleted = true;
        
        // Update slider to full
        if (progressSlider != null)
        {
            progressSlider.DOValue(1f, 0.3f).SetEase(Ease.OutQuad);
        }
        
        // Show completed icon with animation
        if (completedIcon != null)
        {
            completedIcon.SetActive(true);
            completedIcon.transform.localScale = Vector3.zero;
            completedIcon.transform.DOScale(Vector3.one, completionPunchDuration).SetEase(Ease.OutBack);
        }
        
        // Punch scale on the whole item
        transform.DOPunchScale(Vector3.one * completionPunchScale, completionPunchDuration, 5, 0.5f);
    }
    
    private void OnDestroy()
    {
        DOTween.Kill(transform);
        if (progressSlider != null)
            DOTween.Kill(progressSlider);
        if (completedIcon != null)
            DOTween.Kill(completedIcon.transform);
    }
}
