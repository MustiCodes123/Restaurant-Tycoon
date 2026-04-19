using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SoundEffect
{
    MoneyFlow,
    ButtonClick,
    LevelComplete,
    CustomerServed,
    ClockTick,
    FoodPreparing,
    ItemPickup,
    GarbageDrop
}

public enum MusicTrack
{
    Mall1Background,
    None
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioSource musicAudioSource;

    [Header("Settings")]
    [SerializeField] private float defaultSFXVolume = 1f;
    [SerializeField] private float defaultMusicVolume = 0.5f;
    [SerializeField] private float musicFadeDuration = 0.5f;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip moneyFlowClip;
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip levelCompleteClip;
    [SerializeField] private AudioClip customerServedClip;
    [SerializeField] private AudioClip clockTickClip;
    [SerializeField] private AudioClip foodPreparingClip;
    [SerializeField] private AudioClip itemPickupClip;
    [SerializeField] private AudioClip garbageDropClip;

    [Header("Music Tracks")]
    [SerializeField] private AudioClip mall1BackgroundClip;



    // PlayerPrefs Keys
    private const string SFX_ENABLED_KEY = "SFXEnabled";
    private const string MUSIC_ENABLED_KEY = "MusicEnabled";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";

    private bool sfxEnabled = true;
    private bool musicEnabled = true;
    private float savedSFXVolume;
    private float savedMusicVolume;
    private MusicTrack currentMusicTrack = MusicTrack.None;
    private int currentWorldMusicIndex = -1;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
            
            // Ensure music loops
            if (musicAudioSource != null)
            {
                musicAudioSource.loop = true;
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void LoadSettings()
    {
        sfxEnabled = PlayerPrefs.GetInt(SFX_ENABLED_KEY, 1) == 1;
        musicEnabled = PlayerPrefs.GetInt(MUSIC_ENABLED_KEY, 1) == 1;
        
        savedSFXVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, defaultSFXVolume);
        savedMusicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, defaultMusicVolume);
        
        if (sfxAudioSource != null)
            sfxAudioSource.volume = sfxEnabled ? savedSFXVolume : 0f;
        
