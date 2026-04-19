using UnityEngine;
using System.Collections;

public class Mall1 : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(PlayMusicWhenReady());
    }

    private IEnumerator PlayMusicWhenReady()
    {
        // Wait until AudioManager is available (it lives on a DontDestroyOnLoad object)
        while (AudioManager.Instance == null)
            yield return null;

        AudioManager.Instance.PlayMusic(MusicTrack.Mall1Background);
    }
}
