using DG.Tweening;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static LoadingSceneController;

public class WinManager : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] Button continueButton;
    [SerializeField] GameObject busDisplay;

    [SerializeField] TextMeshProUGUI totalCoinText;

    [SerializeField] RectTransform coinSpawnPoint;
    [SerializeField] RectTransform coinTargetIcon;
    [SerializeField] RectTransform coinPrefab;
    [SerializeField] RectTransform coinParent; // winpanel or canvas

    [Header("Reward Settings")]
    [SerializeField] private int baseCoinReward = 120;

    [Header("Animation")]
    [SerializeField] AnimationCurve flyCurve;
    [SerializeField] float flyDuration = 0.8f;
    [SerializeField] float waitBeforeFly = 1f;
    [SerializeField] int visualCoinCount = 10;
    [SerializeField] float spawnRadius = 80f;
    [SerializeField] float jumpHeight = 120f;

    public void OnLevelWon()
    {
        continueButton.gameObject.SetActive(true);
        busDisplay.SetActive(true);

        totalCoinText.text = GameDataManager.Instance.GetCoin().ToString();
    }

    public void OnContinueButton()
    {
        continueButton.gameObject.SetActive(false);
        busDisplay.SetActive(false);

        int coinReward = baseCoinReward;
        if (LevelManager.Instance != null && LevelManager.Instance.GetCurrentLevelConfig() != null)
        {
            coinReward = LevelManager.Instance.GetCurrentLevelConfig().coinRewards;
        }

        GameDataManager.Instance.AddCoin(coinReward);
        totalCoinText.text = GameDataManager.Instance.GetCoin().ToString();

        StartCoroutine(CoinCollectionRoutine(coinReward));
    }

    IEnumerator FlyCoin(RectTransform coin, Vector2 start, Vector2 end, int value, System.Action onFinish)
    {
        float time = 0;

        Vector2 dir = (end - start).normalized;

        Vector2 normal = new Vector2(-dir.y, dir.x);

        while (time < flyDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / flyDuration);

            Vector2 pos = Vector2.Lerp(start, end, t);

            pos.y += flyCurve.Evaluate(t) * jumpHeight;

            coin.anchoredPosition = pos;

            yield return null;
        }

        coin.anchoredPosition = end;

        Destroy(coin.gameObject);

        GameDataManager.Instance.AddCoin(value);

        totalCoinText.text = GameDataManager.Instance.GetCoin().ToString();

        onFinish?.Invoke();
    }

    IEnumerator CoinCollectionRoutine(int rewardAmount)
    {
        int coinValue = rewardAmount / visualCoinCount;
        int remainder = rewardAmount % visualCoinCount;

        List<RectTransform> coins = new List<RectTransform>();

        for (int i = 0; i < visualCoinCount; i++)
        {
            RectTransform coin = Instantiate(coinPrefab, coinSpawnPoint.position, Quaternion.identity, coinSpawnPoint.parent);

            Vector2 offset = Random.insideUnitCircle * spawnRadius;

            coin.anchoredPosition += offset;

            coins.Add(coin);

            coin.DOAnchorPos(coin.anchoredPosition + Random.insideUnitCircle * spawnRadius,0.25f)
                .SetEase(Ease.OutBack);
        }


        yield return new WaitForSeconds(waitBeforeFly);

        int finished = 0;

        for (int i = 0; i < coins.Count; i++)
        {
            RectTransform coin = coins[i];

            int value = coinValue;

            if (i == visualCoinCount - 1)
                value += remainder;


            StartCoroutine(FlyCoin(
                coin,
                coin.anchoredPosition,
                coinTargetIcon.anchoredPosition,
                value,
                () =>
                {
                    finished++;

                    if (finished == visualCoinCount)
                        OnCoinFlyingComplete();
                }));
        }
    }

    void OnCoinFlyingComplete()
    {
        // Tang level
        GameDataManager.Instance.IncreaseLevel();
        // Luu du lieu
        PlayerPrefs.Save();
        // Chuyen scene
        TargetSceneHandler.TargetScene = "GameScene";
        SceneManager.LoadScene("LoadingScene");
    }
}