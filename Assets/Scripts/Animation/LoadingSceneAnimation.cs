using UnityEngine;

public class LoadingSceneAnimation : MonoBehaviour
{
    [Header("Bus")]
    [SerializeField] Transform bus;
    [SerializeField] float jumpSpeed = 3f;
    [SerializeField] float jumpHeight = 0.2f;

    private float initialPosY;

    [Header("Road")]
    [SerializeField] Transform road1;
    [SerializeField] Transform road2;
    [SerializeField] float roadSpeed = 5f;
    [SerializeField] float leftBoudary = -10f;
    [SerializeField] float rightBoudary = 10f;

    private void Start()
    {
        if(bus != null)
        {
            initialPosY = bus.position.y;
        }
    }

    private void Update()
    {
        if(bus != null)
        {
            Vector3 pos = bus.position;

            pos.y = initialPosY + Mathf.Sign(Time.time * jumpSpeed) * jumpHeight;
            bus.position = pos;
        }

        if(road1 != null && road2 != null)
        {
            road1.position += Vector3.left * roadSpeed * Time.deltaTime;
            road2.position += Vector3.left * roadSpeed * Time.deltaTime;

            if(road1.position.x <= leftBoudary)
            {
                Vector3 resetPos = road1.position;
                resetPos.x = road2.position.x + (rightBoudary - leftBoudary);
                road1.position = resetPos;
            }

            if(road2.position.x <= leftBoudary)
            {
                Vector3 resetPos = road2.position;
                resetPos.x = road1.position.x + (rightBoudary - leftBoudary);
                road2.position = resetPos;
            }
        }
    }
}
