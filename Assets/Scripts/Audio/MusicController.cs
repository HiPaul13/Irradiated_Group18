using UnityEngine;

public class MusicController : MonoBehaviour
{
    public static MusicController Instance;

    private bool chasePlaying = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        AkSoundEngine.PostEvent("Play_Exploration_Music", gameObject);
    }

    public void StartChaseMusic()
    {
        if (chasePlaying) return;

        chasePlaying = true;

        AkSoundEngine.PostEvent("Stop_Exploration_Music", gameObject);
        AkSoundEngine.PostEvent("Play_Chase_Music", gameObject);
    }

    public void StopChaseMusic()
    {
        if (!chasePlaying) return;

        chasePlaying = false;

        AkSoundEngine.PostEvent("Stop_Chase_Music", gameObject);
        AkSoundEngine.PostEvent("Play_Exploration_Music", gameObject);
    }
}