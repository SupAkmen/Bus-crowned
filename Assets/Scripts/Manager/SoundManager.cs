using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    public AudioClip menuMusic;
    public AudioClip gameMusic;
    public AudioClip coinCollectSFX;
    public AudioClip winSFX;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UpdateMusicState()
    {
        musicSource.mute = !GameDataManager.Instance.IsMusicOn();
        UpdateSceneMusic(SceneManager.GetActiveScene().name);
    }

    public void UpdateSoundState()
    {
        sfxSource.mute = !GameDataManager.Instance.IsSoundOn();
    }

    public void UpdateSceneMusic(string sceneName)
    {
        if(sceneName == "MenuScene")
        {
            PlayMusic(menuMusic);
        }
        else if(sceneName =="GameScene")
        {
            PlayMusic(gameMusic);
        }
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.Play();    
    }

    public void PlaySFX(AudioClip clip)
    {
        if (!GameDataManager.Instance.IsSoundOn()) return;

        sfxSource.PlayOneShot(clip);
    }
}
