using System.Collections;
using UnityEngine;

public class PopUpAnimation: MonoBehaviour
{
    [Header("References")]
    [SerializeField] RectTransform panel;
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Animation")]
    [SerializeField] float duration = 0.3f;
    [SerializeField] float startOffset = -40f;

    [Header("Curves")]
    [SerializeField] AnimationCurve scaleCurve = 
        new AnimationCurve(
            new Keyframe(0f,0f),
            new Keyframe(0.75f,1.08f),
            new Keyframe(1f,1f)
            );
    [SerializeField] AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField] AnimationCurve alphaCurve = AnimationCurve.Linear(0, 0, 1, 1);

    Vector2 targetPos;
    Coroutine currentRoutine;

    private void Awake()
    {
        if(panel == null)
            panel = GetComponent<RectTransform>();

        targetPos = panel.anchoredPosition;
    }

    private void OnEnable()
    {
        PlayOpen();
    }

    public void PlayOpen()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        float time = 0;

        panel.localScale = Vector3.zero;

        panel.anchoredPosition = targetPos + Vector2.up * startOffset;

        if(canvasGroup != null)
        {
            canvasGroup.alpha = 0;
        }

        while(time < duration)
        {
            time += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(time/duration);

            float scale = scaleCurve.Evaluate(t);
            float move = moveCurve.Evaluate(t);

            panel.localScale = Vector3.one * scale;
            panel.anchoredPosition = Vector2.Lerp(targetPos + Vector2.up * startOffset,targetPos,move);

            if(canvasGroup != null) 
                canvasGroup.alpha = alphaCurve.Evaluate(t);

            yield return null;
        }

        panel.localScale = Vector3.one;
        panel.anchoredPosition = targetPos;

        if (canvasGroup != null)
            canvasGroup.alpha = 1;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
