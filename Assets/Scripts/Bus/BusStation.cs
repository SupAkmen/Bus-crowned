
using System.Collections.Generic;
using UnityEngine;

public class BusStation : MonoBehaviour
{
    public static BusStation instance;

    public BusLane[] busLanes;

    // The list of car in parking 
    [HideInInspector] public List<Bus> parkingBusList = new List<Bus>();

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        foreach (var lane in busLanes)
        {
            if (lane.parkingBus != null)
            {
                RegisterParkingBus(lane.parkingBus);
            }
        }
    }
    public void RegisterParkingBus(Bus bus)
    {
        if (!parkingBusList.Contains(bus))
        {
            parkingBusList.Add(bus);
            bus.targetNode = PassengerGrid.Instance.GetNearestNode(bus.transform.position);
        }
    }

    public void UnRegisterParkingBus(Bus bus)
    {
        parkingBusList.Remove(bus);
    }

    /// <summary>
    /// Find the lane have same color with parking bus and have empty seat
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    public BusLane GetLaneByColor(PassengerColor color)
    {
        foreach (var lane in busLanes)
        {
            if (lane.laneColor == color && lane.GetCurrentBus() != null)
            {
                return lane;
            }
        }

        return null;
    }
    /// <summary>
    /// Get the number of available spaces in the parking lane of the corresponding color.
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>

    public int GetAvailableSeatByColor(PassengerColor color)
    {
        BusLane lane = GetLaneByColor(color);

        if (lane == null) return 0;

        return lane.GetAvailableSeats();
    }
}
