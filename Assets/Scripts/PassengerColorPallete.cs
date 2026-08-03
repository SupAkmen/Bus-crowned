using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Noi luu tru danh sach tat ca PassengerColor dung chung cho ca game.
/// Thay vi phai keo-tha tung mau vao rieng le o moi component (PassengerSpawnAreas,
/// PixelImageConverter...), gio chi can tao 1 asset Palette nay va tham chieu no
/// o moi noi. Them/bot mau chi can sua 1 cho duy nhat.
/// </summary>
[CreateAssetMenu(fileName = "PassengerColorPalette", menuName = "Scriptable Objects/PassengerColorPalette")]
public class PassengerColorPalette : ScriptableObject
{
    public List<PassengerColor> colors = new();
}