using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class PassengerSelector : MonoBehaviour
{
    Camera cam;

    private void Awake()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Passenger p = hit.collider.GetComponentInParent<Passenger>();

                if (p != null && p.CurrentNode != null)
                {
                    Bus nearstBus = BusStation.instance.GetNearestBusByColor(p.PassengerColor, p.transform.position);
                    
                    // ko co bus cung mau tren parking => di chuyển ra wilcardbus
                    if(nearstBus == null)
                    {
                        nearstBus = BusStation.instance.GetWildcardBusIfAcceptable(p.PassengerColor);
                    }

                    if (nearstBus == null) return;

                    if (Passenger.IsBusMoving(nearstBus))
                    {
                        return; // Chi bus nay dang ban, bus khac cung mau van dispatch duoc binh thuong
                    }

                    List<Passenger> group = p.GetConnectedPassengers();
                    // Dùng MoveGroup để tạm mở walkable cho cả nhóm trước khi tìm đường
                    // Check the number of available seat in bus same color
                    int availablesSeat = nearstBus.GetAvailableSeat();

                    // Passenger move = availableSeat 
                    int moveCount = Mathf.Min(group.Count, availablesSeat);
                    List<Passenger> moveGroup = group.GetRange(0, moveCount);

                    // Gan san targetBus cho ca nhom trc khi dispatch dam bao ca nhom ve cung 1 bus, ko bi tach giua chung
                    foreach(var member in moveGroup)
                    {
                        member.targetBus = nearstBus;
                    }

                    StartCoroutine(Passenger.MoveGroup(moveGroup));

                }
            }
        }
    }
}

