using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Win UI")]
    [SerializeField] GameObject winPanel;
    [SerializeField] WinManager winManager;

    private bool isGameFinished = false;

    void Start()
    {
        winPanel.SetActive(false);
        InvokeRepeating("CheckWinCondition", 1f, 1f);
    }

    void CheckWinCondition()
    {
        if (isGameFinished) return;

        // Ko con passengers nao
        bool noPassengers = PassengerManager.Instance.passengers.Count == 0;

       // Tat ca xe deu da den Goal ( vi tri cuoi cung sau khi xe roi parking)
        bool noBuses = BusStation.instance.allBuses.Count == 0;

        if (noPassengers && noBuses)
        {
            TriggerWin();
        }
    }
    void TriggerWin()
    {
        isGameFinished = true;
        CancelInvoke("CheckWinCondition");

        winPanel.SetActive(true);
        winManager.OnLevelWon();

        SoundManager.Instance.PlaySFX(SoundManager.Instance.winSFX);
    }
}