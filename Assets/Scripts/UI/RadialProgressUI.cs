using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class RadialProgressUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image radialImage;
    [SerializeField] private Canvas canvas;
    
    [Header("Settings")]
    [SerializeField] private float fillDuration = 3f;
    [SerializeField] private Color fillColor = Color.green;
    [SerializeField] private Color emptyColor = Color.white;
    
    private Coroutine fillCoroutine;
    private bool isActive = false;
    
    public float FillDuration => fillDuration;
    public bool IsActive => isActive;
    
    private void Awake()
    {
        if (radialImage == null)
        {
            radialImage = GetComponentInChildren<Image>();
        }
        
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>();
        }
        
        HideProgress();
    }
    
    public void StartProgress()
    {
        if (fillCoroutine != null)
        {
            StopCoroutine(fillCoroutine);
        }
        
        ShowProgress();
        
        // Start clock tick sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayLoopingSFX(SoundEffect.ClockTick);
        }
        
        fillCoroutine = StartCoroutine(FillProgress());
    }
    
    public void StopProgress()
    {
        if (fillCoroutine != null)
        {
            StopCoroutine(fillCoroutine);
            fillCoroutine = null;
        }
        
        // Stop clock tick sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopLoopingSFX();
        }
        
        ResetProgress();
        HideProgress();
    }
    
    public void ResetProgress()
    {
        if (radialImage != null)
        {
            radialImage.fillAmount = 0f;
            radialImage.color = emptyColor;
        }
        
        isActive = false;
    }
    
    private IEnumerator FillProgress()
    {
        isActive = true;
        float elapsed = 0f;
        
        if (radialImage != null)
        {
            radialImage.fillAmount = 0f;
        }
        
        while (elapsed < fillDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fillDuration;
            
            if (radialImage != null)
            {
                radialImage.fillAmount = progress;
                radialImage.color = Color.Lerp(emptyColor, fillColor, progress);
            }
            
            yield return null;
        }
        
        // Complete
        if (radialImage != null)
        {
            radialImage.fillAmount = 1f;
            radialImage.color = fillColor;
        }
        
        OnProgressComplete();
    }
    
    private void OnProgressComplete()
    {
        // Stop clock tick sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopLoopingSFX();
        }
        
        Debug.Log("Radial progress completed!");
        isActive = false;
        // Event will be handled by PlayerController
    }
    
    private void ShowProgress()
    {
        if (canvas != null)
        {
            canvas.enabled = true;
        }
        else
        {
            gameObject.SetActive(true);
        }
    }
    
    private void HideProgress()
    {
        if (canvas != null)
        {
            canvas.enabled = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    
    public void SetFillDuration(float duration)
    {
        fillDuration = duration;
    }
}
