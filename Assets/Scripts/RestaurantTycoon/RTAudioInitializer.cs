using UnityEngine;
using System.Collections;

namespace RestaurantTycoon
{
    /// <summary>
    /// Place this on a GameObject in the restaurant scene.
    /// Handles background music (music channel) and level-complete sound (sfx channel).
    /// Controlled by SettingsManager sliders.
    /// </summary>
    public class RTAudioInitializer : MonoBehaviour
    {
        public static RTAudioInitializer Instance { get; private set; }

        [Header("Music")]
        [SerializeField] private AudioClip backgroundMusicClip;
        [SerializeField] private bool loop = true;

        [Header("Level Complete (SFX)")]
        [SerializeField] private AudioClip levelCompleteClip;

        private AudioSource musicSource;
        private AudioSource sfxSource;
        private bool musicEnabled = true;
        private bool sfxEnabled = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private IEnumerator Start()
        {
            yield return null;

            SetupAudioSources();
            ReadSettingsFromAudioManager();
            PlayBackgroundMusic();
            SubscribeToLevelComplete();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (RTLevelManager.Instance != null)
                RTLevelManager.Instance.OnLevelUp -= OnLevelUp;
        }

        private void SetupAudioSources()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = loop;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        private void ReadSettingsFromAudioManager()
        {
            if (AudioManager.Instance != null)
            {
                musicEnabled = AudioManager.Instance.IsMusicEnabled();
                sfxEnabled = AudioManager.Instance.IsSFXEnabled();
                musicSource.volume = musicEnabled ? AudioManager.Instance.GetMusicVolume() : 0f;
                sfxSource.volume = sfxEnabled ? AudioManager.Instance.GetSFXVolume() : 0f;
            }
            else
            {
                musicSource.volume = 0.5f;
                sfxSource.volume = 1f;
            }
        }

        private void PlayBackgroundMusic()
        {
            if (backgroundMusicClip == null)
            {
                Debug.LogWarning("[RTAudioInitializer] No background music clip assigned!");
                return;
            }

            musicSource.clip = backgroundMusicClip;
            if (musicEnabled)
            {
                musicSource.Play();
                Debug.Log($"[RTAudioInitializer] Background music started: '{backgroundMusicClip.name}', vol:{musicSource.volume}");
            }
        }

        private void SubscribeToLevelComplete()
        {
            if (RTLevelManager.Instance != null)
            {
                RTLevelManager.Instance.OnLevelUp += OnLevelUp;
                Debug.Log("[RTAudioInitializer] Subscribed to RTLevelManager.OnLevelUp");
            }
            else
            {
                Debug.LogWarning("[RTAudioInitializer] RTLevelManager.Instance is null — level complete sound won't play");
            }
        }

        private void OnLevelUp(int newLevel)
        {
            if (levelCompleteClip == null)
            {
                Debug.LogWarning("[RTAudioInitializer] No level complete clip assigned!");
                return;
            }

            if (!sfxEnabled) return;

            sfxSource.PlayOneShot(levelCompleteClip);
            Debug.Log($"[RTAudioInitializer] Level complete sound played for level {newLevel}");
        }

        #region Public Volume Controls (called by SettingsManager)

        public void SetMusicVolume(float volume)
        {
            if (musicSource != null)
                musicSource.volume = volume;
        }

        public void SetMusicEnabled(bool enabled)
        {
            musicEnabled = enabled;
            if (musicSource == null) return;

            if (enabled)
            {
                musicSource.volume = AudioManager.Instance != null ? AudioManager.Instance.GetMusicVolume() : 0.5f;
                if (!musicSource.isPlaying && musicSource.clip != null)
                    musicSource.Play();
            }
            else
            {
                musicSource.volume = 0f;
            }
        }

        public void SetSFXVolume(float volume)
        {
            if (sfxSource != null)
                sfxSource.volume = volume;
        }

        public void SetSFXEnabled(bool enabled)
        {
            sfxEnabled = enabled;
            if (sfxSource != null)
                sfxSource.volume = enabled ? (AudioManager.Instance != null ? AudioManager.Instance.GetSFXVolume() : 1f) : 0f;
        }

        #endregion
    }
}
