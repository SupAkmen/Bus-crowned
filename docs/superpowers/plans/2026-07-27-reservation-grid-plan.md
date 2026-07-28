# Reservation Grid (Sliding Window) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current whole-path grid reservation (which locks every cell of a passenger's path the instant a path is found, and never cleans up stale locks) with a sliding 4-cell reservation window per passenger, immediate re-pathing when the window ahead gets blocked, and distance-to-target priority ordering inside `MoveGroup`, so passengers moving together no longer deadlock or block each other unnecessarily.

**Architecture:** `AStar` stops mutating reservation state — it only searches. `Passenger` becomes the sole owner of its own reservation state: it reserves only the next `ReservationWindow` (4) cells of its current path, releases everything before requesting a new path, and re-requests a path automatically the moment a cell in its window becomes blocked by someone else. `MoveGroup` sorts its passengers by remaining distance to their bus target before letting them claim cells, so passengers closer to the exit get first pick.

**Tech Stack:** Unity (C#, MonoBehaviour), existing custom grid A* (`AStar.cs`), no external pathfinding/physics library involved.

## Global Constraints

- Full spec: `docs/superpowers/specs/2026-07-27-reservation-grid-design.md` — read it before starting if anything below is ambiguous.
- Sliding reservation window size is fixed at **4 cells** (not configurable/dynamic). Do not make it depend on `RVOAgent.maxSpeed`.
- `MoveGroup` priority order is Manhattan distance to the passenger's matching bus `targetNode`, ascending (closer first).
- When a passenger's reservation window is blocked mid-route, it must re-path **immediately** (same frame it's detected), not wait or queue.
- `GridNode.cs` must NOT be modified — no time-indexed reservation table, no new fields on `GridNode`.
- `RVOAgent`/`RVOSimulator` (local velocity-based avoidance) must NOT be modified.
- `PassengerGrid`, `PassengerManager`, `PassengerSpawnAreas`, `PassengerSelector`, `BusStation`, `Bus` must NOT be modified.
- **This project is not a git repository** (confirmed: no `.git` folder). Skip every "commit" step you'd normally do after a task — there is nothing to commit to. Each task ends with a manual verification checkpoint instead; do not proceed to the next task until that checkpoint passes.
- **No automated test framework exists in this project** (no `Tests` folder, no `.asmdef` for tests). All "tests" in this plan are manual Unity Editor Play Mode checks — this matches the project's existing testing reality, it is not a shortcut.
- Match existing code style: Vietnamese inline comments for game-logic explanations (as already used throughout `Passenger.cs`/`AStar.cs`), 4-space indentation, no braces-on-same-line changes to surrounding code you're not touching.

---

## Task 1: Stop `AStar` from auto-reserving whole paths

**Files:**
- Modify: `Assets/Scripts/Other/AStar.cs:53-56` (remove whole-path reservation)
- Modify: `Assets/Scripts/Other/AStar.cs:108-114` (`GetDistance` → `public static`)

