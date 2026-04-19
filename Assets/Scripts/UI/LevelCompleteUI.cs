using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class LevelCompleteUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform panelTransform;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI levelNumberText;
    
    [Header("Animation Settings")]
    [SerializeField] private float popupDuration = 0.4f;
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float closeDuration = 0.3f;
    [SerializeField] private Ease popupEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;
    
    private Tween currentTween;
    private Sequence currentSequence;
    private bool isInitialized = false;
    
    /// <summary>
    /// Call this after the GameObject is enabled to setup initial state
    /// </summary>
    public void Initialize()
    {
        if (isInitialized) return;
        isInitialized = true;
        
        // Start hidden
        if (panelTransform != null)
            panelTransform.localScale = Vector3.zero;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
    
    public void ShowLevelComplete(int levelNumber, System.Action onComplete = null)
    {
        // Kill any existing animations
        currentTween?.Kill();
        currentSequence?.Kill();
        
        // Set level number text
        if (levelNumberText != null)
            levelNumberText.text = (levelNumber+1).ToString();
        
        // Play level complete sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SoundEffect.LevelComplete);
        }
        
        // Enable raycasts during display
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        
        // Create animation sequence
        currentSequence = DOTween.Sequence();
        
        // Pop in animation
        currentSequence.Append(
            panelTransform.DOScale(Vector3.one, popupDuration)
                .SetEase(popupEase)
                .From(Vector3.zero)
        );
        
        if (canvasGroup != null)
        {
            currentSequence.Join(
                canvasGroup.DOFade(1f, popupDuration * 0.5f)
            );
        }
        
        // Wait for display duration
        currentSequence.AppendInterval(displayDuration);
        
        // Pop out animation
        currentSequence.Append(
            panelTransform.DOScale(Vector3.zero, closeDuration)
                .SetEase(closeEase)
        );
        
        if (canvasGroup != null)
        {
            currentSequence.Join(
                canvasGroup.DOFade(0f, closeDuration)
            );
        }
        
        // On complete callback
        currentSequence.OnComplete(() =>
        {
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            onComplete?.Invoke();
        });
        
        currentSequence.Play();
    }
    
    public void HideImmediately()
    {
        currentTween?.Kill();
        currentSequence?.Kill();
        
        if (panelTransform != null)
            panelTransform.localScale = Vector3.zero;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }
    
    private void OnDestroy()
    {
        currentTween?.Kill();
        currentSequence?.Kill();
    }
}
