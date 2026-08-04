using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;

public class Bus : MonoBehaviour
{

    [SerializeField] public int capacityPerBus = 30;
    [SerializeField] TextMeshPro capacityText;
    [SerializeField] float busSpeed = 5f;
    [SerializeField] MeshRenderer bodyRenderer;

    [HideInInspector] public PassengerColor passengerColor;
    [HideInInspector] public bool isWildcard = false;

    [HideInInspector] public List<Transform> wildcardLeavePath;

    [Header("Highlight")]
    [Tooltip("GameObject hieu ung glow/outline, la con cua Bus prefab, mac dinh tat. " +
         "Bat len khi mot Booster dang cho nguoi choi chon bus nay.")]
    [SerializeField] GameObject highlightIndicator;

    public GridNode targetNode;
    int capacityInBus;
    bool capacityInitialized = false; // so luong khach thuc te co the chua
    bool hasNotifiedFull = false;
    bool isLeaving = false;

    // Reference to parentLane ( assign by BusLane or GetCompoonentInParent)
    BusLane parentLane;

    public BusLane ParentLane => parentLane;

    private void Start()
    {
        if (!capacityInitialized)
        {
            capacityInBus = capacityPerBus;
        }
        UpdateCapacityText();

        parentLane = GetComponentInParent<BusLane>();
    }
    public void SetCapacity(int amount)
    {
        capacityInBus = Mathf.Clamp(amount, 0, capacityPerBus);
        capacityInitialized = true;
        UpdateCapacityText();
    }


    public void SetHighLight(bool isOn)
    {
        if (highlightIndicator != null)
        {
            highlightIndicator.SetActive(isOn);
        }
    }

    public void SetWildcardLeavepath(List<Transform> path)
    {
        wildcardLeavePath = path;
    }


    private void OnDisable()
    {
        if (BusStation.instance != null)
        {
            BusStation.instance.UnRegisterParkingBus(this);
            BusStation.instance.UnRegisterWildcardBus(this);
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

        if (bodyRenderer != null && color != null && color.material != null)
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
            TriggerFull();
        }
    }

    /// <summary>
    /// Kich hoat khi bus da day khach, thong bao cho parent lane va bat dau di theo duong ra.
    /// Dung chung boi MoveToGoal() va ForceLeaveToGoal() (Booster Express Bus)
    /// </summary>

    void TriggerFull()
    {
        if(isWildcard)
        {
            if(!isLeaving)
            {
                isLeaving = true;
                StartCoroutine(WildcardLeaveRoutine());
            }
            return;
        }

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

    IEnumerator WildcardLeaveRoutine()
    {
        yield return new WaitForSeconds(0.3f);

        BusStation.instance.UnRegisterParkingBus(this);
        BusStation.instance.UnRegisterWildcardBus(this); // giai phong vi tri cho phep wildcard moi vao

        if(wildcardLeavePath != null)
        {
            foreach(Transform point in wildcardLeavePath)
            {
                yield return StartCoroutine(MoveToPosition(point.position, busSpeed));
            }
        }

        Destroy(gameObject);
    }


    /// <summary>
    /// Dung cho booster express bus : ep bus roi khoi parking ngay lap tuc du chua day khach, tai su dung chuoi FollowLeavePath + OnParkingBusFull() co san
    /// </summary>
    public void  ForceToLeaveEarly()
    {
        if (isLeaving) return;

        capacityInBus = 0;
        capacityText.text = " ";
        TriggerFull();
    }

    /// <summary>
    /// danh dau bus nay la wildcard : ko co mau co dinh, capacity co the thay doi,
    /// </summary>
    /// <returns></returns>

    public void SetAsWildcard(int capacity)
    {
        isWildcard = true;
        passengerColor = null;
        capacityPerBus = capacity;
        SetCapacity(capacity);
    }

    IEnumerator FollowLeavePath()
    {
        yield return new WaitForSeconds(0.3f);

        if (parentLane == null)
            yield break;

        parentLane.SetParkingBusy(true);

        int index = 0;
        bool parkingUnlocked = false;

        foreach (Transform point in parentLane.leavePos)
        {
            while (Vector3.Distance(transform.position, point.position) > 0.05f)
            {
                Vector3 direction = point.position - transform.position;
                direction.y = 0f;

                // quay dau xe

                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);

                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 100f * Time.deltaTime);
                }

                transform.position = Vector3.MoveTowards(transform.position, point.position, busSpeed * Time.deltaTime);

                if (!parkingUnlocked)
                {
                    float distance = Vector3.Distance(transform.position, parentLane.parkingPosition.position);

                    if (distance >= parentLane.UnlockParkingDistance)
                    {
                        parkingUnlocked = true;
                        parentLane.SetParkingBusy(false);
                    }
                }

                yield return null;
            }
        }

        if (index == 0)
        {
            parentLane.SetParkingBusy(false);
        }

        index++;

        Destroy(gameObject);

    }

    public IEnumerator ExpressLeaveAndRejoinGaragfe(List<Transform> outPath,BusLane targetLane,float speed)
    {
        isLeaving = true;

        foreach(Transform point in outPath)
        {
            yield return StartCoroutine(MoveToPosition(point.position, speed));
        }

        isLeaving = false;

        // sau khi ra ngoai, xep vao cuoi hang garage dc chi dinh
        targetLane.EnqueueToGarageEnd(this);
    }

    /// <summary>
    /// Di chuyen thang toi 1 vi tri, dung boi WildcardBusBooster de dua bus tu diem spawn
    /// toi vi tri parking rieng (khac voi FollowLeavePath von dung cho luc bus ROI di).
    /// </summary>
    /// <param name="targetPos"></param>
    /// <param name="speed"></param>
    /// <returns></returns>
    public IEnumerator MoveToPosition(Vector3 targetPos, float speed)
    {
        while (Vector3.Distance(transform.position, targetPos) > 0.05f)
        {
            Vector3 direction = targetPos - transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);

                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 100f * Time.deltaTime);
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPos;
    }
}

