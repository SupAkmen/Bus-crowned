using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LoadingSceneController;

public class UIManagerr : MonoBehaviour
{
    [Header("Game Scene UI")]
    [SerializeField] GameObject settingPanel;

    [Header("Menu Scene UI")]
    [SerializeField] TextMeshProUGUI coinTextUI;
    [SerializeField] TextMeshProUGUI heartTextUI;       
    [SerializeField] TextMeshProUGUI heartTimerTextUI;  // Thoi gian hoi tim : 15:00
    [SerializeField] TextMeshProUGUI levelPlayButtonText;
    [SerializeField] GameObject heartFullText;  // "FULL"

    private Coroutine heartTimerCoroutine;

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "MenuScene")
        {
            UpdateMenuUI();
            StartHeartTimerCoroutine();
        }
    }

    public void UpdateMenuUI()
    {
        int coin = GameDataManager.Instance.GetCoin();
        int heart = GameDataManager.Instance.GetHearts();

        coinTextUI.text = coin.ToString();

        int currentLevel = GameDataManager.Instance.GetCurrentLevel();
        levelPlayButtonText.text = $"LEVEL {currentLevel}";

        bool isFull = heart >= 5;

        // An so tim neu day, hien FULL. Hien tim neu chua day, an full
        heartTextUI.gameObject.SetActive(!isFull);
        heartFullText.SetActive(isFull);
        heartTextUI.text = heart.ToString();

        UpdateHeartTimer();
    }

    private void UpdateHeartTimer()
    {
        int heart = GameDataManager.Instance.GetHearts(); 

        if (heart >= 5)
        {
            heartTimerTextUI.text = "FULL";
        }
        else
        {
            float remaining = GameDataManager.Instance.GetHeartRecoveryRemainingSeconds();
            if (remaining <= 0)
            {
                heartTimerTextUI.text = "00:00";
            }
            else
            {
                int minutes = Mathf.FloorToInt(remaining / 60f);
                int seconds = Mathf.FloorToInt(remaining % 60f);
                heartTimerTextUI.text = $"{minutes:00}:{seconds:00}";
            }
        }
    }

    public void OnPlayButton()
    {
        TargetSceneHandler.TargetScene = "GameScene";
        SceneManager.LoadScene("LoadingScene");
    }

    public void OnOpenSetting()
    {
        settingPanel.SetActive(true);
    }

    public void OnCloseSetting()
    {
        settingPanel.SetActive(false);
    }

    void StartHeartTimerCoroutine()
    {
        if (heartTimerCoroutine != null) StopCoroutine(heartTimerCoroutine);
        heartTimerCoroutine = StartCoroutine(HeartTimerRoutine());
    }

    IEnumerator HeartTimerRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            UpdateMenuUI(); // Cap nhat tim va timer moi giay
        }
    }
}