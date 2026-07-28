# Reservation Grid cho Passenger Pathfinding — Design Spec

**Ngày:** 2026-07-27
**Trạng thái:** Đã duyệt bởi user, chờ viết implementation plan
**Phạm vi:** Chỉ thiết kế/plan. Không sửa code trong giai đoạn brainstorming này — implementation do user (hoặc phiên làm việc khác) thực hiện dựa trên plan.

## 1. Bối cảnh & vấn đề

Game passenger/bus kiểu Bus Jam, dùng grid A* pathfinding (`Assets/Scripts/Other/AStar.cs`) trên `GridNode` (`Assets/Scripts/Other/GridNode.cs`), điều khiển bởi `Passenger.cs`. Hệ thống đã có mầm mống "reservation grid" (`GridNode.reservedBy`) nhưng cách dùng hiện tại có 3 lỗi khiến đường đi của các passenger vẫn chồng lấn/kẹt nhau:

1. **Không giải phóng path cũ khi tìm path mới.** `Passenger.FindPath()` (Passenger.cs:136-166) set `path = null` rồi tìm path mới, nhưng không clear `reservedBy` trên các node của path cũ chưa đi tới → các ô đó bị khóa "rác" vĩnh viễn, chặn passenger khác vô lý.

2. **Reserve toàn bộ path cùng lúc, không có yếu tố thời gian/khoảng cách.** `AStar.FindPath()` (AStar.cs:53-56) khóa hết các ô trên đường đi ngay khi tìm ra, kể cả ô cách xa hàng chục bước — trong khi passenger còn lâu mới tới đó. Việc này chặn oan các passenger khác cần đi ngang qua ô đó trước khi passenger gốc kịp tới.

3. **Race trong `MoveGroup`.** `Passenger.MoveGroup()` (Passenger.cs:92-119) cho các passenger trong nhóm tìm path tuần tự theo thứ tự được truyền vào (thứ tự này đến từ flood-fill trong `PassengerSelector`, không phản ánh khoảng cách tới đích) → ai tìm trước chiếm ô trước một cách tùy tiện, khiến passenger tìm sau dễ fail path hoặc phải đi vòng không cần thiết dù thực tế lẽ ra nên nhường nhau theo thứ tự hợp lý.

## 2. Mục tiêu

Xây dựng cơ chế **Group Reservation + Sliding Reservation**: mỗi passenger chỉ giữ trước một cửa sổ nhỏ các ô sắp đi tới (không giữ cả path), nhóm di chuyển ưu tiên theo khoảng cách tới đích, và passenger tự động re-path khi phát hiện đường đi phía trước bị chặn.

## 3. Quyết định thiết kế (đã chốt cùng user)

| Tham số | Giá trị đã chọn | Lý do |
|---|---|---|
| Kích thước sliding window | **4 ô** tính từ ô hiện tại | Đủ buffer cho di chuyển mượt qua RVO mà không khóa oan các ô xa |
| Thứ tự ưu tiên trong `MoveGroup` | **Khoảng cách Manhattan tới đích (bus target), gần hơn ưu tiên trước** | Người đi đường ngắn hơn (thường ra bus trước) được giữ ô trước, giảm khả năng người đi xa phải vòng qua người đi gần |
| Xử lý khi bị chặn giữa đường (ô trong window bị passenger khác giữ) | **Tự động tìm lại đường (re-path) ngay lập tức** | Ưu tiên phản ứng nhanh, chấp nhận chi phí tính A* lại khi xảy ra va chạm hiếm |
| Cấu trúc `GridNode` | Giữ nguyên (`reservedBy: Passenger`, không cần bảng reservation theo thời gian) | Sliding window + re-path tức thời đã đủ giải quyết va chạm mà không cần độ phức tạp time-indexed reservation table |

## 4. Kiến trúc

### 4.1 Sliding Reservation Window (thay cho reserve toàn path)

- `AStar.FindPath()` **không còn** tự reserve path khi trả về (bỏ đoạn `foreach(GridNode node in path) node.reservedBy = currentPassenger;` ở AStar.cs:53-56). A* chỉ có trách nhiệm tìm đường, không quản lý reservation.
- `Passenger` sở hữu logic reservation:
  - Hằng số `ReservationWindow = 4`.
  - Tập hợp `reservedWindow` (các `GridNode` đang bị chính passenger này giữ).
  - `ReserveWindow()`: tính đoạn `[pathIndex, pathIndex + ReservationWindow - 1]` (clip theo `path.Count`), reserve các ô còn trống hoặc đã là của mình; giải phóng các ô trước đó không còn nằm trong window.
  - Gọi `ReserveWindow()` mỗi khi `pathIndex` tăng (mỗi lần bước sang ô mới trong `Update()`).
  - `ReleaseAllReservations()`: clear toàn bộ `reservedWindow` — gọi khi: tìm path mới (trước khi gọi A*), path fail, `EnterBus()`.

