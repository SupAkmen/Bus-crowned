using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
/// <summary>
/// Chon1 trong 3 bus dang dau -> Bus do roi som(di theo leavPos co san cua BusLane) -> bus garage ke tiep len thay ngay lap tuc
/// </summary>
public class ExpressBusBooster : MonoBehaviour
{
    [SerializeField] float expressSpeed = 8f;

    Camera cam;
    bool isSelecting = false;

    void Awake() => cam = Camera.main;

    private void Update()
    {
        if (!isSelecting) return;

        if(Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Space))
        {
            EndSelection();
            return;
        }

        if(Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Bus bus = hit.collider.GetComponentInParent<Bus>();

                if (bus != null && BusStation.instance.parkingBusList.Contains(bus))
                {
                    EndSelection();
                    SendBusExpress(bus);
                }
            }
        }
    }

    public void ActivateBooster()
    {
        if (isSelecting) return;

        isSelecting = true;
        SetHighLight(true);
    }

    void EndSelection()
    {
        isSelecting = false;
        SetHighLight(false);  
    }

    void SetHighLight(bool on)
    {
         foreach(var bus in BusStation.instance.parkingBusList)
        {
            if (bus != null)
            {
                bus.SetHighLight(on);
            }
        }
    }

    void SendBusExpress(Bus bus)
    {
        BusLane lane = bus.ParentLane;

        if(lane == null) return;

        BusStation.instance.UnRegisterParkingBus(bus);
        lane.SetParkingBus(null);

        StartCoroutine(bus.ExpressLeaveAndRejoinGaragfe(lane.expressOutPath,lane,expressSpeed));

        lane.PullNextGarageBusImmediate();
    }
}
