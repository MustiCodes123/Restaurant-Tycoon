using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

public class CleaningSpot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Store parentStore;
    [SerializeField] private LayerMask playerLayer;
    
    [Header("Visual")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private GameObject visualIcon;
    [SerializeField] private GameObject radialProgressParent;
    [SerializeField] private Image radialProgressImage;
    
    [Header("Pulse Animation")]
    [SerializeField] private float pulseMinScale = 0.8f;
    [SerializeField] private float pulseMaxScale = 1.2f;
    [SerializeField] private float pulseDuration = 0.5f;
    [SerializeField] private Ease pulseEase = Ease.InOutSine;
    
    [Header("Show/Hide Animation")]
    [SerializeField] private float showDuration = 0.25f;
    [SerializeField] private float hideDuration = 0.2f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;
    
    [Header("Cleaning Settings")]
    [SerializeField] private float cleaningDuration = 2f;
    [SerializeField] private Color progressColor = Color.green;
    [SerializeField] private Color emptyColor = Color.white;
    
    private bool isDirty = false;
    private bool isBeingCleaned = false;
    private bool playerInRange = false;
    private bool isCleaningActive = false;
    private float cleaningProgress = 0f;
    private Tween pulseTween;
    private Tween showHideTween;
    private Rigidbody playerRigidbody;
    private const float MOVEMENT_THRESHOLD = 0.1f; // Player velocity threshold    
    public bool IsDirty => isDirty;
    public bool IsBeingCleaned => isBeingCleaned;
    public float CleaningDuration => cleaningDuration;
    
    public event Action<CleaningSpot> OnCleaningStarted;
    public event Action<CleaningSpot> OnCleaningCompleted;
    public event Action<CleaningSpot> OnCleaningCancelled;
    public event Action<CleaningSpot> OnPlayerEntered;
    public event Action<CleaningSpot> OnPlayerExited;
    
    private void Start()
    {
        if (parentStore == null)
        {
            parentStore = GetComponentInParent<Store>();
        }
        
        HideSpot();
        // Ensure radial parent is off initially
        if (radialProgressParent != null)
            radialProgressParent.SetActive(false);
    }
    
    private void Update()
    {
        if (!playerInRange || !isDirty) return;
        
        // Check if player has stopped moving
        bool isPlayerStopped = IsPlayerStopped();
        
        // Only activate cleaning if player is stopped
        if (isPlayerStopped && !isCleaningActive)
        {
            isCleaningActive = true;
            StartCleaning();
        }
        else if (!isPlayerStopped && isCleaningActive)
        {
            // Player started moving again, pause cleaning
            isCleaningActive = false;
            CancelCleaning();
        }
        
        // Update cleaning progress if active
        if (isCleaningActive && isBeingCleaned)
        {
            UpdateCleaning(Time.deltaTime);
        }
    }
    
    private bool IsPlayerStopped()
    {
        if (playerRigidbody != null)
        {
            return playerRigidbody.linearVelocity.magnitude < MOVEMENT_THRESHOLD;
        }
        return true; // Default to true if no rigidbody
    }
    
    private void OnDestroy()
    {
        pulseTween?.Kill();
    }
    
    /// <summary>
    /// Called when the store becomes dirty (after customer served)
    /// </summary>
    public void MakeDirty()
    {
        if (isDirty) return;

        isDirty = true;
        cleaningProgress = 0f;
        ShowSpot();
        // Ensure radial UI is hidden until player starts cleaning
        if (radialProgressParent != null)
            radialProgressParent.SetActive(false);
    }
    
    /// <summary>
    /// Called when cleaning is complete
    /// </summary>
    public void MakeClean()
    {
        isDirty = false;
        isBeingCleaned = false;
        cleaningProgress = 0f;
        StopPulseAnimation();
        HideSpot();
        ResetProgress();
    }
    
    public void StartCleaning()
    {
        if (!isDirty || isBeingCleaned) return;
        
        isBeingCleaned = true;
        StopPulseAnimation();
        // Show radial UI and hide visual icon
        if (visualIcon != null)
            visualIcon.SetActive(false);
        if (radialProgressParent != null)
            radialProgressParent.SetActive(true);
        if (radialProgressImage != null)
            radialProgressImage.fillAmount = 0f;
        OnCleaningStarted?.Invoke(this);
    }
    
    public void UpdateCleaning(float deltaTime)
    {
        if (!isBeingCleaned) return;
        
        cleaningProgress += deltaTime / cleaningDuration;
        
        if (radialProgressImage != null)
        {
            radialProgressImage.fillAmount = cleaningProgress;
            radialProgressImage.color = Color.Lerp(emptyColor, progressColor, cleaningProgress);
        }
        
        if (cleaningProgress >= 1f)
        {
            CompleteCleaning();
        }
    }
    
    public void CancelCleaning()
    {
        if (!isBeingCleaned) return;
        
        isBeingCleaned = false;
        cleaningProgress = 0f;
        ResetProgress();
        // Hide radial UI and show visual icon again
        if (radialProgressParent != null)
            radialProgressParent.SetActive(false);
        if (visualIcon != null)
            visualIcon.SetActive(true);
        StartPulseAnimation();
        OnCleaningCancelled?.Invoke(this);
    }
    
    private void CompleteCleaning()
    {
        OnCleaningCompleted?.Invoke(this);
        MakeClean();
        
        if (parentStore != null)
        {
            parentStore.OnCleaningSpotCleaned(this);
        }
    }
    
    private void ShowSpot()
    {
        gameObject.SetActive(true);

        if (worldCanvas != null)
        {
            worldCanvas.enabled = true;
        }

        if (visualIcon != null)
        {
            visualIcon.SetActive(true);
            // start from zero scale and animate in
            visualIcon.transform.localScale = Vector3.zero;
            showHideTween?.Kill();
            showHideTween = visualIcon.transform
                .DOScale(Vector3.one, showDuration)
                .SetEase(showEase)
                .OnComplete(() => StartPulseAnimation());
        }

        if (radialProgressParent != null)
        {
            radialProgressParent.SetActive(false);
        }
    }

    private void HideSpot()
    {
        // Stop pulse when hiding
        StopPulseAnimation();

        // animate scale down then disable
        if (visualIcon != null)
        {
            showHideTween?.Kill();
            showHideTween = visualIcon.transform
                .DOScale(Vector3.zero, hideDuration)
                .SetEase(hideEase)
                .OnComplete(() => {
                    if (worldCanvas != null)
                        worldCanvas.enabled = false;

                    if (visualIcon != null)
                        visualIcon.SetActive(false);

                    if (radialProgressParent != null)
                        radialProgressParent.SetActive(false);

                    gameObject.SetActive(false);
                });
        }
        else
        {
            if (worldCanvas != null)
                worldCanvas.enabled = false;

            if (radialProgressParent != null)
                radialProgressParent.SetActive(false);

            gameObject.SetActive(false);
        }
    }

    private void StartPulseAnimation()
    {
        if (visualIcon == null) return;

        pulseTween?.Kill();
        Transform iconT = visualIcon.transform;
        iconT.localScale = Vector3.one * pulseMinScale;

        pulseTween = iconT
            .DOScale(pulseMaxScale, pulseDuration)
            .SetEase(pulseEase)
            .SetLoops(-1, LoopType.Yoyo);
    }
    
    private void StopPulseAnimation()
    {
        pulseTween?.Kill();
        
        if (visualIcon != null)
        {
            visualIcon.transform.localScale = Vector3.one;
        }
    }
    
    private void ResetProgress()
    {
        if (radialProgressImage != null)
        {
            radialProgressImage.fillAmount = 0f;
            radialProgressImage.color = emptyColor;
        }
        if (radialProgressParent != null)
            radialProgressParent.SetActive(false);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = true;
            playerRigidbody = other.GetComponent<Rigidbody>();
            isCleaningActive = false;
            // Don't start cleaning immediately - wait for player to stop in Update
            OnPlayerEntered?.Invoke(this);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            playerInRange = false;
            isCleaningActive = false;
            playerRigidbody = null;
            
            if (isBeingCleaned)
            {
                CancelCleaning();
            }
            
            OnPlayerExited?.Invoke(this);
        }
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = isDirty ? Color.yellow : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}
