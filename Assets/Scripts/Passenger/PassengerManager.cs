using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PassengerManager : MonoBehaviour
{
    public static PassengerManager Instance;

    [HideInInspector] public List<Passenger> passengers = new List<Passenger>();

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

    /// <summary>
    /// Tim nhom passenger nen di chueyn tiep theo, dung boi HintBooster. Duyet mau theo thu tu outness(mau vien ngoai truoc)
    /// voi moi mau liem tra co bus(thuong hoac wildcard) con cho ko, roi lay nhom lien ket dau tien tim duoc cua mau do.
    /// </summary>
    /// <returns></returns>
    public List<Passenger> FindBestHintGroup()
    {
        List<PassengerColor> orderedColors = GetColorsOrderedByOuterness();

        foreach (var color in orderedColors)
        {
            bool hasNormalSeat = BusStation.instance.GetAvailableSeatByColor(color) > 0;
            bool hasWildcardSeat = BusStation.instance.CanWildcardAccept(color);

            if (!hasNormalSeat && !hasWildcardSeat) continue;

            GridNode targetNode = hasNormalSeat
                ? BusStation.instance.GetBusByColor(color).targetNode
                : BusStation.instance.wildcardBus.targetNode;

            if (targetNode == null) continue;

            // FIX: truoc day chi lay dai dien DAU TIEN tim thay (FirstOrDefault) lam seed,
            // khong kiem tra xem nguoi do CO DUONG DI THUC SU toi bus hay khong. Gio duyet
            // qua tung passenger cua mau nay, chi chon lam seed nguoi CHAC CHAN co duong
            // (AStar.FindPath != null), tranh goi y nham nguoi dang bi vay kin ben trong.
            Passenger seed = null;

            foreach (var p in passengers.Where(x => x.PassengerColor == color))
            {
                if (!p.IsAvailableForHint) continue;

                var testPath = AStar.instance.FindPath(p, p.CurrentNode, targetNode);

                if (testPath != null)
                {
                    seed = p;
                    break;
                }
            }

            if (seed == null) continue; // Ca mau nay hien khong ai co duong di -> thu mau tiep theo

            List<Passenger> group = seed.GetConnectedPassengers()
                .Where(p => p.IsAvailableForHint)
                .ToList();

            if (group.Count > 0)
            {
                return group;
            }
        }

        return null;
    }

    /// <summary>
    /// Tra ve danh sach passenger cung mau, sap xep theo do uu tien de hut vao bus qua Booster.
    /// Uu tien tuyet doi: passenger KHONG tim duoc duong toi targetNode (chac chan dang bi chan).
    /// Sau do: cang gan tam (center) cua toan bo dam dong cang uu tien, vi cang o "sau ben trong"
    /// thi cang de bi cac lop ben ngoai bao vay/chan duong trong tuong lai.
    /// </summary>
    public List<Passenger> GetPassengersOrderedBySuctionPriority(PassengerColor color, GridNode targetNode)
    {
        List<Passenger> candidates = passengers
            .Where(p => p.PassengerColor == color && p.CurrentNode != null)
            .ToList();

        if (candidates.Count == 0 || targetNode == null) return candidates;

        List<Passenger> blocked = new();
        List<Passenger> reachable = new();

        foreach (var p in candidates)
        {
            var path = AStar.instance.FindPath(p, p.CurrentNode, targetNode);

            if (path == null) blocked.Add(p);
            else reachable.Add(p);
        }

        ComputeCenter(out float centerRow, out float centerCol);

        // cang gan tam cua anh => khoang cach cang nho thi cang duoc uu tien lay
        blocked = blocked.OrderBy(p => DistanceFromCenter(p, centerRow, centerCol)).ToList();
        reachable = reachable.OrderBy(p => DistanceFromCenter(p, centerRow, centerCol)).ToList();

        return blocked.Concat(reachable).ToList();
    }

    void ComputeCenter(out float centerRow, out float centerCol)
    {
        float sumRow = 0f, sumCol = 0f;
        int total = 0;

        foreach (var p in passengers)
        {
            if (p.CurrentNode == null) continue;
            sumRow += p.CurrentNode.row;
            sumCol += p.CurrentNode.column;
            total++;
        }

        centerRow = total > 0 ? sumRow / total : 0f;
        centerCol = total > 0 ? sumCol / total : 0f;
    }

    float DistanceFromCenter(Passenger p, float centerRow, float centerCol)
    {
        float dr = p.CurrentNode.row - centerRow;
        float dc = p.CurrentNode.column - centerCol;
        return Mathf.Sqrt(dr * dr + dc * dc);
    }
}
