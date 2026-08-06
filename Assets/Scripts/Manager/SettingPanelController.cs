using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static LoadingSceneController;

public class SettingsPanelController : MonoBehaviour
{
    [Header("Setting Toggles")]
    public Toggle musicToggle;
    public Toggle soundToggle;
    public Toggle vibrateToggle;

    private void OnEnable()
    {
   
        musicToggle.isOn = GameDataManager.Instance.IsMusicOn();
        soundToggle.isOn = GameDataManager.Instance.IsSoundOn();
        vibrateToggle.isOn = GameDataManager.Instance.IsVibrateOn();

        musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
        soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);
        vibrateToggle.onValueChanged.AddListener(OnVibrateToggleChanged);
    }

    private void OnDisable()
    {

        musicToggle.onValueChanged.RemoveListener(OnMusicToggleChanged);
        soundToggle.onValueChanged.RemoveListener(OnSoundToggleChanged);
        vibrateToggle.onValueChanged.RemoveListener(OnVibrateToggleChanged);
    }

    private void OnMusicToggleChanged(bool isOn)
    {
        GameDataManager.Instance.SetMusic(isOn);
        SoundManager.Instance.UpdateMusicState();
    }

    private void OnSoundToggleChanged(bool isOn)
    {
        GameDataManager.Instance.SetSound(isOn);
        SoundManager.Instance.UpdateSoundState();
    }

    private void OnVibrateToggleChanged(bool isOn)
    {
        GameDataManager.Instance.SetVibrate(isOn);
        // Ở đây bạn có thể tắt/bật thư viện Vibrate nếu bạn dùng
    }

    public void OnResetLevelButton()
    {
        GameDataManager.Instance.UseHeart();
        TargetSceneHandler.TargetScene = "GameScene";
        SceneManager.LoadScene("LoadingScene");
    }

    public void OnExitButton()
    {
        GameDataManager.Instance.UseHeart();
        TargetSceneHandler.TargetScene = "MenuScene";
        SceneManager.LoadScene("LoadingScene");
    }
}