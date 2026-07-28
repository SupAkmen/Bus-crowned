using UnityEngine;

public class MovementBoundary : MonoBehaviour
{
    public static MovementBoundary Instance;
    [SerializeField] BoxCollider boxCollider;

    private void Awake()
    {
        Instance = this;
    }

    public Vector3 Clamp(Vector3 pos)
    {
        Bounds b = boxCollider.bounds;

        pos.x = Mathf.Clamp(pos.x, b.min.x, b.max.x);

        pos.z = Mathf.Clamp(pos.z, b.min.z, b.max.z);

        return pos;
    }

    public Bounds GetBounds()
    {
        return boxCollider.bounds;
    }
}
