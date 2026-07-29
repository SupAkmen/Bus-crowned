using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class PassengerManager : MonoBehaviour
{
    public static PassengerManager Instance;

    [HideInInspector] List<Passenger> passengers = new List<Passenger>();

    private void Awake()
    {
        Instance = this;
    }

    public void Register(Passenger p)
    {
        if(!passengers.Contains(p))
                passengers.Add(p);
    }

    public void Unregister(Passenger p)
    {
        passengers.Remove(p);
    }

    public int getCountByColor(PassengerColor color)
    {
        int count = 0;

        foreach(var p in passengers)
        {
            if(p.PassengerColor == color) count++;
        }

        return count;
    }

    public List<PassengerColor> GetAllColorsPresent()
    {
        List<PassengerColor> colors = new List<PassengerColor>();

        foreach(var p in passengers)
        {
            if(!colors.Contains(p.PassengerColor))
            {
                colors.Add(p.PassengerColor);
            }
        }

        return colors;
    }

    // Tim ra cac mau cung mau de mau rieng le gop nhom vao
    public GridNode GetEmptyNodeNearSameColor(Passenger source)
    {
        float minDistane = float.MaxValue;
        GridNode bestNode = null;

        foreach(Passenger p in passengers)
        {
            if (p == source) continue;

            if (p.PassengerColor != source.PassengerColor) continue;

            if(p.CurrentNode == null) continue;

            foreach(GridNode neighbour in p.CurrentNode.GetNeighbours())
            {
                if (neighbour != null && neighbour.walkable)
                {
                    float distance = Vector3.Distance(source.transform.position,neighbour.worldPosition);

                    if(distance < minDistane)
                    {
                        minDistane = distance;
                        bestNode = neighbour;
                    }
                }
            }
        }

        return bestNode;
    }
}
