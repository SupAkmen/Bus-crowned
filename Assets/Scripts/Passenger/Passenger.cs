using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Passenger : MonoBehaviour
{
    PassengerColor passengerColor;
    [SerializeField] MeshRenderer bodyRenderer;
    public PassengerColor PassengerColor => passengerColor;
    public GridNode CurrentNode;

    List<GridNode> path;
    int pathIndex;

    RVOAgent agent;

    bool isEntering;
    // đang trong quá trình di chuyển theo path
    bool isMoving;   
    // Ktra xem passenger co dang duoc dispatch ve bus hay ko (dung de khoa theo mau tranh tinh trang dispatch 1 nhom khac cung mau trong khi nhom nay chua len het xe)
    bool isDispatchedToBus;
    // Dem so passenger cua tung mau dang duoc dispatch di ve bus ( chua len xe xong)
    static readonly Dictionary<PassengerColor, int> movingCountByColor = new();


    const int reservationWindow = 1; // so o phia trc dc giu ali tinh tu pathindecx hien tai
    readonly HashSet<GridNode> reservedWindow = new HashSet<GridNode>();
    GridNode finalTarget;  // dich cuoi cung (bus targetNode hoac target cua MoveToTargetNode), dung de re-path

    private void Start()
    {
        agent = GetComponent<RVOAgent>();
        isMoving = false;
        agent.UnRegister();

        float cell = PassengerGrid.Instance != null ? PassengerGrid.Instance.cellSize : 1f;

        StartCoroutine(AutoGroupRoutine());
    }

    private void Update()
    {
        if (isEntering) return;

        if (path == null || pathIndex >= path.Count)
        {
            if (isMoving)
            {
                // Đã đi hết path hoặc path null -> dừng lại
                isMoving = false;
                agent.preferredVelocity = Vector3.zero;
                agent.velocity = Vector3.zero;

                if(isDispatchedToBus)
                {
                    isDispatchedToBus = false;
                }

                // Căn chỉnh vị trí chính xác vào giữa ô lưới
                if (CurrentNode != null)
                {
                    Vector3 exactPos = CurrentNode.worldPosition;
                    exactPos.y = transform.position.y; // Giữ nguyên độ cao Y
                    transform.position = exactPos;
                }
            }

            return;
        }

        if ((IsWindowBlocked()))
        {
            RequestPathTo(finalTarget);

            if (path == null || pathIndex >= path.Count) return;
        }

        Vector3 targetPos = path[pathIndex].worldPosition;
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;   // game top-down: chi di chuyen tren mat phang XZ

        float speed = Random.Range(agent.maxSpeed - 1.5f,agent.maxSpeed);
        agent.preferredVelocity = dir.normalized * speed;

        if (dir.magnitude < 0.3f)
        {
            CurrentNode.walkable = true;
            CurrentNode.occupant = null;

            CurrentNode = path[pathIndex];

            CurrentNode.walkable = false;
            CurrentNode.occupant = this;

            pathIndex++;

            ReserveWindow();
        }
    }

    /// <summary>
    /// Kiem tra xem mau nay hien co dang co passenger nao dang duoc disptach ve bus hay khong, passengerselector se dung ham nay de chan click chon them
    /// cung mau khi dang co nhom di chuyen tranh tinh trang dispatch chong len nhau lam ngheo bus
    /// </summary>
    /// <param name="passengerColor"></param>
    /// <returns></returns>
    public static bool IsColorMoving(PassengerColor passengerColor)
    {
        return passengerColor != null && movingCountByColor.TryGetValue(passengerColor, out var count) && count > 0;
    }

    static void IncrementMoving(PassengerColor passengerColor)
    {
        if (passengerColor == null) return;

        movingCountByColor.TryGetValue(passengerColor, out int count);
        movingCountByColor[passengerColor] = count + 1;
    }

    static void DecrementMoving(PassengerColor passengerColor)
    {
        if(passengerColor == null) return;

        if(movingCountByColor.TryGetValue(passengerColor,out int count))
        {
            movingCountByColor[passengerColor] = Mathf.Max(0,count-1);
        }
    }

    public void SetColor(PassengerColor color)
    {
        passengerColor = color;

        bodyRenderer.material = color.material;
    }

    /// <summary>
    /// Giu truoc reservationWindow o ke tiep tren path ( tu pathIndex hien tai) giai phong cac o da giu trc do khong con nam trong cua so
    /// </summary>
    void ReserveWindow()
    {
        HashSet<GridNode> desired = new HashSet<GridNode>();

        if (path != null)
        {
            int end = Mathf.Min(pathIndex + reservationWindow, path.Count - 1);

            for (int i = pathIndex;  i <= end; i++)
            {
                desired.Add(path[i]);
            }
        }

        // Giai phong nhung o ko con nam trong cua so moi
        foreach(GridNode node in reservedWindow)
        {
            if(!desired.Contains(node) && node.reservedBy == this)
            {
                node.reservedBy = null;
            }
        }

        // Chiem cac o trong cua so con trong hoac dang la cua chinh minh
        foreach(GridNode node in desired)
        {
            if(node.reservedBy == null || node.reservedBy == this)
            {
                node.reservedBy = this;
            }
        }

        reservedWindow.Clear();

        foreach(GridNode node in desired)
        {
            if(node.reservedBy == this)
            {
                reservedWindow.Add(node);
            }
        }
    }

    public void ReleaseAllReservations()
    {
        foreach(GridNode node in reservedWindow)
        {
            if(node.reservedBy == this)
            {
                node.reservedBy = null;
            }
        }

        reservedWindow.Clear();
    }

    bool IsWindowBlocked()
    {
        if (path == null) return false;

        int end = Mathf.Min(pathIndex + reservationWindow, path.Count - 1);

        for (int i = pathIndex; i <= end; i++)
        {
            GridNode node = path[i];

            if (node.reservedBy != null && node.reservedBy != this)
            {
                return true;
            }
        }

        return false;
    }

    GridNode FindMatchingBusTargetNode()
    {
        Bus bus = BusStation.instance.GetBusByColor(passengerColor);
        return bus != null ? bus.targetNode : null;
    }

    public void RequestPathTo(GridNode target)
    {
        ReleaseAllReservations();

        finalTarget = target;
        pathIndex = 0;

        if (target == null)
        {
            path = null;
            isMoving = false;
            agent.UnRegister();
            return;
        }

        path = AStar.instance.FindPath(this, CurrentNode, target);

        if (path == null || path.Count == 0)
        {
            path = null;
            isMoving = false;
            agent.UnRegister();
            return;
        }

        ReserveWindow();
    }

    /// <summary>
    /// Khoang cach Manhattan tu vi tri hien tai toi bus dich (hoac finalTarget dang theo duoi).
    /// Dung de sap xep uu tien trong MoveGroup - ai gan dich hon di truoc.
    /// Tra ve int.MaxValue neu khong xac dinh duoc dich (vi du khong con bus cung mau).
    /// </summary>
    public int GetDistanceToTarget()
    {
        GridNode target = finalTarget != null ? finalTarget : FindMatchingBusTargetNode();

        if (target == null || CurrentNode == null) return int.MaxValue;

        return AStar.GetDistance(CurrentNode, target);
    }


    /// <summary>
    /// Gọi Move cho một nhóm passengers cùng lúc.
    /// Tạm mở walkable cho tất cả ô của nhóm trước khi tìm đường,
    /// để các passenger không chặn đường nhau.
    /// </summary>
    public static void MoveGroup(List<Passenger> group)
    {
        // Uu tien passenger gan dich (bus) hon tim duong va giu o truoc
        group.Sort((a, b) => a.GetDistanceToTarget().CompareTo(b.GetDistanceToTarget()));

        //Tạm mở walkable cho tất cả ô của nhóm
        foreach (Passenger p in group)
        {
            if (p.CurrentNode != null)
            {
                p.CurrentNode.walkable = true;
            }
        }

        // Tìm đường cho từng passenger
        foreach (Passenger p in group)
        {
            p.MoveInternal();

            // Neu tim duco duong thanh cong (dang di chuyen) -> khoa mau nay lai tranh mot nhom mau khac cung mau bi dispatch chong len luc nay
            if(p.isMoving && !p.isDispatchedToBus)
            {
                p.isDispatchedToBus = true;
                IncrementMoving(p.passengerColor);
            }
        }

        // Đánh dấu lại walkable = false cho những passenger chưa tìm được đường (vẫn đứng tại chỗ)
        foreach (Passenger p in group)
        {
            if (p.CurrentNode != null)
            {
                p.CurrentNode.walkable = false;
                p.CurrentNode.occupant = p;
            }
        }
    }

    public void Move()
    {
        // Giữ lại cho backward compatibility nhưng có thể gặp lỗi nếu gọi riêng lẻ
        // Nên dùng MoveGroup() thay thế
        MoveInternal();
        Debug.Log("Move");
    }

    void MoveInternal()
    {
        agent.Register();
        isMoving = true;
        FindPath();
    }

    void FindPath()
    {
        RequestPathTo(FindMatchingBusTargetNode());
    }

    public void EnterBus(Bus bus)
    {
        if(isEntering ) return;

        isEntering = true;
        isMoving = false;

        // Neu da len bus thanh cong -> giai phong mau nay
        if(isDispatchedToBus)
        {
            isDispatchedToBus = false;
            DecrementMoving(passengerColor);
        }

        ReleaseAllReservations();

        // Giải phóng ô hiện tại
        if (CurrentNode != null)
        {
            CurrentNode.walkable = true;
            CurrentNode.occupant = null;
            CurrentNode = null;
        }

        PassengerManager.Instance.Unregister(this);
        agent.UnRegister();

        StartCoroutine(JumpIntoBus(bus));
        bus.UpdateCapcity();
    }

    IEnumerator JumpIntoBus(Bus bus)
    {
        Vector3 start = transform.position;
        Vector3 end = bus.transform.position;

        float duration = 0.4f;
        float t = 0;

        while (t < duration)
        {
            t += Time.deltaTime;

            float p = t / duration;

            Vector3 pos = Vector3.Lerp(start, end, p);

            pos.y += Mathf.Sin(p * Mathf.PI) * 1.5f;

            transform.position = pos;

            yield return null;
        }

        transform.SetParent(bus.transform);

        Destroy(gameObject);
       
    }

    /// <summary>
    /// The implicit loop runs continously to automatically group
    /// </summary>
    /// <returns></returns>
    IEnumerator AutoGroupRoutine()
    {
        yield return new WaitForSeconds(Random.Range(0.1f, 1.0f));

        while (true)
        {
            yield return new WaitForSeconds(1.0f);

            if (isMoving || isEntering) continue;

            if (CurrentNode != null)
            {
                // Kiểm tra xem cả 4 hướng xung quanh có trống hoàn toàn không (không có ai đứng)
                bool isCompletelyIsolated = true;
                foreach (GridNode neighbour in CurrentNode.GetNeighbours())
                {
                    // Nếu có bất kỳ ô nào xung quanh có người đứng (bất kể màu gì), thì không phải isolated
                    if (neighbour != null && neighbour.occupant != null)
                    {
                        isCompletelyIsolated = false;
                        break;
                    }
                }

                if (isCompletelyIsolated)
                {
                    // Đứng 1 mình và 4 hướng đều trống -> tự động tìm chỗ ghép nhóm
                    GridNode emptyNode = PassengerManager.Instance.GetEmptyNodeNearSameColor(this);

                    if (emptyNode != null)
                    {
                        MoveToTargetNode(emptyNode);
                    }
                }
            }
        }
    }

    public void MoveToTargetNode(GridNode target)
    {
        agent.Register();
        isMoving = true;

        bool walkable = CurrentNode.walkable;
        CurrentNode.walkable = true;

        RequestPathTo(target);

        if (path == null)
        {
            isMoving = false;
            agent.UnRegister();
            CurrentNode.walkable = walkable;
        }
    }

    public List<Passenger> GetConnectedPassengers()
    {
        List<Passenger> result = new();

        Queue<GridNode> queue = new();
        HashSet<GridNode> visited = new();

        queue.Enqueue(CurrentNode);
        visited.Add(CurrentNode);

        while (queue.Count > 0)
        {
            GridNode node = queue.Dequeue();

            Passenger passenger = node.occupant;

            if(passenger == null) continue;

            if(passenger.PassengerColor != PassengerColor) continue;

            result.Add(passenger);

            foreach(GridNode neighbour in node.GetNeighbours())
            {
                if(neighbour == null) continue;

                if(visited.Contains(neighbour)) continue;

                Passenger neighbourPassenger = neighbour.occupant;

                if(neighbourPassenger == null) continue;

                if (neighbourPassenger.PassengerColor != PassengerColor) continue;

                visited.Add(neighbour);
                queue.Enqueue(neighbour);
            }
        }

        return result;
    }


}

