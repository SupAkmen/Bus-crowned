using System.Collections.Generic;
using UnityEngine;

public class GridNode
{
    public int row;
    public int column;

    public Vector3 worldPosition;

    public bool walkable = true;

    public Passenger occupant;

    public GridNode parent;
    public GridNode Up;
    public GridNode Down;
    public GridNode Left;
    public GridNode Right;

    // Diagonal neighbours ( 8-direction pathfiding)
    public GridNode UpLeft;
    public GridNode UpRight;
    public GridNode DownLeft;
    public GridNode DownRight;

    public int gCost;
    public int hCost;

    public int fCost => gCost + hCost;

    public GridNode(int row, int column, Vector3 worldPosition)
    {
        this.row = row;
        this.column = column;
        this.worldPosition = worldPosition;
    }

    public IEnumerable<GridNode> GetNeighbours()
    {
        if (Up != null)
            yield return Up;
        if (Down != null)
            yield return Down;
        if (Left != null)
            yield return Left;
        if (Right != null)
            yield return Right;

        if (UpLeft != null && (Up == null || Up.walkable) && (Left == null || Left.walkable))
        {
            yield return UpLeft;
        }

        if (UpRight != null && (Up == null || Up.walkable) && (Right == null || Right.walkable))
        {
            yield return UpRight;
        }

        if (DownLeft != null && (Down == null || Down.walkable) && (Left == null || Left.walkable))
        {
            yield return DownLeft;
        }

        if (DownRight != null && (Right == null || Right.walkable) && (Down == null || Down.walkable))
        {
            yield return DownRight;
        }
    }
}
