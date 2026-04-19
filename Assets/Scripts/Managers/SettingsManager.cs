using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("Audio Sliders")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    
    [Header("Toggle Buttons (Optional)")]
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle sfxToggle;
    
    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button closeSettingsButton;
    
    private void Start()
    {
        InitializeSliders();
        InitializeButtons();
    }
    
    private void InitializeButtons()
    {
        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.AddListener(OpenSettings);
        }
        
        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.AddListener(CloseSettings);
        }
    }
    
    private void InitializeSliders()
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("AudioManager instance not found!");
            return;
        }
        
        // Initialize music slider
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.value = AudioManager.Instance.GetMusicVolume();
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
        
        // Initialize SFX slider
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.value = AudioManager.Instance.GetSFXVolume();
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
        
        // Initialize music toggle
        if (musicToggle != null)
        {
            musicToggle.isOn = AudioManager.Instance.IsMusicEnabled();
            musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
        }
        
        // Initialize SFX toggle
        if (sfxToggle != null)
        {
            sfxToggle.isOn = AudioManager.Instance.IsSFXEnabled();
            sfxToggle.onValueChanged.AddListener(OnSFXToggleChanged);
        }
    }
    
    private void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);

        if (RestaurantTycoon.RTAudioInitializer.Instance != null)
            RestaurantTycoon.RTAudioInitializer.Instance.SetMusicVolume(value);
    }
    
    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
            AudioManager.Instance.PlaySFX(SoundEffect.ButtonClick);
        }

        if (RestaurantTycoon.RTAudioInitializer.Instance != null)
            RestaurantTycoon.RTAudioInitializer.Instance.SetSFXVolume(value);
    }
    
    private void OnMusicToggleChanged(bool enabled)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicEnabled(enabled);

        if (RestaurantTycoon.RTAudioInitializer.Instance != null)
            RestaurantTycoon.RTAudioInitializer.Instance.SetMusicEnabled(enabled);
    }
    
    private void OnSFXToggleChanged(bool enabled)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXEnabled(enabled);
            if (enabled)
                AudioManager.Instance.PlaySFX(SoundEffect.ButtonClick);
        }

        if (RestaurantTycoon.RTAudioInitializer.Instance != null)
            RestaurantTycoon.RTAudioInitializer.Instance.SetSFXEnabled(enabled);
    }
    
    private void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            Time.timeScale = 0f; // Pause the game
        }
    }
    
    private void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            Time.timeScale = 1f; // Resume the game
        }
    }
    
    private void OnDestroy()
    {
        // Clean up listeners
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
        
        if (musicToggle != null)
            musicToggle.onValueChanged.RemoveListener(OnMusicToggleChanged);
        
        if (sfxToggle != null)
            sfxToggle.onValueChanged.RemoveListener(OnSFXToggleChanged);
        
        if (openSettingsButton != null)
            openSettingsButton.onClick.RemoveListener(OpenSettings);
        
        if (closeSettingsButton != null)
            closeSettingsButton.onClick.RemoveListener(CloseSettings);
    }
}
