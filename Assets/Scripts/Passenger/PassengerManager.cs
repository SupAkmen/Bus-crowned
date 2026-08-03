using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
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

    public int GetCountByColor(PassengerColor color)
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

    public List<PassengerColor> GetColorsOrderedByOuterness()
    {
        List<PassengerColor> colors = GetAllColorsPresent();

        if(colors.Count == 0) return colors;

        float sumRows = 0f;
        float sumCols = 0f;

        int total = 0;

        foreach(var p in passengers)
        {
            if(p.CurrentNode == null) continue;

            sumRows += p.CurrentNode.row;
            sumCols += p.CurrentNode.column;
            total++;
        }

        if(total == 0) return colors;

        float centerRow = sumRows / total;
        float centerCol = sumCols / total;

        Dictionary<PassengerColor, float> sumDist = new();
        Dictionary<PassengerColor, int> countByColor = new();

        foreach(var p in passengers)
        {
            if (p.CurrentNode == null) continue;

            float dr = p.CurrentNode.row - centerRow;
            float dc = p.CurrentNode.column - centerCol;
            float dist = Mathf.Sqrt(dr * dr + dc * dc);

            sumDist.TryGetValue(p.PassengerColor, out float s);
            sumDist[p.PassengerColor] = s + dist;

            countByColor.TryGetValue(p.PassengerColor,out int c);
            countByColor[p.PassengerColor] = c + 1;
        }

        // Mau xa tam nhat ( vien ngoai nhat) -> uu tien truoc
        //Tie-brek : neu do xa ga bang nhau , mau dong hon se duoc uu tien hon

        return colors
            .OrderByDescending(c => countByColor.TryGetValue(c,out int cnt) && cnt > 0 ? sumDist[c] / cnt : 0f)
            .ThenByDescending(c => countByColor.TryGetValue(c,out int cnt) ? cnt : 0)
            .ToList();
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
