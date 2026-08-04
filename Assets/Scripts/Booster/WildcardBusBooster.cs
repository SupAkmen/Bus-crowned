using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawn 1 bus da nang tai spawnPoint (vi tri mau vang khoanh tron trong anh), di chuyen
/// toi wildcardParkingPoint (vi tri parking rieng, ben canh 3 lane thuong). Bus nay nhan
/// duoc moi mau KHONG trung voi 3 bus binh thuong dang dau, capacity thap hon binh thuong.
/// </summary>
public class WildcardBusBooster : MonoBehaviour
{
    [Header("Wildcard Bus Setup")]
    [SerializeField] Bus wildcardBusPrefab;
    [SerializeField] Transform spawnPoint;
    [SerializeField] Transform wildcardParkingPoint;
    [SerializeField] int wildcardCapacity = 15;
    [SerializeField] float moveSpeed = 5f;

    [Header("Wildcard Leave Path")]
    [Tooltip("Duong di khi Wildcard Bus day khach va roi ban co, vi no khong thuoc BusLane nao.")]
    [SerializeField] List<Transform> wildcardLeaveOutPath;

    public void ActivateBooster()
    {
        if (BusStation.instance.wildcardBus != null)
        {
            Debug.Log("[WildcardBusBooster] Da co mot Wildcard Bus dang hoat dong.");
            return;
        }

        StartCoroutine(SpawnAndMoveRoutine());

        Debug.Log("WildcardBus Booster");
    }

    IEnumerator SpawnAndMoveRoutine()
    {
        Bus bus = Instantiate(wildcardBusPrefab, spawnPoint.position, Quaternion.Euler(0, 180, 0));

        bus.SetAsWildcard(wildcardCapacity);
        bus.SetWildcardLeavepath(wildcardLeaveOutPath);
        BusStation.instance.RegisterWildcardBus(bus);

        yield return StartCoroutine(bus.MoveToPosition(wildcardParkingPoint.position, moveSpeed));

        bus.transform.SetLocalPositionAndRotation(wildcardParkingPoint.position, wildcardParkingPoint.rotation);

        bus.targetNode = PassengerGrid.Instance.GetNearestNode(wildcardParkingPoint.position);
    }
}
