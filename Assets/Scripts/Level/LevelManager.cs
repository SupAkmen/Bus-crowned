using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("Level Configs")]
    [SerializeField] private List<LevelConfig> allLevels;

    private LevelConfig currentLevelConfig;

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
    }

    private void Start()
    {
        LoadCurrentLevel();
    }

    public void LoadCurrentLevel()
    {
        int currentLevelNum = GameDataManager.Instance.GetCurrentLevel();

        if (currentLevelNum - 1 < allLevels.Count)
        {
            currentLevelConfig = allLevels[currentLevelNum - 1];
            Debug.Log($"Loaded Level {currentLevelNum}: {currentLevelConfig.name}");
        }
        else
        {
            Debug.LogError($"Level {currentLevelNum} chưa được cấu hình trong LevelManager! (Vượt quá danh sách)");
            // Nếu muốn vòng lặp lại từ đầu:
            // currentLevelConfig = allLevels[0];
        }
    }

    public LevelConfig GetCurrentLevelConfig()
    {
        if (currentLevelConfig == null) LoadCurrentLevel();
        return currentLevelConfig;
    }
}
