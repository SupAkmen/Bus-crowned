using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BusLane : MonoBehaviour
{
    [Header("Position")]
    public Transform parkingPosition;
    public List<Transform> leavePos;
    public List<Transform> garagePositions;

    [Header("Buses")]
    public Bus parkingBus;
    public List<Bus> garageBuses;

    [Header("Settings")]
    public float moveSpeed = 5f;

    [Header("Parking")]
    [SerializeField] float unlockParkingDistance = 2.5f;
    public bool ParkingBusy { get; private set; }
    public void SetParkingBusy(bool busy) => ParkingBusy = busy;
    public float UnlockParkingDistance => unlockParkingDistance;
    public void SetParkingBus(Bus bus) => parkingBus = bus;

    [Header("Express Bus Path")]
    [Tooltip("Duong di rieng cua LANE NAY khi bus bi Booster Express rung ra ngoai. " +
         "Moi lane co vi tri vat ly khac nhau nen can duong di khac nhau, mo phong " +
         "duong mau do rieng cho lane do. Diem cuoi la noi bus 'khuat tam nhin' " +
         "truoc khi duoc dua thang vao garage.")]
    public List<Transform> expressOutPath;

    /// <summary>
    /// Return the current bus in the parking
    /// </summary>
    /// <returns></returns>
    public Bus GetCurrentParkingBus()
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

        yield return StartCoroutine(PullNextGarageBusRoutine());
    }

    public void PullNextGarageBusImmediate()
    {
        StartCoroutine(PullNextGarageBusRoutine());
    }


    IEnumerator PullNextGarageBusRoutine()
    {
        while (ParkingBusy)
        {
            yield return null;
        }

        if (garageBuses.Count > 0)
        {
            Bus nextBus = garageBuses[0];
            garageBuses.RemoveAt(0);

            yield return StartCoroutine(MoveBusToPosition(nextBus, parkingPosition.position));

            parkingBus = nextBus;

            BusStation.instance.RegisterParkingBus(nextBus);

            nextBus.targetNode = PassengerGrid.Instance.GetNearestNode(parkingPosition.position);


            // 3. Other garage bus push one position
            RefreshGaragePositions();
        }
    }

    /// <summary>
    /// Them mot bus vao CUOI hang cua garage cua lane nay( dung boi Express Bus Booster khi bus quay lai hang cho khac voiw luc spawn ban dau.
    /// </summary>
    /// <param name="bus"></param>
    public void EnqueueToGarageEnd(Bus bus)
    {
        garageBuses.Add(bus);
        RefreshGaragePositions();
    }
    /// <summary>
    /// Di chuyen lai toan bo bus trong garage ve dung vi tri slot theo thu tu hien tai dung sau khi garage thaty doi ( bus toi di hoa bus moi duoc them vao cuoi hang)
    /// </summary>
    public void RefreshGaragePositions()
    {
        for (int i = 0; i < garageBuses.Count && i < garagePositions.Count; i++)
        {
            StartCoroutine(MoveBusToPosition(garageBuses[i], garagePositions[i].position));
        }
    }

    /// <summary>
    /// Xoa hoan toan cac bus trong garage chua tung duoc dua vao parking va co mau da het passenger( xem Bus.FlushIfColorExhausted). Vi cac bus nay chua nhan duoc khach nao, huy an toan khong ma
    /// </summary>
    /// <param name="color"></param>
    public void RemoveGarageBusesOfColor(PassengerColor color)
    {
        for(int i = garageBuses.Count - 1;i >= 0; i--)
        {
            if (garageBuses[i] != null && garageBuses[i].passengerColor == color)
            {
                Bus bus = garageBuses[i];
                garageBuses.RemoveAt(i);
                Destroy(bus.gameObject);
            }
        }

        RefreshGaragePositions();
    }

    IEnumerator MoveBusToPosition(Bus bus, Vector3 targetPos)
    {
        Vector3 startPos = bus.transform.position;
        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / moveSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            bus.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        bus.transform.position = targetPos;
    }
}
