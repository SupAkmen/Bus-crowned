using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeedBoostBooster : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] float speedMultiplier = 2f;
    [SerializeField] float durationMinutes = 10f;

    [Header("UI")]
    [SerializeField] Button boosterButton;
    [SerializeField] TextMeshProUGUI countdownText;
    [SerializeField] string idleText = "";

    [Header("Images")]
    [SerializeField] Image boosterImage;
    [SerializeField] Sprite inActiveSprite;
    [SerializeField] Sprite activeSprite;

    private const string RemainingTimeKey = "SpeedBoost_RemainingTime";

    private static float defaultTimeScale = 1f;

    private float remainingTime;
    private bool isActive;
    private Coroutine runningCoroutine;

    private void Awake()
    {
        remainingTime = PlayerPrefs.GetFloat(
            RemainingTimeKey,
            durationMinutes * 60f);

        boosterImage.sprite = inActiveSprite;

        if (remainingTime < durationMinutes * 60f)
        {
            UpdateCountdownText(remainingTime);
        }
        else
        {
            countdownText.text = idleText;
        }
    }

    public void ActivateBooster()
    {
        if (isActive)
        {
            StopBooster();
        }
        else
        {
            StartBooster();
        }
    }

    void StartBooster()
    {
        if (runningCoroutine != null)
            StopCoroutine(runningCoroutine);

        runningCoroutine = StartCoroutine(BoostRoutine());
    }

    IEnumerator BoostRoutine()
    {
        isActive = true;

        boosterImage.sprite = activeSprite;

        Time.timeScale = speedMultiplier;

        float saveTimer = 0f;

        while (remainingTime > 0)
        {
            UpdateCountdownText(remainingTime);

            yield return null;

            remainingTime -= Time.unscaledDeltaTime;

            if (remainingTime < 0)
                remainingTime = 0;

            saveTimer += Time.unscaledDeltaTime;

            if (saveTimer >= 1f)
            {
                SaveData();
                saveTimer = 0f;
            }
        }

        // Booster dùng hết
        remainingTime = durationMinutes * 60f;

        SaveData();

        StopBooster(true);
    }

    void StopBooster(bool finished = false)
    {
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }

        Time.timeScale = defaultTimeScale;

        isActive = false;

        boosterImage.sprite = inActiveSprite;

        if (finished)
        {
            countdownText.text = idleText;
        }
        else
        {
            UpdateCountdownText(remainingTime);
        }

        SaveData();
    }

    void UpdateCountdownText(float secondsRemaining)
    {
        if (countdownText == null) return;

        int totalSeconds = Mathf.CeilToInt(secondsRemaining);

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        countdownText.text = $"{minutes:00}:{seconds:00}";
    }

    void SaveData()
    {
        PlayerPrefs.SetFloat(RemainingTimeKey, remainingTime);
        PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            SaveData();
    }

    private void OnApplicationQuit()
    {
        SaveData();
    }

    private void OnDisable()
    {
        if (isActive)
        {
            Time.timeScale = defaultTimeScale;
        }
    }
}