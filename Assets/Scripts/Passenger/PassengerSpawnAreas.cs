using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class PassengerSpawnAreas : MonoBehaviour
{
    [Header("Spawn Area")]
    public Transform startPoint;

    public float width = 8f;
    public float height = 10f;

    public float spaceX = 1f;
    public float spaceZ = 1f;

    [Header("Prefab")]
    public Passenger passengerPrefab;

    [Header("Passenger Color")]
    public List<PassengerColor> colors;

    [Header("ClusterColor")]
    [SerializeField] int minClusterSize = 4;
    [SerializeField] int maxClusterSize = 8;
    Dictionary<GridNode, PassengerColor> colorMap = new();

    int columns;
    int rows;

    private void Start()
    {
        rows = Mathf.FloorToInt(height / spaceZ);
        columns = Mathf.FloorToInt(width / spaceX);

        SpawnPassengers();
    }


    void SpawnPassengers()
    {
        colorMap.Clear();

        // Grid cuc bo cho khu vuc spawn, index theo (r,c) cua khu vuc nay
        // (khac voi row/column cua PassengerGrid toan cuc)
        GridNode[,] localGrid = new GridNode[rows, columns];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 pos = startPoint.position + new Vector3(c * spaceX, 0.5f, -r * spaceZ);

                GridNode node = PassengerGrid.Instance.GetNearestNode(pos);

                // node da bi chiem -> bo qua de khong 2 passenger chong len 1 o
                if (!node.walkable) continue;

                localGrid[r, c] = node;
            }
        }


        GenerateClusters(localGrid);

        foreach (var pair in colorMap)
        {
            GridNode node = pair.Key;

            // spawn dung tam o luoi de passenger khop voi grid
            Vector3 spawnPos = node.worldPosition;
            spawnPos.y = startPoint.position.y;

            Passenger p = Instantiate(passengerPrefab, spawnPos, Quaternion.identity, startPoint);


            p.SetColor(pair.Value);

            PassengerManager.Instance.Register(p);

            node.occupant = p;
            node.walkable = false;

            p.CurrentNode = node;
        }
    }

    /// <summary>
    /// Quet qua tung o cua khu vuc spawn theo thu tu hang/cot.
    /// Voi moi o con trong, "phat trien" mot hinh chu nhat (width x height)
    /// bat dau tu o do de tao thanh 1 cluster mot mau duy nhat.
    /// </summary>
    void GenerateClusters(GridNode[,] localGrid)
    {
        bool[,] used = new bool[rows, columns];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (used[r, c]) continue;
                if (localGrid[r, c] == null) continue;

                // Chon kich thuoc muc tieu cho cluster nay, co dien tich nam trong [minClusterSize, maxClusterSize]
                int targetArea = Random.Range(minClusterSize, maxClusterSize + 1);

                int targetWidth = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(targetArea)));
                int targetHeight = Mathf.Max(1, Mathf.CeilToInt((float)targetArea / targetWidth));

                // Doi ngau nhien chieu rong/cao de cluster khong bi lap lai mot kieu hinh
                if (Random.value < 0.5f)
                {
                    (targetWidth, targetHeight) = (targetHeight, targetWidth);
                }

                Vector2Int actualSize = GetActualRectSize(localGrid, used, r, c, targetWidth, targetHeight);

                if (actualSize.x <= 0 || actualSize.y <= 0) continue;

                PassengerColor color = colors[Random.Range(0, colors.Count)];

                for (int dr = 0; dr < actualSize.y; dr++)
                {
                    for (int dc = 0; dc < actualSize.x; dc++)
                    {
                        int nr = r + dr;
                        int nc = c + dc;

                        used[nr, nc] = true;
                        colorMap[localGrid[nr, nc]] = color;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tinh kich thuoc hinh chu nhat thuc te co the tao duoc bat dau tu (r,c),
    /// gioi han boi maxWidth/maxHeight mong muon, bien khu vuc spawn va cac o da dung/khong the di chuyen.
    /// </summary>
    Vector2Int GetActualRectSize(GridNode[,] localGrid, bool[,] used, int r, int c, int maxWidth, int maxHeight)
    {
        // Tinh chieu rong toi da co the dung tu (r,c) tren cung 1 hang
        int width = 0;
        while (width < maxWidth &&
               c + width < columns &&
               localGrid[r, c + width] != null &&
               !used[r, c + width])
        {
            width++;
        }

        if (width == 0) return Vector2Int.zero;

        // Tinh chieu cao toi da: moi hang them vao phai co du "width" o hop le
        int height = 1;
        while (height < maxHeight)
        {
            int nr = r + height;

            if (nr >= rows) break;

            bool rowValid = true;

            for (int dc = 0; dc < width; dc++)
            {
                int nc = c + dc;

                if (localGrid[nr, nc] == null || used[nr, nc])
                {
                    rowValid = false;
                    break;
                }
            }

            if (!rowValid) break;

            height++;
        }

        return new Vector2Int(width, height);
    }
}