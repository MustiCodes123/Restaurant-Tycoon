using UnityEngine;

public class Mall1 : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        AudioManager.Instance.PlayMusic(MusicTrack.Mall1Background);
    }


}
