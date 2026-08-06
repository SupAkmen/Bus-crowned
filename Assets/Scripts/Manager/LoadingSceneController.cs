using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingSceneController : MonoBehaviour
{
    [SerializeField] private float minLoadTime = 2f;

    private void Start()
    {
        StartCoroutine(LoadSceneRoutine());
    }

    IEnumerator LoadSceneRoutine()
    {
        // Loading scene chay time
        yield return new WaitForSeconds(minLoadTime);

        // Check state : Game -> Menu or Menu -> Game
        // Use a static string to save purpose loading scene
        string targetScene = TargetSceneHandler.TargetScene;
        AsyncOperation operation = SceneManager.LoadSceneAsync(targetScene);
    }

    public static class TargetSceneHandler
    {
        public static string TargetScene = "MenuScene";
    }
}