### 4.2 Phát hiện & xử lý block giữa đường

- Mỗi frame trong `Update()` khi đang di chuyển: `IsWindowBlocked()` kiểm tra các ô còn lại trong window hiện tại — nếu có ô nào `reservedBy != null && reservedBy != this` → coi là bị chặn.
- Khi bị chặn: gọi `RequestPathTo(finalTarget)` — một method dùng chung thay cho việc gọi rời rạc `FindPath()`/`MoveToTargetNode()` hiện tại. `RequestPathTo` luôn `ReleaseAllReservations()` trước khi gọi `AStar.FindPath` để tìm đường mới từ vị trí hiện tại tới cùng đích, né các ô đang bị giữ.
- `finalTarget` (bus targetNode hoặc node đích của `MoveToTargetNode`) được lưu lại trên passenger để có thể re-path nhiều lần tới cùng một đích.
- Đây cũng là cách khắc phục vấn đề #1 (path cũ không giải phóng) — vì mọi đường vào tìm path mới đều đi qua `RequestPathTo`, đảm bảo `ReleaseAllReservations()` luôn chạy trước.

### 4.3 Ưu tiên trong `MoveGroup`

- Trước khi từng passenger gọi tìm path tuần tự (bước 2 trong `MoveGroup`, Passenger.cs:104-107), sắp xếp `group` theo khoảng cách Manhattan tới đích tăng dần:
  ```csharp
  group.Sort((a, b) => a.GetDistanceToTarget().CompareTo(b.GetDistanceToTarget()));
  ```
- `GetDistanceToTarget()`: helper mới trên `Passenger`, tái sử dụng phần tìm bus cùng màu hiện đang lặp lại trong `FindPath()` — refactor thành `FindMatchingBusTargetNode()` dùng chung bởi cả `RequestPathTo` (khi target là bus) và `GetDistanceToTarget()`.
- `AStar.GetDistance()` (hiện `private`, AStar.cs:108-114) đổi thành `public static` (hoặc thêm overload public) để tái sử dụng làm khoảng cách heuristic Manhattan ở `GetDistanceToTarget()`, tránh trùng lặp logic.

### 4.4 Không đổi

- Cấu trúc `GridNode` (row/column/worldPosition/walkable/occupant/reservedBy/neighbours/costs) giữ nguyên hoàn toàn.
- RVO system (`RVOAgent`, `RVOSimulator`) — né va chạm hình học cục bộ — không đụng tới.
- Cơ chế mở/khóa `walkable` quanh bước 1 và bước 3 của `MoveGroup` giữ nguyên.
- `PassengerGrid`, `PassengerManager`, `PassengerSpawnAreas`, `PassengerSelector`, `BusStation` — không thay đổi cấu trúc, chỉ `Passenger`/`AStar` bị ảnh hưởng.

## 5. Các thay đổi cụ thể theo file (tham khảo cho implementation plan)

| File | Thay đổi |
|---|---|
| `Assets/Scripts/Other/AStar.cs` | Bỏ reserve-toàn-path trong `FindPath()`. Đổi `GetDistance()` thành `public static`. |
| `Assets/Scripts/Passenger/Passenger.cs` | Thêm `ReservationWindow`, `reservedWindow`, `ReserveWindow()`, `ReleaseAllReservations()`, `IsWindowBlocked()`, `RequestPathTo()`, `FindMatchingBusTargetNode()`, `GetDistanceToTarget()`. Sửa `Update()` để gọi `ReserveWindow()`/`IsWindowBlocked()`. Sửa `FindPath()`, `MoveToTargetNode()` để dùng `RequestPathTo()`. Sửa `MoveGroup()` để sort theo `GetDistanceToTarget()` trước bước 2. Sửa `EnterBus()` để gọi `ReleaseAllReservations()`. |
| `Assets/Scripts/Other/GridNode.cs` | Không đổi. |

## 6. Kiểm thử (thủ công, Play mode — dự án không có test tự động cho gameplay này)

