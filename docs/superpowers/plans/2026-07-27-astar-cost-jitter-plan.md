# A* Cost Jitter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let passengers take visibly meandering, non-shortest paths toward their bus (not just tie-broken-but-still-optimal paths) by adding a tunable random jitter to A*'s edge cost, exposed as an Inspector field so the amount of "messiness" can be tuned without touching code.

**Architecture:** `AStar.FindPath` adds `Random.Range(0, costJitter + 1)` to the computed edge cost (`newCost`) for every neighbour it evaluates. The rest of the A* algorithm (open/closed list bookkeeping, best-node selection, termination) is untouched, so the search still always terminates and still finds *a* path whenever one exists — it just no longer finds the shortest one when `costJitter > 0`. `costJitter` is a `[SerializeField] [Range(0, 20)]` field on `AStar`, default `6`.

**Tech Stack:** Unity (C#), existing custom grid A* (`AStar.cs`). No new dependencies.

## Global Constraints

- Full spec: `docs/superpowers/specs/2026-07-27-reservation-grid-design.md` §10 — read it before starting if anything below is ambiguous.
- `costJitter` default is **6**, Inspector range **0–20**, field name **`costJitter`**, type `int`, `[SerializeField]` (not fully public — same visibility style as other tunables already in this codebase, e.g. `Passenger.CurrentNode` is public but internal tuning knobs elsewhere use `[SerializeField]`; here we specifically chose `[SerializeField] [Range(0, 20)] int costJitter` per the user's confirmed answer).
- Do NOT change how `gCost`/`hCost`/`fCost` are compared to pick the best open-list node (lines with the `for(int i = 1; i < openList.Count; i++)` loop) — only how `newCost` is computed changes.
- Do NOT remove or alter the existing `ShuffleNeighbours` tie-breaking helper from the prior plan (`docs/superpowers/plans/2026-07-27-astar-random-tiebreak-plan.md`) — this plan adds jitter on top of it, it does not replace it.
- Do NOT modify `GridNode.cs`, `Passenger.cs`, `PassengerGrid.cs`, or any other file — this plan touches only `Assets/Scripts/Other/AStar.cs`.
- Do NOT change the sliding-window reservation logic or `MoveGroup` priority ordering — those are already implemented and fixed.
- **This project is not a git repository.** Skip "commit" steps — each task ends with a manual Play Mode verification checkpoint instead.
- **No automated test framework exists in this project.** All verification is manual Unity Editor Play Mode checks.
- Match existing code style: Vietnamese inline comments for game-logic explanations, 4-space indentation.
- Per user's explicit workflow choice: present each step for the user to type/paste by hand — do not use Edit/Write on `.cs` files for this plan.

---

## Task 1: Add tunable cost jitter to `AStar.FindPath`

**Files:**
- Modify: `Assets/Scripts/Other/AStar.cs` (add field near the top of the class; modify the `newCost` calculation inside `FindPath`)

**Interfaces:**
- Consumes: nothing new.
- Produces: `AStar.costJitter` — a `[SerializeField]` int field, visible and editable in the Unity Inspector on the `AStar` component. No other script reads it in this plan; it's purely a designer-facing tuning knob.

- [ ] **Step 1: Add the `costJitter` field**

In `Assets/Scripts/Other/AStar.cs`, find:

```csharp
public class AStar : MonoBehaviour
{
    public static AStar instance;
```

Replace with:

```csharp
public class AStar : MonoBehaviour
{
    public static AStar instance;

    [SerializeField] [Range(0, 20)] int costJitter = 6; // nhieu ngau nhien cong vao chi phi moi canh - 0 = duong toi uu nhu cu, cang lon cang di vong nhieu
```

- [ ] **Step 2: Apply the jitter to the edge cost calculation**

In `Assets/Scripts/Other/AStar.cs`, find, inside `FindPath`:

```csharp
                int newCost = currentNode.gCost + GetDistance(currentNode,neighbour);
```

Replace with:

```csharp
                int newCost = currentNode.gCost + GetDistance(currentNode,neighbour) + Random.Range(0, costJitter + 1);
```

- [ ] **Step 3: Verify it compiles**

Return to the Unity Editor, let it recompile, check the Console.

Expected: no compile errors referencing `AStar.cs`.

- [ ] **Step 4: Set the value in the Inspector and verify it's visible**

In the Unity Editor, select the GameObject that has the `AStar` component attached (the one whose `Awake()` sets `AStar.instance`). Confirm a "Cost Jitter" slider now appears in its Inspector, ranged 0–20, defaulting to 6.

Expected: field is visible and adjustable without needing a script edit.

- [ ] **Step 5: Manual verification checkpoint — jittered paths at default value (6)**

With `costJitter` left at its default (6), enter Play Mode. Click a large single-color group (10+ passengers) in an open area heading to the same bus.

Expected: passengers now take visibly winding, non-shortest routes to the bus — more pronounced deviation than the tie-breaking-only behavior from the previous plan. They should still reliably reach the bus and board (no passenger gets permanently stuck wandering).

- [ ] **Step 6: Manual verification checkpoint — `costJitter = 0` restores optimal pathing**

Exit Play Mode. Set `costJitter` to `0` in the Inspector. Enter Play Mode again and repeat the same group-move test.

Expected: passengers path directly/optimally again, matching the pre-jitter behavior (this confirms the jitter is fully responsible for the wandering, and can be dialed back for debugging or design taste).

- [ ] **Step 7: Manual verification checkpoint — no regression on reservation/blocking behavior**

With `costJitter` back at 6 (or any non-zero value you plan to ship with), repeat the crossing-groups and bottleneck-group checkpoints from the earlier plans:
- Two crossing groups still avoid occupying the same cell simultaneously; a blocked passenger still re-paths and continues instead of freezing.
- A large group funneling through a bottleneck still generally favors closer-to-target passengers claiming cells first, though with jitter on, some variance here is expected and fine.

Expected: no deadlocks, no permanent freezes, no overlapping passengers — jitter changes *which* path is found, not the safety guarantees from the reservation system. Exit Play Mode when confirmed.

---

## Post-implementation notes

- If `costJitter` at high values (e.g. 15–20) ever causes a passenger to take a path so long it looks broken/stuck-in-a-loop-looking, that's expected per spec §10's accepted risk — lower the Inspector value rather than treating it as a bug.
- No new field was added to `Passenger.cs` or `GridNode.cs` — `costJitter` lives solely on the `AStar` singleton, affecting every passenger's path requests uniformly.
