using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfig", menuName = "Scriptable Objects/LevelConfig")]
public class LevelConfig : ScriptableObject
{
    public int levelNumber;
    public Texture2D layoutImage;
    public int coinRewards = 40;
}
