using System;
using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance;

    [Header("Keys")]
    private const string COIN_KEY = "Player_Coin";
    private const string HEART_KEY = "Player_Heart";
    private const string HEART_UPDATE_TIME_KEY = "Heart_Update_Time";
    private const string LEVEL_KEY = "Current_Level";

    // Settings keys
    private const string MUSIC_ON_KEY = "Music_On";
    private const string SOUND_ON_KEY = "Sound_On";
    private const string VIBRATE_ON_KEY = "Vibrate_On";

    private const int MAX_HEART = 5;
    private const int HEART_RECOVER_TIME = 900; // 15 minutes = 900 giây


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        DontDestroyOnLoad(gameObject);

        InitData();
    }

    private void InitData()
    {
        if (!PlayerPrefs.HasKey(HEART_KEY))
        {
            PlayerPrefs.SetInt(HEART_KEY, MAX_HEART);
            PlayerPrefs.SetString(HEART_UPDATE_TIME_KEY, DateTime.UtcNow.ToString());
        }

        if (!PlayerPrefs.HasKey(COIN_KEY)) PlayerPrefs.SetInt(COIN_KEY, 0);
        if (!PlayerPrefs.HasKey(LEVEL_KEY)) PlayerPrefs.SetInt(LEVEL_KEY, 1);
        if (!PlayerPrefs.HasKey(MUSIC_ON_KEY)) PlayerPrefs.SetInt(MUSIC_ON_KEY, 1);
        if (!PlayerPrefs.HasKey(SOUND_ON_KEY)) PlayerPrefs.SetInt(SOUND_ON_KEY, 1);
        if (!PlayerPrefs.HasKey(VIBRATE_ON_KEY)) PlayerPrefs.SetInt(VIBRATE_ON_KEY, 1);
    }

    // Coin system
    public int GetCoin() => PlayerPrefs.GetInt(COIN_KEY,0);

    public void AddCoin(int amount)
    {
        int current = GetCoin();

        PlayerPrefs.SetInt(COIN_KEY, current + amount);
    }

    public bool SpendCoin(int amount)
    {
        int current = GetCoin();

        if(current > amount)
        {
            PlayerPrefs.SetInt(COIN_KEY,current - amount);
            return true;
        }

        return false;
    }

    // Heart System ( real time)
    public int GetHearts()
    {
        int currentHeart = PlayerPrefs.GetInt(HEART_KEY, MAX_HEART);

        if(currentHeart >= MAX_HEART)
        {
            return MAX_HEART;
        }

        DateTime lastUpdate = DateTime.Parse(PlayerPrefs.GetString(HEART_UPDATE_TIME_KEY));

        TimeSpan elapsed = DateTime.UtcNow - lastUpdate;

        int addHearts = Mathf.FloorToInt((float)elapsed.TotalSeconds / HEART_RECOVER_TIME);

        currentHeart = Mathf.Min(MAX_HEART, currentHeart + addHearts);


        // cap nhat lai thoi gian neu co tim moi duoc sinh ra

        if(addHearts > 0 && currentHeart < MAX_HEART)
        {
            TimeSpan newRemainTime = elapsed - TimeSpan.FromSeconds(addHearts * HEART_RECOVER_TIME); // lay ra phan du thoi gian sau khi da hoi lai tim

            PlayerPrefs.SetString(HEART_UPDATE_TIME_KEY, (DateTime.UtcNow - newRemainTime).ToString()); // thoi gian can hoi se giam xuong
        }
        else if(currentHeart >= MAX_HEART)
        {
            PlayerPrefs.SetString(HEART_UPDATE_TIME_KEY,DateTime.UtcNow.ToString());
        }

        PlayerPrefs.SetInt(HEART_KEY, currentHeart);
        return currentHeart;
    }

    public float GetHeartRecoveryRemainingSeconds()
    {
        int currentHeart = PlayerPrefs.GetInt(HEART_KEY, MAX_HEART);

        if (currentHeart >= MAX_HEART)
            return 0f;

        DateTime lastUpdate = DateTime.Parse(PlayerPrefs.GetString(HEART_UPDATE_TIME_KEY));
        TimeSpan elapsed = DateTime.UtcNow - lastUpdate;

        // Số tim đã được cộng thêm kể từ lần cuối lưu
        int addedHearts = Mathf.FloorToInt((float)elapsed.TotalSeconds / HEART_RECOVER_TIME);
        if (addedHearts >= MAX_HEART - currentHeart) return 0f; // Đã đầy

        // Thời gian đã trôi qua kể từ lần sinh tim gần nhất
        float remainder = (float) elapsed.TotalSeconds % HEART_RECOVER_TIME;
        float remaining = HEART_RECOVER_TIME - remainder;
        return Mathf.Max(0, remaining);
    }

    public void UseHeart()
    {
        int current = GetHearts(); // goi getheart se tu dong tinh toan thoi gian hoi

        if(current > 0)
        {
            PlayerPrefs.SetInt(HEART_KEY, current - 1);
            PlayerPrefs.SetString(HEART_UPDATE_TIME_KEY, DateTime.UtcNow.ToString());
        }
    }

    //=============Level System=================
    public int GetCurrentLevel() => PlayerPrefs.GetInt(LEVEL_KEY, 1);

    public void IncreaseLevel()
    {
        int level = GetCurrentLevel();

        PlayerPrefs.SetInt(LEVEL_KEY, level + 1);
    }

    //============Setting System==================
    public bool IsMusicOn() => PlayerPrefs.GetInt(MUSIC_ON_KEY,1) == 1;
    public bool IsSoundOn() => PlayerPrefs.GetInt(SOUND_ON_KEY,1) == 1;
    public bool IsVibrateOn() => PlayerPrefs.GetInt(VIBRATE_ON_KEY,1) == 1;

    public void SetMusic(bool isOn) { PlayerPrefs.SetInt(MUSIC_ON_KEY, isOn ? 1 : 0); }
    public void SetSound(bool isOn) { PlayerPrefs.SetInt(SOUND_ON_KEY, isOn ? 1 : 0); }
    public void SetVibrate(bool isOn) { PlayerPrefs.SetInt(VIBRATE_ON_KEY, isOn ? 1 : 0); }
}
