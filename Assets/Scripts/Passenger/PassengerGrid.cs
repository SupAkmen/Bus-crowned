using UnityEngine;

public class PassengerGrid : MonoBehaviour
{
    public static PassengerGrid Instance;
    [SerializeField] MovementBoundary movementBoundary;

    public GridNode[,] Nodes;

    public int Rows;
    public int Columns;

    public float cellSize = 1.0f;

    Vector3 gridOrigin;

    private void Awake()
    {
        Instance = this;
        CreateGrid();
    }

    public void CreateGrid()
    {
        Bounds bounds = movementBoundary.GetBounds();

        Columns = Mathf.RoundToInt(bounds.size.x / cellSize);
        Rows = Mathf.RoundToInt(bounds.size.z / cellSize);

        Nodes = new GridNode[Rows, Columns];

        gridOrigin = new Vector3(bounds.min.x + cellSize * 0.5f, bounds.center.y, bounds.max.z - cellSize * 0.5f);

        Vector3 startPos = gridOrigin;

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                Vector3 pos = startPos + new Vector3(c * cellSize, 0, -r * cellSize);

                Nodes[r, c] = new GridNode(r, c, pos);
            }
        }

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                GridNode node = Nodes[r, c];

                if (r > 0)
                    node.Up = Nodes[r - 1, c];

                if (r < Rows - 1)
                    node.Down = Nodes[r + 1, c];

                if (c > 0)
                    node.Left = Nodes[r, c - 1];

                if (c < Columns - 1)
                    node.Right = Nodes[r, c + 1];

                // Diagonal neighbours
                if (r > 0 && c > 0)
                    node.UpLeft = Nodes[r - 1, c - 1];

                if (r > 0 && c < Columns - 1)
                    node.UpRight = Nodes[r - 1, c + 1];

                if (r < Rows - 1 && c > 0)
                    node.DownLeft = Nodes[r + 1, c - 1];

                if (r < Rows - 1 && c < Columns - 1)
                    node.DownRight = Nodes[r + 1, c + 1];
            }
        }
    }

    public GridNode GetNearestNode(Vector3 worldPos)
    {
        int column = Mathf.RoundToInt((worldPos.x - gridOrigin.x) / cellSize);
        int row = Mathf.RoundToInt((gridOrigin.z - worldPos.z) / cellSize);

        column = Mathf.Clamp(column, 0, Columns - 1);
        row = Mathf.Clamp(row, 0, Rows - 1);

        return Nodes[row, column];
    }

    //private void OnDrawGizmos()
    //{
    //    if (Nodes == null)
    //        return;

    //    foreach (GridNode node in Nodes)
    //    {
    //        Gizmos.color = node.walkable ? Color.white : Color.red;

    //        Gizmos.DrawWireCube(
    //            node.worldPosition,
    //            new Vector3(cellSize, 0.05f, cellSize)
    //        );
    //    }
    //}

}
