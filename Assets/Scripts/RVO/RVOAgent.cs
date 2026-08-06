using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class RVOAgent : MonoBehaviour
{
    [Header("Movement")]
    public float radius = 0.5f;
    public float maxSpeed = 3.0f;
    public float neighborDistance = 2f;

    [HideInInspector] public Vector3 velocity;          // van toc thuc te
    [HideInInspector] public Vector3 preferredVelocity; // van toc mong muon
    [HideInInspector] public List<RVOAgent> neighbors = new();

    [Header("Avoidance")]
    public float seperationWeight = 2f;
    public float timeHorizon = 1f; // du doan trc 1 giay

    [Header("Push Force")]
    public float pushDistance = 1.2f;
    public float pushStrength = 2.5f;

    bool isRegistered;
    private void Awake()
    {
        ClampRadiusToGrid();
    }
    /// <summary>
    /// Bao ve chong lai truong hop radius bi dat qua lon so voi kich thuoc o luoi
    /// (vi du radius = 0.75 nhung cellSize = 1 -> 2 agent dung o 2 o ke nhau da bi
    /// tinh nham la dang de len nhau, gay day/xo day lien tuc du dang dung yen).
    /// Radius toi da duoc gioi han o mot ti le an toan cua cellSize.
    /// </summary>
    void ClampRadiusToGrid()
    {
        if (PassengerGrid.Instance == null) return;
        float cell = PassengerGrid.Instance.cellSize;
        float maxSafeRadius = cell * 0.4f;
        if (radius > maxSafeRadius)
        {
            radius = maxSafeRadius;
        }
    }

    public void Register()
    {
        if ((isRegistered))
        {
            return;
        }

        isRegistered = true;

        RVOSimulator.Instance.Register(this);
    }

    public void UnRegister()
    {
        if (!isRegistered) 
            return;

        isRegistered = false;

        velocity = Vector3.zero;
        preferredVelocity = Vector3.zero;
        neighbors.Clear();
        RVOSimulator.Instance.Unregister(this);
    }

    private void OnDestroy()
    {
        UnRegister();
    }
}
