using DG.Tweening;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Passenger : MonoBehaviour
{
    PassengerColor passengerColor;
    [SerializeField] MeshRenderer bodyRenderer;
    public PassengerColor PassengerColor => passengerColor;

    /// <summary>
    /// True neu passenger dang DUNG YEN (khong di chuyen, khong dang len xe). Dung boi
    /// HintBooster de tranh chon nham mot passenger dang tren duong lam "seed".
    /// </summary>
    public bool IsAvailableForHint => !isMoving && !isEntering && CurrentNode != null;

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
    GridNode finalTarget;  // dich cuoi cung (bus targetNode hoac target cua MoveToTargetNode), dung de re-path

    [Header("Rotation")]
    [SerializeField] float rotationSpeed = 720f;
    Quaternion idleRotation;

    [Header("Highlight")]
    [Tooltip("GameObject hieu ung glow/outline, la con cua Passenger prefab, mac dinh tatBat len khi mot Booster dang cho nguoi choi chon passenger nay.")]
    [SerializeField] GameObject highlightIndicator;

    Coroutine hintBlinkCoroutine;
    public Bus targetBus; // Bus cu the ma passenger dang huong toi 

    private void Start()
    {
        agent = GetComponent<RVOAgent>();
        isMoving = false;
        agent.UnRegister();

        float cell = PassengerGrid.Instance != null ? PassengerGrid.Instance.cellSize : 1f;

        StartCoroutine(AutoGroupRoutine());
        idleRotation = transform.rotation;
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
                agent.UnRegister();

                if (isDispatchedToBus)
                {
                    isDispatchedToBus = false;
                    DecrementMoving(targetBus);
                }

                // Căn chỉnh vị trí chính xác vào giữa ô lưới
                if (CurrentNode != null)
                {
                    Vector3 exactPos = CurrentNode.worldPosition;
                    exactPos.y = transform.position.y; // Giữ nguyên độ cao Y
                    transform.position = exactPos;
                }
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, idleRotation, rotationSpeed * Time.deltaTime);

            return;
        }

        Vector3 targetPos = path[pathIndex].worldPosition;
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;   // game top-down: chi di chuyen tren mat phang XZ

        float speed = Random.Range(agent.maxSpeed - 1.5f, agent.maxSpeed);
        agent.preferredVelocity = dir.normalized * speed;

        Vector3 faceDir = agent.velocity;
        faceDir.y = 0f;

        if (faceDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetPosition = Quaternion.LookRotation(faceDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetPosition, rotationSpeed * Time.deltaTime);
        }

        if (dir.magnitude < 0.3f)
        {
            CurrentNode.walkable = true;
            CurrentNode.occupant = null;

            CurrentNode = path[pathIndex];

            CurrentNode.walkable = false;
            CurrentNode.occupant = this;

            pathIndex++;
        }
    }

    public void SetHighLight(bool on, float duration = 3f)
    {
        if (hintBlinkCoroutine != null)
        {
            StopCoroutine(hintBlinkCoroutine);
            hintBlinkCoroutine = null;
        }

        if (on)
        {
            hintBlinkCoroutine = StartCoroutine(HintBlinkRoutine(duration));
        }
        else if (highlightIndicator != null)
        {
            highlightIndicator.SetActive(false);
        }
    }

    IEnumerator HintBlinkRoutine(float duration)
    {
        if (highlightIndicator == null) yield break;

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            highlightIndicator.SetActive(Mathf.PingPong(t * 4f, 1f) > 0.5f); // nhap nhay

            yield return null;
        }

        highlightIndicator.SetActive(false);
        //highlightIndicator = null;
    }

    static readonly Dictionary<Bus, int> movingCountByBus = new();

    /// <summary>
    /// Kiem tra xem mau nay hien co dang co passenger nao dang duoc disptach ve bus hay khong, passengerselector se dung ham nay de chan click chon them
    /// cung mau khi dang co nhom di chuyen tranh tinh trang dispatch chong len nhau lam ngheo bus
    /// </summary>
    /// <param name="passengerColor"></param>
    /// <returns></returns>
    public static bool IsBusMoving(Bus bus)
    {
        return bus != null && movingCountByBus.TryGetValue(bus, out var count) && count > 0;
    }

    static void IncrementMoving(Bus bus)
    {
        if (bus == null) return;

        movingCountByBus.TryGetValue(bus, out int count);
        movingCountByBus[bus] = count + 1;
    }

    static void DecrementMoving(Bus bus)
    {
        if (bus == null) return;

        if (movingCountByBus.TryGetValue(bus, out int count))
        {
            movingCountByBus[bus] = Mathf.Max(0, count - 1);
        }
    }

    public void SetColor(PassengerColor color)
    {
        passengerColor = color;

        bodyRenderer.material = color.material;
    }

    GridNode FindMatchingBusTargetNode()
    {
        // Neu da co bú dang bam va bú do van con cho -> giu nguyen, tranh doi bú giua duong
        if (targetBus != null && targetBus.GetAvailableSeat() > 0)
        {
            return targetBus.targetNode;
        }

        Bus bus = BusStation.instance.GetNearestBusByColor(passengerColor, transform.position);
        targetBus = bus;
        return bus != null ? bus.targetNode : null;
    }

    public void RequestPathTo(GridNode target)
    {

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

    static Dictionary<Passenger, int> ComputeLayers(List<Passenger> group)
    {
        Dictionary<Passenger, int> layer = new();

        HashSet<GridNode> groupNodes = new(group.Select(p => p.CurrentNode));

        Queue<Passenger> queue = new();

        // Tim layer 0 : nhung nguoi co huong ra ngoai cum ( ko  bi bao vay het 8 huong)

        foreach (Passenger p in group)
        {
            bool isEdge = false;

            foreach (GridNode neighbour in p.CurrentNode.GetNeighbours())
            {
                if (neighbour == null || !groupNodes.Contains(neighbour))
                {
                    isEdge = true;
                }
            }

            if (isEdge)
            {
                layer[p] = 0;
                queue.Enqueue(p);
            }
        }

        // Lan tu ngoai vao trong 

        while (queue.Count > 0)
        {
            Passenger current = queue.Dequeue();

            int curLayer = layer[current];

            foreach (GridNode neighbour in current.CurrentNode.GetNeighbours())
            {
                if (neighbour == null || !groupNodes.Contains(neighbour)) continue;

                Passenger np = neighbour.occupant;

                if (np == null || layer.ContainsKey(np)) continue;

                layer[np] = curLayer + 1;

                queue.Enqueue(np);
            }
        }

        // Truong hop bi sot

        foreach (Passenger p in group)
        {
            if (!layer.ContainsKey(p))
                layer[p] = int.MaxValue;
        }

        return layer;
    }

    /// <summary>
    /// Gọi Move cho một nhóm passengers cùng lúc.
    /// Tạm mở walkable cho tất cả ô của nhóm trước khi tìm đường,
    /// để các passenger không chặn đường nhau.
    /// </summary>
    public static IEnumerator MoveGroup(List<Passenger> group)
    {
        if (group.Count == 0) yield break;

        Dictionary<Passenger, int> layerOf = ComputeLayers(group);

        // Mo walkable cho ca nhom

        foreach (Passenger p in group)
        {
            if (p.CurrentNode != null)
                p.CurrentNode.walkable = true;
        }

        // Gom theo layer, layer ngoai thi xu li truoc

        var layerGroups = group.GroupBy(p => layerOf[p]).OrderBy(g => g.Key);

        foreach (var layerGroup in layerGroups)
        {
            // Trong  1 layer uu tien gan bus hon
            var sorted = layerGroup.OrderBy(p => p.GetDistanceToTarget());

            foreach (Passenger p in sorted)
            {
                p.MoveInternal();

                if (p.isMoving && !p.isDispatchedToBus)
                {
                    p.isDispatchedToBus = true;
                    IncrementMoving(p.targetBus);
                }
            }

            // Cho lop nay di chuyen ra ngoai roi moi den lop tiep

            yield return new WaitForSeconds(0.5f);
        }

        // Danh dau lai walkable = false cho ai chua tim dc duong

        foreach (Passenger p in group)
        {
            if (p.CurrentNode != null && !p.isMoving)
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
        if (isEntering) return;

        isEntering = true;
        isMoving = false;

        // Neu da len bus thanh cong -> giai phong mau nay
        if (isDispatchedToBus)
        {
            isDispatchedToBus = false;
            DecrementMoving(targetBus);
        }

        // Giải phóng ô hiện tại
        if (CurrentNode != null)
        {
            CurrentNode.walkable = true;
            CurrentNode.occupant = null;
            CurrentNode = null;
        }

        PassengerManager.Instance.Unregister(this);
        agent.UnRegister();

        bus.PlayBoardEffect();
        bus.UpdateCapcity();
        StartCoroutine(JumpIntoBus(bus));


        BusStation.instance.OnPassengerBoardedColor(PassengerColor);
    }

    IEnumerator JumpIntoBus(Bus bus)
    {
        //Vector3 start = transform.position;
        //Vector3 end = bus.transform.position;

        //float duration = 0.4f;
        //float t = 0;

        //while (t < duration)
        //{
        //    t += Time.deltaTime;

        //    float p = t / duration;

        //    Vector3 pos = Vector3.Lerp(start, end, p);

        //    pos.y += Mathf.Sin(p * Mathf.PI) * 1.5f;

        //    transform.position = pos;

        //    yield return null;
        //}

        bool done = false;

        transform.DOJump(bus.transform.position, 1.5f, 1, 0.4f)
                 .SetEase(Ease.OutQuad)
                 .OnComplete(() => { done = true; });

        yield return new WaitUntil(() => done);

        transform.SetParent(bus.transform);

        Destroy(gameObject);

    }

    /// <summary>
    /// Dung boi Booster "Hut Passenger": passenger bay thang len tren (vao "lo hong" hieu ung phia
    /// tren dau) roi bien mat, thay vi bay theo duong toi vi tri bus nhu JumpIntoBus() thong thuong.
    /// Bo qua hoan toan pathfinding - day la cong cu "chua chay" khi passenger bi ket khong co duong ra.
    /// </summary>
    public IEnumerator SuctionIntoBus(Bus bus, float riseHeight, float duration)
    {
        if (isEntering) yield break;

        isEntering = true;
        isMoving = false;

        if (isDispatchedToBus)
        {
            isDispatchedToBus = false;
            DecrementMoving(targetBus);
        }

        if (CurrentNode != null)
        {
            CurrentNode.walkable = true;
            CurrentNode.occupant = null;
            CurrentNode = null;
        }

        PassengerManager.Instance.Unregister(this);
        agent.UnRegister();

        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * riseHeight;
        Vector3 startScale = transform.localScale;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);

            transform.position = Vector3.Lerp(start, end, p);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, p); // teo nho dan khi bay len

            yield return null;
        }

        bus.UpdateCapcity();

        BusStation.instance.OnPassengerBoardedColor(PassengerColor);

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

            if (passenger == null) continue;

            if (passenger.PassengerColor != PassengerColor) continue;

            result.Add(passenger);

            foreach (GridNode neighbour in node.GetNeighbours())
            {
                if (neighbour == null) continue;

                if (visited.Contains(neighbour)) continue;

                Passenger neighbourPassenger = neighbour.occupant;

                if (neighbourPassenger == null) continue;

                if (neighbourPassenger.PassengerColor != PassengerColor) continue;

                visited.Add(neighbour);
                queue.Enqueue(neighbour);
            }
        }

        return result;
    }
}

