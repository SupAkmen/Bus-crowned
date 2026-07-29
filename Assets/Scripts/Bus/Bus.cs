using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class Bus : MonoBehaviour
{

    [SerializeField] public int capacityPerBus = 30;
    [SerializeField] TextMeshPro capacityText;
    [SerializeField] float busSpeed = 5f;
    [SerializeField] MeshRenderer bodyRenderer;
    [HideInInspector] public PassengerColor passengerColor;
    public GridNode targetNode;
    int capacityInBus;
    bool capacityInitialized = false; // so luong khach thuc te co the chua
    bool hasNotifiedFull = false;
    bool isLeaving = false;

    // Reference to parentLane ( assign by BusLane or GetCompoonentInParent)
    BusLane parentLane;

    private void Start()
    {
        if(!capacityInitialized)
        {
            capacityInBus = capacityPerBus;
        }   
        UpdateCapacityText();

        parentLane =  GetComponentInParent<BusLane>();
    }
    public void SetCapacity(int amount)
    {
        capacityInBus = Mathf.Clamp(amount,0,capacityPerBus);
        capacityInitialized = true;
        UpdateCapacityText ();
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

    public void SetColor(PassengerColor color)
    {
        passengerColor = color;

        if(bodyRenderer != null && color != null && color.material != null)
        {
            bodyRenderer.material = color.material;
        }
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
        if (capacityInBus <= 0)
        {
            capacityText.text = " ";

            // Notified parent lane
            if (!hasNotifiedFull && parentLane != null)
            {
                hasNotifiedFull = true;
                parentLane.OnParkingBusFull();
            }

            if (!isLeaving)
            {
                isLeaving = true;
                StartCoroutine(FollowLeavePath());
            }
        }
    }

    IEnumerator FollowLeavePath()
    {
        yield return new WaitForSeconds(0.5f);

        if (parentLane == null) 
            yield break;

        parentLane.SetParkingBusy(true);

        int index = 0;
        bool parkingUnlocked = false;

        foreach(Transform point in parentLane.leavePos)
        {
            while(Vector3.Distance(transform.position, point.position) > 0.05f)
            {
                Vector3 direction = point.position - transform.position;
                direction.y = 0f;

                // quay dau xe

                if(direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);

                    transform.rotation = Quaternion.RotateTowards(transform.rotation,targetRotation,100f * Time.deltaTime);
                }

                transform.position = Vector3.MoveTowards(transform.position,point.position,busSpeed * Time.deltaTime);

                if(!parkingUnlocked)
                {
                    float distance = Vector3.Distance(transform.position, parentLane.parkingPosition.position);

                    if(distance >= parentLane.UnlockParkingDistance)
                    {
                        parkingUnlocked = true;
                        parentLane.SetParkingBusy(false);
                    }
                }

                yield return null;
            }
        }

        if(index == 0)
        {
            parentLane.SetParkingBusy(false);
        }

        index++;

    }
}

