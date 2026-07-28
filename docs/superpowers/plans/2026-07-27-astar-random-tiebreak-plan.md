# A* Random Tie-Breaking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make passengers in the same `MoveGroup` take visibly different, natural-looking paths instead of marching in an identical line/column, by randomizing neighbour visitation order in `AStar.FindPath` so ties between equal-cost routes are broken differently each call.

**Architecture:** `AStar.FindPath` copies the current node's neighbours into a list and shuffles it (Fisher-Yates, `UnityEngine.Random.Range`) before iterating. This only changes which equal-`fCost` neighbour gets explored/added to the open list first — the `gCost`/`hCost` comparison logic in the existing best-node selection loop is untouched, so path optimality is preserved; only the choice among equally-good branches becomes randomized per call.

**Tech Stack:** Unity (C#), existing custom grid A* (`AStar.cs`). No new dependencies.

## Global Constraints

- Full spec: `docs/superpowers/specs/2026-07-27-reservation-grid-design.md` §9 — read it before starting if anything below is ambiguous.
- Do NOT change how `gCost`/`hCost`/`fCost` are computed or compared. Only the iteration order of neighbours changes.
- Do NOT modify `GridNode.cs`, `Passenger.cs`, `PassengerGrid.cs`, or any other file — this plan touches only `Assets/Scripts/Other/AStar.cs`.
- Do NOT change the sliding-window reservation logic (§4.1/§4.2 of the spec) or the `MoveGroup` priority ordering (§4.3) — those are already implemented and fixed (§8 of the spec).
- **This project is not a git repository.** Skip "commit" steps — each task ends with a manual Play Mode verification checkpoint instead.
- **No automated test framework exists in this project.** All verification is manual Unity Editor Play Mode checks.
- Match existing code style: Vietnamese inline comments for game-logic explanations, 4-space indentation.
- Per user's explicit workflow choice: present each step for the user to type/paste by hand — do not use Edit/Write on `.cs` files for this plan.

---

## Task 1: Randomize neighbour order in `AStar.FindPath`

**Files:**
- Modify: `Assets/Scripts/Other/AStar.cs:54` (the `foreach(GridNode neighbour in currentNode.GetNeighbours())` loop)

**Interfaces:**
- Consumes: `GridNode.GetNeighbours()` (unchanged, still yields up to 4 non-null neighbours in `Up, Down, Left, Right` order).
- Produces: no new public members. `AStar.FindPath`'s external signature and return type are unchanged — only its internal exploration order changes, which is invisible to callers (`Passenger.RequestPathTo`, etc.).

- [ ] **Step 1: Add a private static shuffle helper**

In `Assets/Scripts/Other/AStar.cs`, add this method right after `GetDistance` (at the end of the class, before the closing `}` of `AStar`):

```csharp
    /// <summary>
    /// Xao tron danh sach neighbour (Fisher-Yates) de A* khong luon chon
    /// cung 1 nhanh khi nhieu duong co cung fCost - giup path cua cac
    /// passenger khac nhau tu nhien hon thay vi di thanh hang cung mot tuyen.
    /// </summary>
    static List<GridNode> ShuffleNeighbours(GridNode node)
    {
        List<GridNode> neighbours = new List<GridNode>(node.GetNeighbours());

        for (int i = neighbours.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            (neighbours[i], neighbours[j]) = (neighbours[j], neighbours[i]);
        }

        return neighbours;
    }
```

- [ ] **Step 2: Use the shuffled list in the main search loop**

Find, inside `FindPath`:

```csharp
            foreach(GridNode neighbour in currentNode.GetNeighbours())
            {
```

Replace with:

```csharp
            foreach(GridNode neighbour in ShuffleNeighbours(currentNode))
            {
```

- [ ] **Step 3: Verify it compiles**

Return to the Unity Editor, let it recompile, check the Console.

Expected: no compile errors referencing `AStar.cs`. In particular, confirm the tuple-swap syntax `(a, b) = (b, a)` compiles under this project's C# language version (Unity 2021+/C# 9 supports it; if the Console reports a syntax error on that line, use the explicit 3-line swap instead: `GridNode tmp = neighbours[i]; neighbours[i] = neighbours[j]; neighbours[j] = tmp;`).

- [ ] **Step 4: Manual verification checkpoint — visual path diversity**

Enter Play Mode. Click a large single-color group (10+ passengers) standing close together in an open area (not a 1-cell-wide corridor) so they all path toward the same bus `targetNode`.

Expected: passengers fan out and take visibly different routes toward the bus — no more single-file line or rigid row/column marching for passengers that have room to spread out. In genuinely narrow 1-cell corridors, passengers may still funnel single-file — that is correct grid geometry, not a bug.

- [ ] **Step 5: Manual verification checkpoint — no regression on reservation/blocking behavior**

While still in Play Mode, repeat the two checkpoints from the original plan (`docs/superpowers/plans/2026-07-27-reservation-grid-plan.md` Task 2 Step 10 and Task 3 Step 4):
- Two crossing groups still avoid occupying the same cell simultaneously, and a blocked passenger still re-paths and continues instead of freezing.
- A large group funneling through a bottleneck still shows closer-to-target passengers claiming cells first (fewer internal fail-paths than before the original plan).

Expected: both still hold — randomized tie-breaking must not reintroduce the deadlock/overlap bugs already fixed. Exit Play Mode when confirmed.

---

## Post-implementation notes

- If passengers still look too uniform after this fix, a possible follow-up (out of scope here, would need a new brainstorming pass) is adding a small random offset to `hCost` itself — but per spec §9 this was explicitly not chosen, to keep path length guarantees simple. Revisit only if the user asks for stronger diversification.
