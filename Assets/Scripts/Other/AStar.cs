using System.Collections.Generic;
using UnityEngine;

public class AStar : MonoBehaviour
{
    public static AStar instance;
    private void Awake()
    {
        instance = this;
    }

    public List<GridNode> FindPath(Passenger currentPassenger,GridNode startNode,GridNode targetNode)
    {
        foreach(GridNode node in PassengerGrid.Instance.Nodes)
        {
            node.gCost = int.MaxValue;
            node.hCost = 0;
            node.parent = null;
        }

        startNode.gCost = 0;
        startNode.hCost = GetDistance(startNode, targetNode);

        List<GridNode> openList = new();
        HashSet<GridNode> closedList = new();

        openList.Add(startNode);

        while(openList.Count > 0)
        {
            GridNode currentNode = openList[0];

            // tim node co fcost nho nhat

            for(int i = 1; i < openList.Count; i++)
            {
                if (openList[i].fCost <  currentNode.fCost || (openList[i].fCost == currentNode.fCost && openList[i].hCost < currentNode.hCost))
                {
                    currentNode = openList[i];
                }
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            // di toi dich

            if(currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            foreach(GridNode neighbour in currentNode.GetNeighbours())
            {
                if(neighbour == null) continue;

                if(!neighbour.walkable && neighbour != targetNode) continue;

                if(closedList.Contains(neighbour)) continue;

                int congestion = 0;

                if(neighbour.occupant != null && neighbour.occupant != currentPassenger)
                {
                    congestion = 30;
                }
                // Truoc day: reservedBy la hard-block (continue), khien ca nhom bi ket
                // vi passenger dau tien reserve het hanh lang hep, nhung nguoi con lai
                // van CAN di qua chinh hanh lang do (khong co duong nao khac).
                // Gio chuyen thanh phi phat de uu tien tranh, nhung van cho phep di qua
                // (giong nguoi ta xep hang di theo sau nhau ngoai doi thuc)

                //if (neighbour.reservedBy != null && neighbour.reservedBy != currentPassenger && neighbour != targetNode)
                //{
                //    congestion += 30;
                //}    

                int newCost = currentNode.gCost + GetDistance(currentNode, neighbour) + congestion;

                if (newCost < neighbour.gCost || !openList.Contains(neighbour))
                {
                    neighbour.gCost = newCost;
                    neighbour.hCost = GetDistance(neighbour,targetNode);
                    neighbour.parent = currentNode;

                    if(!openList.Contains(neighbour))
                    {
                        openList.Add(neighbour);
                    }
                }
            }

        }

        return null;
    }

    List<GridNode> RetracePath(GridNode startNode,GridNode endNode)
    {
        List<GridNode> path = new();

        GridNode currentNode = endNode;

        while(currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }

        path.Reverse();

        return path;
    }

    public static int GetDistance(GridNode a,GridNode b)
    {
        int dx = Mathf.Abs(a.column - b.column);
        int dy = Mathf.Abs(a.row - b.row);

        if (dx > dy)
            return 14 * dy + 10 * (dx - dy);
        return 14 * dx + 10 * (dy - dx);

    }

}
