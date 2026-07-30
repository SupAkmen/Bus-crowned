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
                    if(Passenger.IsColorMoving(p.PassengerColor))
                    {
                        return;
                    }

                    List<Passenger> group = p.GetConnectedPassengers();
                    // Dùng MoveGroup để tạm mở walkable cho cả nhóm trước khi tìm đường
                    // Check the number of available seat in bus same color
                    int availablesSeat = BusStation.instance.GetAvailableSeatByColor(p.PassengerColor);

                    if (availablesSeat >= 0)
                    {
                        // Passenger move = availableSeat 
                        int moveCount = Mathf.Min(group.Count, availablesSeat);
                        List<Passenger> moveGroup = group.GetRange(0, moveCount);

                        Passenger.MoveGroup(moveGroup);
                    }
                }
            }
        }
    }
}