**Interfaces:**
- Consumes: nothing new.
- Produces: `AStar.GetDistance(GridNode a, GridNode b)` — now `public static`, callable as `AStar.GetDistance(...)` from any class (Task 3's `Passenger.GetDistanceToTarget()` depends on this).

After this task, the only reservation still happening is the pre-existing "reserve my current standing cell" logic in `Passenger.Update()` (unchanged in this task) — full sliding-window behavior lands in Task 2. This is an expected, safe intermediate state: passengers still path and move correctly; they just don't yet reserve cells ahead of themselves, which is fine because `walkable=false` on the currently-occupied cell already prevents anyone from stepping into an occupied cell.

- [ ] **Step 1: Remove the whole-path reservation loop in `AStar.FindPath`**

In `Assets/Scripts/Other/AStar.cs`, find:

```csharp
            if(currentNode == targetNode)
            {
                List<GridNode> path = RetracePath(startNode, targetNode);

                foreach(GridNode node in path)
                {
                    node.reservedBy = currentPassenger;
                }

                return path;
            }
```

Replace with:

```csharp
            if(currentNode == targetNode)
            {
                // Reservation la trach nhiem cua Passenger (sliding window), khong reserve toan bo path o day.
                return RetracePath(startNode, targetNode);
            }
```

- [ ] **Step 2: Make `GetDistance` public static**

In `Assets/Scripts/Other/AStar.cs`, find:

```csharp
    int GetDistance(GridNode a,GridNode b)
    {
        int dx = Mathf.Abs(a.column - b.column);
        int dy = Mathf.Abs(a.row - b.row);

        return dx + dy;
    }
```

Replace with:

```csharp
    public static int GetDistance(GridNode a,GridNode b)
    {
        int dx = Mathf.Abs(a.column - b.column);
        int dy = Mathf.Abs(a.row - b.row);

        return dx + dy;
    }
```

- [ ] **Step 3: Verify it compiles**

Open the project in Unity Editor (or focus it if already open) and let it recompile. Check the Console window.

Expected: no new compile errors or warnings referencing `AStar.cs`.

- [ ] **Step 4: Manual verification checkpoint**

Enter Play Mode. Click one small single-color passenger group and confirm:
- They still find a path and walk to their matching bus.
- They still board the bus normally (`capacityText` decreases).

Expected: identical behavior to before this task for a single, uncontested group — this task only removes *redundant future-cell locking*, it doesn't change movement for a passenger moving alone. Exit Play Mode when confirmed.

---

## Task 2: Sliding reservation window + automatic re-path when blocked

**Files:**
- Modify: `Assets/Scripts/Passenger/Passenger.cs:9-20` (new fields)
- Modify: `Assets/Scripts/Passenger/Passenger.cs:30-78` (`Update()`)
- Modify: `Assets/Scripts/Passenger/Passenger.cs:136-166` (`FindPath()`)
- Modify: `Assets/Scripts/Passenger/Passenger.cs:168-190` (`EnterBus()`)
- Modify: `Assets/Scripts/Passenger/Passenger.cs:263-281` (`MoveToTargetNode()`)
- Add new methods: `ReserveWindow()`, `ReleaseAllReservations()`, `IsWindowBlocked()`, `FindMatchingBusTargetNode()`, `RequestPathTo(GridNode)`

**Interfaces:**
- Consumes: `AStar.instance.FindPath(Passenger, GridNode, GridNode)` (unchanged signature), `AStar.GetDistance` (not used yet here, used in Task 3), `BusStation.instance.parkingBusList`, `Bus.passengerColor`, `Bus.targetNode` (all pre-existing, unchanged).
- Produces: `Passenger.RequestPathTo(GridNode target)` (public, void) and `Passenger.ReleaseAllReservations()` (public, void) — both consumed nowhere else yet in this task, but `RequestPathTo` becomes the single path-request entry point used internally by `FindPath()`/`MoveToTargetNode()`, and `GetDistanceToTarget()` in Task 3 will call the new private `FindMatchingBusTargetNode()`.

- [ ] **Step 1: Add sliding-window fields**

In `Assets/Scripts/Passenger/Passenger.cs`, find:

```csharp
    List<GridNode> path;
    int pathIndex;

    RVOAgent agent;

    bool isEntering;
    bool isMoving;   // đang trong quá trình di chuyển theo path
```

Replace with:

```csharp
    List<GridNode> path;
    int pathIndex;

    RVOAgent agent;

    bool isEntering;
    bool isMoving;   // đang trong quá trình di chuyển theo path

    const int ReservationWindow = 4; // so o phia truoc duoc giu truoc, tinh tu pathIndex hien tai
    readonly HashSet<GridNode> reservedWindow = new HashSet<GridNode>();
    GridNode finalTarget; // dich cuoi cung (bus targetNode hoac target cua MoveToTargetNode), dung de re-path
```

- [ ] **Step 2: Add `ReserveWindow()`, `ReleaseAllReservations()`, `IsWindowBlocked()` methods**

Add these three new methods to the `Passenger` class (place them right after the `SetColor` method, i.e. after line 86 `}` and before the `MoveGroup` doc-comment on line 87):

```csharp
    /// <summary>
    /// Giu truoc ReservationWindow o ke tiep tren path (tinh tu pathIndex hien tai),
    /// giai phong cac o da giu truoc do khong con nam trong cua so.
    /// </summary>
    void ReserveWindow()
    {
        HashSet<GridNode> desired = new HashSet<GridNode>();

        if (path != null)
        {
            int end = Mathf.Min(pathIndex + ReservationWindow - 1, path.Count - 1);

            for (int i = pathIndex; i <= end; i++)
            {
                desired.Add(path[i]);
            }
        }

        // Giai phong nhung o khong con trong cua so moi
        foreach (GridNode node in reservedWindow)
        {
            if (!desired.Contains(node) && node.reservedBy == this)
            {
                node.reservedBy = null;
            }
        }

        // Chiem cac o trong cua so con trong hoac dang la cua chinh minh
        foreach (GridNode node in desired)
        {
            if (node.reservedBy == null || node.reservedBy == this)
            {
                node.reservedBy = this;
            }
        }

        reservedWindow.Clear();

        foreach (GridNode node in desired)
        {
            if (node.reservedBy == this)
            {
                reservedWindow.Add(node);
            }
        }
    }

    /// <summary>
    /// Giai phong toan bo reservation dang giu. Goi truoc khi tim path moi hoac khi len bus.
    /// </summary>
    public void ReleaseAllReservations()
    {
        foreach (GridNode node in reservedWindow)
        {
            if (node.reservedBy == this)
            {
                node.reservedBy = null;
            }
        }

        reservedWindow.Clear();
    }

    /// <summary>
    /// Kiem tra cac o con lai trong cua so reservation phia truoc co bi passenger khac giu khong.
    /// </summary>
    bool IsWindowBlocked()
    {
        if (path == null) return false;

        int end = Mathf.Min(pathIndex + ReservationWindow - 1, path.Count - 1);

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
```

- [ ] **Step 3: Add `FindMatchingBusTargetNode()` and `RequestPathTo(GridNode)`**

Add these two new methods right after the three added in Step 2 (still before the `MoveGroup` doc-comment):

```csharp
    /// <summary>
    /// Tim targetNode cua bus dau tien trong parkingBusList cung mau voi passenger nay.
    /// Tra ve null neu khong co bus nao cung mau.
    /// </summary>
    GridNode FindMatchingBusTargetNode()
    {
        foreach (Bus bus in BusStation.instance.parkingBusList)
        {
            if (bus.passengerColor != passengerColor) continue;

            return bus.targetNode;
        }

        return null;
    }

    /// <summary>
    /// Diem vao duy nhat de yeu cau mot path moi toi target.
    /// Luon giai phong reservation cu truoc khi tim path moi, tranh o bi khoa "rac".
    /// </summary>
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
```

- [ ] **Step 4: Replace `FindPath()` to use the new entry point**

Find:

```csharp
    void FindPath()
    {
        path = null;
        pathIndex = 0;

        foreach (Bus bus in BusStation.instance.parkingBusList)
        {
            if (bus.passengerColor != passengerColor) continue;

            path = AStar.instance.FindPath(this,CurrentNode, bus.targetNode);

            if (path != null && path.Count > 0)
            {
               // Debug.Log($"{name} FindPath() success, steps={path.Count}");
            }
            else
            {
                //Debug.LogWarning($"{name} FindPath() FAILED - no path found!");
                path = null;
            }

            break;
        }

        // Nếu không tìm được đường, unregister khỏi RVO
        if (path == null)
        {
            isMoving = false;
            agent.UnRegister();
        }
    }
```

Replace with:

```csharp
    void FindPath()
    {
        RequestPathTo(FindMatchingBusTargetNode());
    }
```

- [ ] **Step 5: Replace `MoveToTargetNode()` to use the new entry point**

Find:

```csharp
    public void MoveToTargetNode(GridNode target)
    {
        agent.Register();
        isMoving = true;

        bool walkable = CurrentNode.walkable;
        CurrentNode.walkable = true;

        path = AStar.instance.FindPath(this, CurrentNode, target);

        pathIndex = 0;

        if (path == null || path.Count == 0)
        {
            isMoving = false;
            agent.UnRegister();
            CurrentNode.walkable = walkable;
        }
    }
```

Replace with:

```csharp
    public void MoveToTargetNode(GridNode target)
    {
        agent.Register();
        isMoving = true;

        bool previousWalkable = CurrentNode.walkable;
        CurrentNode.walkable = true;

        RequestPathTo(target);

        if (path == null)
        {
            isMoving = false;
            agent.UnRegister();
            CurrentNode.walkable = previousWalkable;
        }
    }
```

- [ ] **Step 6: Wire block-detection and window sliding into `Update()`**

Find:

```csharp
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

        Vector3 targetPos = path[pathIndex].worldPosition;

        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;   // game top-down: chi di chuyen tren mat phang XZ

        float speed = Random.Range(agent.maxSpeed - 1.5f,agent.maxSpeed + 1f);

        agent.preferredVelocity = dir.normalized * agent.maxSpeed;

        if (dir.magnitude < 0.15f)
        {
            CurrentNode.reservedBy = null;
            CurrentNode.walkable = true;
            CurrentNode.occupant = null;

            CurrentNode = path[pathIndex];

            CurrentNode.walkable = false;
            CurrentNode.occupant = this;
            CurrentNode.reservedBy = this;

            pathIndex++;
        }
    }
```

Replace with:

```csharp
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

        // O phia truoc trong cua so reservation bi passenger khac giu -> tim lai duong ngay
        if (IsWindowBlocked())
        {
            RequestPathTo(finalTarget);

            if (path == null || pathIndex >= path.Count) return;
        }

        Vector3 targetPos = path[pathIndex].worldPosition;

        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;   // game top-down: chi di chuyen tren mat phang XZ

        float speed = Random.Range(agent.maxSpeed - 1.5f,agent.maxSpeed + 1f);

        agent.preferredVelocity = dir.normalized * agent.maxSpeed;

        if (dir.magnitude < 0.15f)
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
```

- [ ] **Step 7: Release reservations when boarding the bus**

Find:

```csharp
    public void EnterBus(Bus bus)
    {
        if(isEntering ) return;

        isEntering = true;
        isMoving = false;

        // Giải phóng ô hiện tại
        if (CurrentNode != null)
        {
            CurrentNode.walkable = true;
            CurrentNode.occupant = null;
            CurrentNode = null;
        }
```

Replace with:

```csharp
    public void EnterBus(Bus bus)
    {
        if(isEntering ) return;

        isEntering = true;
        isMoving = false;

        ReleaseAllReservations();

        // Giải phóng ô hiện tại
        if (CurrentNode != null)
        {
            CurrentNode.walkable = true;
            CurrentNode.occupant = null;
            CurrentNode = null;
        }
```

- [ ] **Step 8: Verify it compiles**

Open/focus Unity Editor, let it recompile, check Console.

Expected: no compile errors referencing `Passenger.cs`.

- [ ] **Step 9: Manual verification checkpoint — single group**

Enter Play Mode. Click one small single-color group and confirm they walk to the bus and board normally, same as Task 1's checkpoint.

Expected: no regression for the simple case.

- [ ] **Step 10: Manual verification checkpoint — crossing groups**

In Play Mode, set up (or find in the current scene) two same-color-or-different-color passenger groups whose paths to their respective buses cross each other. Click to move both groups at roughly the same time (or move one, then immediately move a second group whose path crosses the first).

Expected:
- No two passengers ever visually occupy the same grid cell at the same time.
- If a passenger's upcoming path briefly gets blocked by another passenger crossing it, that passenger visibly pauses for a moment and then continues (re-path happened) — it does not freeze permanently or get stuck reserving a cell it never reaches.

Exit Play Mode when confirmed.

---

## Task 3: Priority ordering in `MoveGroup` by distance to target

**Files:**
- Modify: `Assets/Scripts/Passenger/Passenger.cs:92-119` (`MoveGroup`)
- Add new method: `GetDistanceToTarget()`

**Interfaces:**
- Consumes: `AStar.GetDistance(GridNode, GridNode)` (made `public static` in Task 1), `FindMatchingBusTargetNode()` (added in Task 2), `finalTarget` field (added in Task 2).
- Produces: `Passenger.GetDistanceToTarget()` (public, int) — used only by `MoveGroup`'s sort in this task, but public in case other systems need "how far is this passenger from boarding" later.

- [ ] **Step 1: Add `GetDistanceToTarget()`**

Add this method to the `Passenger` class, right after `RequestPathTo` (added in Task 2 Step 3) and before `MoveGroup`:

```csharp
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
```

- [ ] **Step 2: Sort the group by distance before finding paths**

Find:

```csharp
    public static void MoveGroup(List<Passenger> group)
    {
        // Bước 1: Tạm mở walkable cho tất cả ô của nhóm
        foreach (Passenger p in group)
        {
            if (p.CurrentNode != null)
            {
                p.CurrentNode.walkable = true;
            }
        }
```

Replace with:

```csharp
    public static void MoveGroup(List<Passenger> group)
    {
        // Uu tien passenger gan dich (bus) hon tim duong va giu o truoc
        group.Sort((a, b) => a.GetDistanceToTarget().CompareTo(b.GetDistanceToTarget()));

        // Bước 1: Tạm mở walkable cho tất cả ô của nhóm
        foreach (Passenger p in group)
        {
            if (p.CurrentNode != null)
            {
                p.CurrentNode.walkable = true;
            }
        }
```

- [ ] **Step 3: Verify it compiles**

Open/focus Unity Editor, let it recompile, check Console.

Expected: no compile errors referencing `Passenger.cs`.

- [ ] **Step 4: Manual verification checkpoint — bottleneck group**

In Play Mode, set up (or find) a large single-color group (10+ passengers) that must funnel through a narrow corridor toward one bus, with passengers at varying distances from the bus.

Expected: compared to pre-plan behavior, fewer passengers fail to find a path or get stuck standing in place mid-group-move — passengers closer to the bus consistently get to claim their path cells first, and passengers farther back path around them instead of contesting the same cells.

Exit Play Mode when confirmed. This is the final task — all three spec requirements (sliding window, auto re-path, group priority) are now implemented.

---

## Post-implementation notes

- The spec (`docs/superpowers/specs/2026-07-27-reservation-grid-design.md`) section 7 flags a known limitation: repeated immediate re-pathing in very dense crowds could be a future performance concern. Not addressed by this plan — out of scope per spec.
- Since there's no git repository, consider asking the user whether they want one initialized now that this feature is done, so future changes can be tracked and reverted if needed. Do not initialize git without asking first.