1. Spawn nhiều passenger cùng màu, chọn nhóm lớn băng qua nhau → xác nhận không có 2 passenger cùng đứng chung 1 ô tại bất kỳ thời điểm nào.
2. Dựng tình huống 2 nhóm khác màu cắt đường nhau giữa lúc đang di chuyển → xác nhận passenger bị chặn tự động re-path, không đứng khựng vĩnh viễn.
3. Test nhóm đông đi qua hành lang hẹp (thắt cổ chai) hướng về cùng 1 bus → xác nhận thứ tự ưu tiên theo khoảng cách làm giảm số lần fail-path nội bộ nhóm so với hiện tại.
4. Kiểm tra không còn hiện tượng ô bị khóa "rác" (reservedBy trỏ tới passenger đã lên bus hoặc đã đổi hướng) sau nhiều lượt di chuyển liên tiếp.

## 7. Rủi ro / giới hạn đã biết

- Re-path ngay lập tức khi bị chặn có thể gây nhiều lệnh gọi `AStar.FindPath` liên tiếp nếu nhiều passenger cùng chặn nhau trong khu vực đông đúc — chấp nhận được ở quy mô hiện tại của game, nhưng nếu số lượng passenger tăng mạnh trong tương lai có thể cần throttle (không thuộc phạm vi thiết kế này).
- Sliding window 4 ô là giá trị cố định, không tự động điều chỉnh theo tốc độ agent (RVOAgent.maxSpeed) — đã được user xác nhận là đủ dùng, không cần làm động.

## 8. Bug fix sau triển khai — `ReserveWindow()` không bao giờ thực sự set `reservedBy`

**Ngày phát hiện:** 2026-07-27 (cùng ngày, sau khi user tự tay implement theo plan và test thấy passenger vẫn chồng lấn).

**Root cause:** đoạn "chiếm ô mới" trong `ReserveWindow()` (Passenger.cs, được user gõ tay theo plan) bị gõ nhầm thành:

```csharp
foreach(GridNode node in reservedWindow)   // sai: phải là `desired`
{
    if(node.reservedBy == null && node.reservedBy == this)   // sai: phải là `||`, và không thể vừa null vừa == this
    {
        node.reservedBy = this;
    }
}
```

Điều kiện `reservedBy == null && reservedBy == this` không bao giờ đúng (một reference không thể vừa null vừa bằng `this`), nên `reservedBy` không bao giờ được gán cho bất kỳ passenger nào trong suốt vòng đời game. Hệ quả: `IsWindowBlocked()` luôn trả `false`, và điều kiện né `reservedBy` trong `AStar.FindPath()` (AStar.cs:60) không bao giờ kích hoạt — toàn bộ cơ chế sliding-window reservation từ mục 4.1/4.2 là no-op hoàn toàn, việc ngăn chồng lấn chỉ còn dựa vào `walkable`/`occupant` của đúng ô đang đứng.

**Fix đã áp dụng** (đúng theo thiết kế gốc mục 4.1, user đã tự sửa):

```csharp
foreach(GridNode node in desired)
{
    if(node.reservedBy == null || node.reservedBy == this)
    {
        node.reservedBy = this;
    }
}
```

## 9. Tính năng bổ sung — Random tie-breaking trong A* để path khác nhau tự nhiên trong nhóm

**Yêu cầu mới của user (sau khi fix mục 8):** khi nhấn chọn một nhóm (`MoveGroup`), tất cả passenger di chuyển cùng lúc nhưng path của mỗi người nên khác nhau một cách tự nhiên, thay vì đi theo đúng 1 tuyến/hàng dọc-ngang giống hệt nhau.

**Vấn đề:** `AStar.FindPath()` là thuật toán xác định — với cùng `startNode`/`targetNode` và chi phí ô đồng nhất, thứ tự duyệt neighbour luôn cố định (`Up, Down, Left, Right`, xem `GridNode.GetNeighbours()`), nên nhiều passenger đứng gần nhau cùng đi tới cùng `targetNode` của bus sẽ luôn tính ra path giống hệt hoặc gần giống hệt nhau, tạo cảm giác đi theo "đội hình" cứng nhắc thay vì tự nhiên.

**Giải pháp đã chọn (đã trao đổi với user — chọn phương án khuyến nghị, không chọn phương án đích khác nhau quanh bus hay kết hợp cả hai):** thêm nhiễu ngẫu nhiên vào bước tie-breaking của A*, không đổi cách tính cost:

