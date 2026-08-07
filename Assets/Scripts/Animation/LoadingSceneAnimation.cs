using UnityEngine;

public class LoadingSceneAnimation : MonoBehaviour
{
    [Header("Bus")]
    [SerializeField] Transform bus;
    [SerializeField] float jumpSpeed = 3f;
    [SerializeField] float jumpHeight = 0.2f;

    private float initialPosY;
    [Header("Road")]
    [SerializeField] RectTransform road1;
    [SerializeField] RectTransform road2;
    [SerializeField] float roadSpeed = 5f;
    float roadWidth;

    private void Start()
    {
        if (bus != null)
        {
            initialPosY = bus.position.y;
        }

        roadWidth = road1.rect.width;

        if (road1 != null && road2 != null)
        {
            road2.anchoredPosition = new Vector2(road1.anchoredPosition.x + roadWidth, road1.anchoredPosition.y);
        }
    }

    private void Update()
    {
        if (bus != null)
        {
            Vector3 pos = bus.position;
            pos.y = initialPosY + Mathf.Sin(Time.time * jumpSpeed) * jumpHeight;
            bus.position = pos;
        }

        if (road1 != null && road2 != null)
        {
            road1.anchoredPosition += Vector2.left * roadSpeed * Time.deltaTime;
            road2.anchoredPosition += Vector2.left * roadSpeed * Time.deltaTime;

            if (road1.anchoredPosition.x <= -roadWidth)
            {
                road1.anchoredPosition = new Vector2(road2.anchoredPosition.x + roadWidth, road1.anchoredPosition.y);
            }

            if (road2.anchoredPosition.x <= -roadWidth)
            {
                road2.anchoredPosition = new Vector2(road1.anchoredPosition.x + roadWidth, road2.anchoredPosition.y);
            }

        }

    }

}

