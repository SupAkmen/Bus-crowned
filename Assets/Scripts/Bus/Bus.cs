using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class Bus : MonoBehaviour
{

    [SerializeField] public int capacityPerBus = 30;
    [SerializeField] TextMeshPro capacityText;
    [SerializeField] float busSpeed = 5f;

    [HideInInspector] public PassengerColor passengerColor;
    public GridNode targetNode;
    int capacityInBus;
    bool hasNotifiedFull = false;

    // Reference to parentLane ( assign by BusLane or GetCompoonentInParent)
    BusLane parentLane;

    private void Start()
    {
        capacityInBus = capacityPerBus;
        UpdateCapacityText();

        parentLane =  GetComponentInParent<BusLane>();
    }

    private void OnDisable()
    {
        if (BusStation.instance != null)
        {
            BusStation.instance.UnRegisterParkingBus(this);
        }
    }

    private void Update()
    {

        MoveToGoal();

    }

    /// <summary>
    /// Return the number of available seat in bus
    /// </summary>
    /// <returns></returns>
    public int GetAvailableSeat()
    {
        return capacityInBus;
    }

    public void UpdateCapcity()
    {
        DecreasePassenger();
        UpdateCapacityText();
    }

    public void DecreasePassenger()
    {
        capacityInBus = capacityInBus - 1;
    }

    public void UpdateCapacityText()
    {
        capacityText.text = capacityInBus.ToString();
    }

    public void MoveToGoal()
    {
        if(capacityInBus <= 0)
        {
            capacityText.text = " ";

            // Notified parent lane
            if(!hasNotifiedFull && parentLane != null)
            {
                hasNotifiedFull = true;
                parentLane.OnParkingBusFull();
            }

            if(parentLane != null)
            {
                transform.position = Vector3.MoveTowards(transform.position, parentLane.goalPosition.position, busSpeed * Time.deltaTime);
            }
        }

    }
}