- Trong `AStar.FindPath()`, ngay trước `foreach(GridNode neighbour in currentNode.GetNeighbours())`, copy các neighbour ra một `List<GridNode>` rồi xáo trộn ngẫu nhiên (Fisher-Yates, dùng `UnityEngine.Random.Range`) trước khi duyệt.
- Việc xáo trộn chỉ ảnh hưởng tới **thứ tự neighbour được thêm vào `openList` khi có nhiều lựa chọn cùng `fCost`** — không đổi cách tính `gCost`/`hCost`, nên độ dài đường đi tìm được vẫn tối ưu hoặc gần-tối-ưu như cũ (A* vẫn ưu tiên đúng theo `fCost`/`hCost` khi so sánh ở dòng 36-42; xáo trộn chỉ đổi thứ tự các neighbour có cùng chi phí được xét tới, không đổi kết quả so sánh cost).
- Không cần state/field mới ở `Passenger` hay `GridNode`. Mỗi lần gọi `FindPath()` tự shuffle độc lập bằng `UnityEngine.Random`, nhất quán với cách `Passenger.Update()` đã dùng `Random.Range` cho tốc độ di chuyển.
- **Đánh đổi đã chấp nhận:** khi có nhiều đường cùng độ dài tối ưu, đường được chọn sẽ ngẫu nhiên giữa các nhánh đó thay vì luôn chọn nhánh cố định theo thứ tự Up/Down/Left/Right — đây chính là mục tiêu (path khác nhau tự nhiên giữa các passenger).
- **Không đổi:** không đụng tới cách bus nhận khách (`EnterBus`, `targetNode`), không đổi cấu trúc `GridNode`, không đổi reservation window (mục 4.1) hay priority ordering (mục 4.3).

## 10. Tính năng bổ sung — Cost Jitter (chấp nhận đường vòng, không cần tối ưu)

**Yêu cầu mới của user (sau khi thấy tie-breaking ở mục 9 chưa đủ "lộn xộn"):** không cần đường đi ngắn nhất nữa, chỉ cần tới đích — muốn passenger di chuyển lộn xộn/tự nhiên hơn nữa, kể cả nếu đường dài hơn tối ưu.

**Quyết định đã chốt cùng user:**

| Tham số | Giá trị đã chọn | Lý do |
|---|---|---|
| Cơ chế | Cộng nhiễu ngẫu nhiên vào **chi phí cạnh** (`newCost`), không đổi thuật toán A* nền tảng | Vẫn dùng đúng vòng lặp `openList`/`closedList` hiện có, đảm bảo thuật toán vẫn kết thúc hữu hạn và luôn tìm được đường nếu tồn tại — chỉ khác là đường tìm được không còn tối ưu |
| Nơi cấu hình | `[SerializeField] [Range(0, 20)] int costJitter` public trên `AStar`, chỉnh trong Inspector | User (designer) muốn tự tune độ "lộn xộn" ngay trong Unity mà không cần sửa code/build lại |
| Giá trị mặc định | `6` | Đủ để tạo đường vòng rõ rệt nhưng không quá cực đoan; `costJitter = 0` cho phép quay lại hành vi A* tối ưu như cũ để so sánh/debug |

**Kiến trúc:**

- Trong `AStar.FindPath()`, dòng tính `newCost`:
  ```csharp
  int newCost = currentNode.gCost + GetDistance(currentNode, neighbour);
  ```
  đổi thành:
  ```csharp
  int newCost = currentNode.gCost + GetDistance(currentNode, neighbour) + Random.Range(0, costJitter + 1);
  ```
- Vì mọi cạnh đều được cộng thêm nhiễu ngẫu nhiên độc lập, `gCost` tích lũy không còn phản ánh đúng khoảng cách thật — A* vẫn chọn đường có `gCost` tích lũy nhỏ nhất theo đúng logic hiện có (dòng 36-42 không đổi), nhưng "nhỏ nhất theo gCost đã nhiễu" không còn trùng với "ngắn nhất theo số ô thật" → đường đi có thể vòng vèo tùy theo `costJitter`.
- Giữ nguyên `ShuffleNeighbours` đã thêm ở mục 9 (không xung đột — cả hai cơ chế cộng hưởng: shuffle quyết định thứ tự duyệt khi tie, jitter quyết định việc có tie hay không và mức độ lệch khỏi tối ưu).
- **Không đổi:** cấu trúc `GridNode`, reservation window (mục 4.1), priority ordering trong `MoveGroup` (mục 4.3), cách bus nhận khách.
- **Rủi ro đã cân nhắc:** `costJitter` lớn có thể khiến passenger đi vòng xa hơn (tốn nhiều frame di chuyển hơn để tới đích), nhưng không có nguy cơ treo/vô hạn vì `closedList` đảm bảo mỗi ô chỉ được xử lý "đóng" một lần — thuật toán vẫn kết thúc hữu hạn như A* gốc.