        if (musicAudioSource != null)
            musicAudioSource.volume = musicEnabled ? savedMusicVolume : 0f;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt(SFX_ENABLED_KEY, sfxEnabled ? 1 : 0);
        PlayerPrefs.SetInt(MUSIC_ENABLED_KEY, musicEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, savedSFXVolume);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, savedMusicVolume);
        PlayerPrefs.Save();
    }

    #region Sound Effects

    public void PlaySFX(SoundEffect sound)
    {
        if (!sfxEnabled || sfxAudioSource == null) return;

        AudioClip clip = GetSFXClip(sound);
        if (clip != null)
        {
            sfxAudioSource.PlayOneShot(clip);
        }
    }

    public void PlaySFXWithPitch(SoundEffect sound, float pitch)
    {
        if (!sfxEnabled || sfxAudioSource == null) return;

        AudioClip clip = GetSFXClip(sound);
        if (clip != null)
        {
            // Store original pitch
            float originalPitch = sfxAudioSource.pitch;
            sfxAudioSource.pitch = pitch;
            sfxAudioSource.PlayOneShot(clip);
            // Reset pitch after a frame (coroutine not needed for OneShot)
            StartCoroutine(ResetPitchAfterDelay(originalPitch, clip.length / pitch));
        }
    }

    public void PlayLoopingSFX(SoundEffect sound)
    {
        if (!sfxEnabled || sfxAudioSource == null) return;

        AudioClip clip = GetSFXClip(sound);
        if (clip != null)
        {
            sfxAudioSource.clip = clip;
            sfxAudioSource.loop = true;
            sfxAudioSource.Play();
        }
    }

    public void StopLoopingSFX()
    {
        if (sfxAudioSource != null)
        {
            sfxAudioSource.Stop();
            sfxAudioSource.loop = false;
            sfxAudioSource.clip = null;
        }
    }

    private IEnumerator ResetPitchAfterDelay(float originalPitch, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (sfxAudioSource != null)
            sfxAudioSource.pitch = originalPitch;
    }

    private AudioClip GetSFXClip(SoundEffect sound)
    {
        return sound switch
        {
            SoundEffect.MoneyFlow => moneyFlowClip,
            SoundEffect.ButtonClick => buttonClickClip,
            SoundEffect.LevelComplete => levelCompleteClip,
            SoundEffect.CustomerServed => customerServedClip,
            SoundEffect.ClockTick => clockTickClip,
            SoundEffect.FoodPreparing => foodPreparingClip,
            SoundEffect.ItemPickup => itemPickupClip,
            SoundEffect.GarbageDrop => garbageDropClip,
            _ => null
        };
    }

    public void SetSFXEnabled(bool enabled)
    {
        sfxEnabled = enabled;
        if (sfxAudioSource != null)
        {
            sfxAudioSource.volume = enabled ? savedSFXVolume : 0f;
        }
        SaveSettings();
    }

    public void SetSFXVolume(float volume)
    {
        savedSFXVolume = volume;
        if (sfxAudioSource != null && sfxEnabled)
        {
            sfxAudioSource.volume = volume;
        }
        SaveSettings();
    }

    public bool IsSFXEnabled() => sfxEnabled;
    public float GetSFXVolume() => savedSFXVolume;

    #endregion

    #region Music

    public void PlayMusic(MusicTrack track, bool fade = true)
    {
        if (track == currentMusicTrack && currentWorldMusicIndex == -1) return;
        
        currentMusicTrack = track;
        currentWorldMusicIndex = -1;
        
        if (musicAudioSource == null) return;

        AudioClip clip = GetMusicClip(track);
        
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        if (fade && musicAudioSource.isPlaying)
        {
            fadeCoroutine = StartCoroutine(FadeToNewTrack(clip));
        }
        else
        {
            musicAudioSource.clip = clip;
            if (clip != null && musicEnabled)
            {
                musicAudioSource.Play();
            }
            else
            {
                musicAudioSource.Stop();
            }
        }
    }

    private IEnumerator FadeToNewTrack(AudioClip newClip)
    {
        float targetVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, defaultMusicVolume);
        
        // Fade out
        if (musicAudioSource.isPlaying)
        {
            float startVolume = musicAudioSource.volume;
            float elapsed = 0f;
            
            while (elapsed < musicFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeDuration);
                yield return null;
            }
        }
        
        // Switch track
        musicAudioSource.clip = newClip;
        
        if (newClip != null && musicEnabled)
        {
            musicAudioSource.Play();
            
            // Fade in
            float elapsed = 0f;
            while (elapsed < musicFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicAudioSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / musicFadeDuration);
                yield return null;
            }
            musicAudioSource.volume = targetVolume;
        }
        else
        {
            musicAudioSource.Stop();
        }
        
        fadeCoroutine = null;
    }

    private AudioClip GetMusicClip(MusicTrack track)
    {
        return track switch
        {
            MusicTrack.Mall1Background => mall1BackgroundClip,
            MusicTrack.None => null,
            _ => null
        };
    }

    public void StopMusic(bool fade = true)
    {
        currentMusicTrack = MusicTrack.None;
        currentWorldMusicIndex = -1;
        
        if (musicAudioSource == null) return;
        
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (fade && musicAudioSource.isPlaying)
        {
            fadeCoroutine = StartCoroutine(FadeToNewTrack(null));
        }
        else
        {
            musicAudioSource.Stop();
            musicAudioSource.clip = null;
        }
    }
    
    /// <summary>
    /// Immediately stops all music and resets state. Use this when transitioning levels.
    /// </summary>
    public void ForceStopMusic()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        
        currentMusicTrack = MusicTrack.None;
        currentWorldMusicIndex = -1;
        
        if (musicAudioSource != null)
        {
            musicAudioSource.Stop();
            musicAudioSource.clip = null;
            musicAudioSource.volume = musicEnabled ? savedMusicVolume : 0f;
        }
    }

    public void PauseMusic()
    {
        if (musicAudioSource != null)
        {
            musicAudioSource.Pause();
        }
    }

    public void ResumeMusic()
    {
        if (musicAudioSource != null && musicEnabled)
        {
            musicAudioSource.UnPause();
        }
    }

    public void SetMusicEnabled(bool enabled)
    {
        musicEnabled = enabled;
        if (musicAudioSource != null)
        {
            if (enabled)
            {
                musicAudioSource.volume = savedMusicVolume;
                if (!musicAudioSource.isPlaying && musicAudioSource.clip != null)
                {
                    musicAudioSource.Play();
                }
            }
            else
            {
                musicAudioSource.volume = 0f;
            }
        }
        SaveSettings();
    }

    public void SetMusicVolume(float volume)
    {
        savedMusicVolume = volume;
        if (musicAudioSource != null && musicEnabled)
        {
            musicAudioSource.volume = volume;
        }
        SaveSettings();
    }

    public bool IsMusicEnabled() => musicEnabled;
    public float GetMusicVolume() => savedMusicVolume;
    public MusicTrack GetCurrentMusicTrack() => currentMusicTrack;
    public int GetCurrentWorldMusicIndex() => currentWorldMusicIndex;

    #endregion

    #region Utility

    public void SetMasterMute(bool muted)
    {
        SetSFXEnabled(!muted);
        SetMusicEnabled(!muted);
    }

    #endregion
}