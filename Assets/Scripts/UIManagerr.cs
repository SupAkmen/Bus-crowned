using UnityEngine;

public class UIManagerr : MonoBehaviour
{
    [SerializeField] GameObject settingPanel;

    public void OnOpenSetting()
    {
        settingPanel.SetActive(true);
    }

    public void OnCloseSetting()
    {
        settingPanel.SetActive(false);
    }
}
