using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BusLane : MonoBehaviour
{
    [Header("LaneColor")]
    public PassengerColor laneColor;

    [Header("Position")]
    public Transform parkingPosition;
    public Transform turnPosition;
    public Transform goalPosition;

    [Header("Buses")]
    public Bus parkingBus;
    public List<Bus> garageBuses;

    [Header("Settings")]
    public float moveSpeed = 5f;

    private List<Vector3> garageSlotPosition = new List<Vector3>();

    private void Start()
    {
        // Save the initialize positon 
        foreach(Bus bus in garageBuses)
        {
            garageSlotPosition.Add(bus.transform.position);
        }
    }

    /// <summary>
    /// Return the current bus in the parking
    /// </summary>
    /// <returns></returns>
    public Bus GetCurrentBus()
    {
        return parkingBus;
    }

    /// <summary>
    /// Return the empty seat in current parking bus
    /// </summary>
    /// <returns></returns>
    public int GetAvailableSeats()
    {
        if (parkingBus == null) return 0;

        return parkingBus.GetAvailableSeat();
    }    

    /// <summary>
    /// Call when the parking bus is full
    /// 1. Parking bus move to the goal
    /// 2. Garage bus [0] move to parking seat
    /// 3. Other garage bus push one position
    /// </summary>
    public void OnParkingBusFull()
    {
        StartCoroutine(HandleBusFullSequence());
    }

    IEnumerator HandleBusFullSequence()
    {
        Bus fullBus = parkingBus;

        //Remove fullBus to passenger don't findpath to bus is running to goal.
        BusStation.instance.UnRegisterParkingBus(fullBus);
        parkingBus = null;

        // Parking bus move to the goal 


        // Garage bus -> parking bus 
        yield return new WaitForSeconds(0.3f);

        if(garageBuses.Count > 0)
        {
            Bus nextBus  = garageBuses[0];
            garageBuses.RemoveAt(0);

            yield return StartCoroutine(MoveBusToPosition(nextBus, parkingPosition.position + new Vector3(0,0.63f,0)));

            parkingBus = nextBus;

            BusStation.instance.RegisterParkingBus(nextBus);

            nextBus.targetNode = PassengerGrid.Instance.GetNearestNode(parkingPosition.position);


            // 3. Other garage bus push one position
            for (int i  = 0; i < garageBuses.Count && i < garageSlotPosition.Count; i++)
            {
                StartCoroutine(MoveBusToPosition(garageBuses[i], garageSlotPosition[i]));
            }
        }
    }

    IEnumerator MoveBusToPosition(Bus bus, Vector3 targetPos)
    {
        Vector3 startPos = bus.transform.position;
        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / moveSpeed;
        float elapsed = 0f;

        while(elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed/duration);
            bus.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        bus.transform.position = targetPos;
    }
}
