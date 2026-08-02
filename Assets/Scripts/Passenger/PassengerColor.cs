using UnityEngine;

[CreateAssetMenu(fileName = "PassengerColor", menuName = "Scriptable Objects/PassengerColor")]
public class PassengerColor : ScriptableObject
{
    public string colorName;

    public Material material;

    [Header("Image Mapping")]
    public Color pixelColor;

}
