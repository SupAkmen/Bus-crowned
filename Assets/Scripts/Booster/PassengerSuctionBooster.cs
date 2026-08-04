using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Booster "Hut Passenger": nguoi choi nhan booster -> chon 1 trong cac bus dang dau o parking
/// -> he thong tu dong hut mot so luong passenger cung mau (uu tien nguoi dang bi chan/o sau
/// ben trong nhat) truc tiep len bus do, BO QUA hoan toan pathfinding. Day la cong cu "chua chay"
/// manh nhat trong 4 booster, dung khi nguoi choi thuc su bi ket khong tim duoc duong di.
/// </summary>
public class PassengerSuctionBooster : MonoBehaviour
{
    public static PassengerSuctionBooster Instance;

    [Header("Suction Settings")]
    [Tooltip("So luong passenger toi da se duoc hut trong 1 lan dung booster. " +
             "Thuc te se bi gioi han them boi so ghe trong con lai cua bus va so passenger cung mau hien co.")]
    [SerializeField] int suctionCount = 5;

    [SerializeField] float suctionRiseHeight = 2f;
    [SerializeField] float suctionRiseDuration = 0.3f;
    [SerializeField] float staggerDelay = 0.15f; // khoang cach giua moi lan hut, tranh dong loat qua roi mat

    [Header("Effects (Optional)")]
    [Tooltip("Hieu ung 'lo hong' hut, se duoc spawn phia tren dau passenger va phia tren bus. Co the de trong.")]
    [SerializeField] GameObject suctionHoleEffectPrefab;
    [SerializeField] float effectLifetime = 0.6f;

    bool isSelecting = false;
    Camera cam;

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    void Update()
    {
        if (!isSelecting) return;

        // Cho phep huy che do chon bang phim Escape hoac chuot phai
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelSelection();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Bus bus = hit.collider.GetComponentInParent<Bus>();

                if (bus != null && BusStation.instance.parkingBusList.Contains(bus))
                {
                    EndSelection();
                    StartCoroutine(SuctionRoutine(bus));
                }
            }
        }
    }

    /// <summary>
    /// Goi tu OnClick cua nut Booster tren UI. Bat che do cho nguoi choi chon 1 bus dang dau.
    /// </summary>
    public void ActivateBooster()
    {
        if (isSelecting) return;

        isSelecting = true;
        SetParkingBusesHighlight(true);
    }

    void CancelSelection()
    {
        EndSelection();
    }

    void EndSelection()
    {
        isSelecting = false;
        SetParkingBusesHighlight(false);
    }

    void SetParkingBusesHighlight(bool on)
    {
        foreach (var bus in BusStation.instance.parkingBusList)
        {
            if (bus != null) bus.SetHighLight(on);
        }
    }

    IEnumerator SuctionRoutine(Bus bus)
    {
        int availableSeats = bus.GetAvailableSeat();
        if (availableSeats <= 0) yield break;

        int targetCount = Mathf.Min(suctionCount, availableSeats);

        List<Passenger> priorityList = PassengerManager.Instance
            .GetPassengersOrderedBySuctionPriority(bus.passengerColor, bus.targetNode);

        int actualCount = Mathf.Min(targetCount, priorityList.Count);

        for (int i = 0; i < actualCount; i++)
        {
            Passenger p = priorityList[i];
            if (p == null) continue;

            yield return StartCoroutine(SuctionSinglePassenger(p, bus));

            if (i < actualCount - 1)
                yield return new WaitForSeconds(staggerDelay);
        }
    }

    IEnumerator SuctionSinglePassenger(Passenger p, Bus bus)
    {
        // Hieu ung "lo hong" phia tren dau passenger truoc khi hut
        GameObject holeAbovePassenger = SpawnEffect(p.transform.position + Vector3.up * 1.2f);

        yield return StartCoroutine(p.SuctionIntoBus(bus, suctionRiseHeight, suctionRiseDuration));

        if (holeAbovePassenger != null) Destroy(holeAbovePassenger, effectLifetime);

        // Hieu ung "lo hong" tha xuong phia tren bus, bao hieu passenger da len xe
        GameObject holeAboveBus = SpawnEffect(bus.transform.position + Vector3.up * 2f);
        if (holeAboveBus != null) Destroy(holeAboveBus, effectLifetime);
    }

    GameObject SpawnEffect(Vector3 position)
    {
        if (suctionHoleEffectPrefab == null) return null;
        return Instantiate(suctionHoleEffectPrefab, position, Quaternion.identity);
    }
}