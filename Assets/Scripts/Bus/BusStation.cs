
using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements.Experimental;

public class BusStation : MonoBehaviour
{
    public static BusStation instance;

    public BusLane[] busLanes;

    [Header("Spawn")]
    [SerializeField] public Bus busPrefab;

    // The list of car in parking 
    [HideInInspector] public List<Bus> parkingBusList = new List<Bus>();

    [HideInInspector] public Bus wildcardBus;

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        StartCoroutine(InitializeBusesRoutine());
    }

    public void RegisterWildcardBus(Bus bus)
    {
        wildcardBus = bus;
    }

    public void UnRegisterWildcardBus(Bus bus)
    {
        if (wildcardBus == bus)
        {
            wildcardBus = null;
        }
    }
    public bool CanWildcardAccept(PassengerColor color)
    {
        if(wildcardBus == null || wildcardBus.GetAvailableSeat() <= 0)
        {
            return false;
        }

        foreach(var normalBus in parkingBusList)
        {
            if(normalBus == wildcardBus) continue;

            if (normalBus.passengerColor == color) return false;
        }

        return true;
    }

    public Bus GetWildcardBusIfAcceptable(PassengerColor color)
    {
        return CanWildcardAccept(color) ? wildcardBus : null;
    }

    IEnumerator InitializeBusesRoutine()
    {
        // Cho mot frame de tat ca Passenger da spawn and register xong truoc khi dem so luong
        yield return null;

        List<BusTicket> ticketQueue = BuiltBusTicketQueue();

        int laneCount = busLanes.Length;
        int ticketIndex = 0;

        // Nếu bus đầu tiên trong garage trùng màu với bus parking
        // thì đổi với lane khác nếu có thể.
        for (int i = 0; i < laneCount; i++)
        {
            int garageIndex = laneCount + i;

            if (garageIndex >= ticketQueue.Count)
                break;

            if (ticketQueue[i].passengerColor != ticketQueue[garageIndex].passengerColor)
                continue;

            for (int j = garageIndex + 1; j < ticketQueue.Count; j++)
            {
                if (ticketQueue[j].passengerColor != ticketQueue[i].passengerColor)
                {
                    (ticketQueue[garageIndex], ticketQueue[j]) =
                        (ticketQueue[j], ticketQueue[garageIndex]);

                    break;
                }
            }
        }

        // Pha 1 : spawn tai vi tri parking

        for (int i = 0; i < laneCount && ticketIndex < ticketQueue.Count; i++)
        {
            BusLane lane = busLanes[i];

            Bus bus = SpawnBus(ticketQueue[ticketIndex], lane.parkingPosition.position, lane.transform);

            lane.SetParkingBus(bus);
            RegisterParkingBus(bus);

            ticketIndex++;
        }

        // Pha 2 : Spawn bus con lai chia deu (round-robin) vao garage cua tung lane

        int[] garaFillIndex = new int[laneCount];

        while(ticketIndex < ticketQueue.Count)
        {
            bool spawnedAny = false;

            for (int i = 0; i < laneCount && ticketIndex < ticketQueue.Count; i++)
            {
                BusLane lane = busLanes[i];

                if (garaFillIndex[i] >= lane.garagePositions.Count) continue;

                Transform slot = lane.garagePositions[garaFillIndex[i]];

                Bus bus = SpawnBus(ticketQueue[ticketIndex],slot.position,lane.transform);

                lane.garageBuses.Add(bus);

                garaFillIndex[i]++;

                ticketIndex++;

                spawnedAny = true;
            }

            if (!spawnedAny) break;
        }

        if(ticketIndex < ticketQueue.Count)
        {
            Debug.LogWarning($"[BusStattion] ko du slot,con {ticketQueue.Count - ticketIndex} bus chu duoc spawn");
        }
    }

    /// <summary>
    /// Tinh so bus can cho moi mau (ceil(count/capacity)), roi tron round-robin giua cac mau de pha 1(bus dau tine moi lane) ko bi trung mau
    /// </summary>
    /// <returns></returns>
    struct BusTicket
    {
        public PassengerColor passengerColor;
        public int capacity;
    }

    List<BusTicket> BuiltBusTicketQueue()
    {
        List<PassengerColor> colors = PassengerManager.Instance.GetColorsOrderedByOuterness();
            
        colors = colors.OrderByDescending(c => PassengerManager.Instance.GetCountByColor(c)).ToList();

        List<Queue<BusTicket>> perColorQueues = new List<Queue<BusTicket>>();

        foreach(PassengerColor color in colors)
        {
            int count = PassengerManager.Instance.GetCountByColor(color);
            int cap = busPrefab.capacityPerBus;
            int busesNeeded = Mathf.CeilToInt((float)count / cap);

            Queue<BusTicket> q = new Queue<BusTicket>();

            for (int i = 0; i < busesNeeded; i++)
            {
                bool isLastBus = (i == busesNeeded - 1);

                // Bus cuoi cung se chi nhan phan du
                int remainder = count - i * cap;
                int ticketCapacity = isLastBus ? remainder : cap;

                q.Enqueue(new BusTicket { passengerColor = color, capacity = ticketCapacity });
            }

            perColorQueues.Add(q);
        }

        List<BusTicket> parkingTickets = new();
        List<BusTicket> garageTickets = new();

        // Bus dau tien cua moi mau -> parking

        foreach (Queue<BusTicket> q in perColorQueues)
        {
            if(q.Count > 0)
            {
                parkingTickets.Add(q.Dequeue());
            }
        }

        //Cac bus con lai -> garage
        bool addedAny = true;
        while (addedAny)
        {
            addedAny = false;

            foreach (Queue<BusTicket> q in perColorQueues) // perColorQueues đã sort desc theo count ở bước trên
            {
                if (q.Count > 0)
                {
                    garageTickets.Add(q.Dequeue());
                    addedAny = true;
                }
            }
        }

        parkingTickets.AddRange(garageTickets);

        return parkingTickets;

    }

    Bus SpawnBus(BusTicket ticket,Vector3 position,Transform parent)
    {
        // parent = lane.transform de Bus.GetComponentInParent<BusLane>() hoat dong dung
        Bus bus = Instantiate(busPrefab, position, Quaternion.identity, parent);

        bus.SetColor(ticket.passengerColor);
        bus.SetCapacity(ticket.capacity);

        return bus;
    }

    void Shuffle<T>(List<T> list)
    {
        for(int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void RegisterParkingBus(Bus bus)
    {
        if (!parkingBusList.Contains(bus))
        {
            parkingBusList.Add(bus);
            bus.targetNode = PassengerGrid.Instance.GetNearestNode(bus.transform.position);
        }
    }

    public void UnRegisterParkingBus(Bus bus)
    {
        parkingBusList.Remove(bus);
    }

    public Bus GetNearestBusByColor(PassengerColor color, Vector3 fromPosition)
    {
        Bus best = null;

        float bestSqrtDist = float.MaxValue;

        foreach(var bus in parkingBusList)
        {
            if(bus.passengerColor != color || bus.GetAvailableSeat() <= 0) continue;

            float sqrtDist = (bus.transform.position - fromPosition).sqrMagnitude;

            if(sqrtDist < bestSqrtDist)
            {
                bestSqrtDist = sqrtDist;
                best = bus;
            }
        }

        return best;
    }

    /// <summary>
    /// Find the lane have same color with parking bus and have empty seat
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    public Bus GetBusByColor(PassengerColor color)
    {
        foreach (var bus in parkingBusList)
        {
            if (bus.passengerColor == color && bus.GetAvailableSeat() > 0)
            {
                return bus;
            }
        }

        return null;
    }
    /// <summary>
    /// Get the number of available spaces in the parking lane of the corresponding color.
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>

    public int GetAvailableSeatByColor(PassengerColor color)
    {
        Bus bus = GetBusByColor(color);

        return bus == null ? 0 : bus.GetAvailableSeat();
    }


    public void OnPassengerBoardedColor(PassengerColor color)
    {
        if (color == null) return;

        int remaining = PassengerManager.Instance.GetCountByColor(color);

        int committed = 0;

        foreach (var bus in parkingBusList)
        {
            if (bus != null && !bus.isWildcard && bus.passengerColor == color)
                committed += bus.GetAvailableSeat();
        }

        foreach (var lane in busLanes)
        {
            foreach (var bus in lane.garageBuses)
            {
                if (bus != null && bus.passengerColor == color)
                    committed += bus.GetAvailableSeat();
            }
        }

        int excess = committed - remaining;
        if (excess <= 0) return;

        // Cắt từ CUỐI hàng garage trước - bus này chưa ai đang hướng tới, hủy an toàn nhất.
        // QUAN TRỌNG: chỉ hủy NGUYÊN bus khi toàn bộ ghế trống của nó đều là dư thừa
        // (seat <= excess). Nếu seat > excess, nghĩa là bus này vẫn cần thiết để chở
        // phần passenger còn lại -> chỉ GIẢM capacity đúng bằng phần dư, không phá cả bus.
        foreach (var lane in busLanes)
        {
            for (int i = lane.garageBuses.Count - 1; i >= 1 && excess > 0; i--)
            {
                Bus bus = lane.garageBuses[i];
                if (bus == null || bus.passengerColor != color) continue;

                int seat = bus.GetAvailableSeat();

                if (seat <= excess)
                {
                    // Toàn bộ bus này là dư thừa -> xóa hẳn
                    lane.garageBuses.RemoveAt(i);
                    Destroy(bus.gameObject);
                    excess -= seat;
                }
                else
                {
                    // Chỉ một phần capacity là dư thừa -> giảm capacity, GIỮ LẠI bus
                    // vì nó vẫn cần chở (seat - excess) passenger còn lại của màu này
                    bus.SetCapacity(seat - excess);
                    excess = 0;
                }
            }

            lane.RefreshGaragePositions();
        }

        // Nếu vẫn còn dư VÀ remaining đã thật sự về 0 -> ép bus đang đậu (nếu có) rời sớm
        if (excess > 0 && remaining == 0)
        {
            foreach (var bus in parkingBusList.ToList())
            {
                if (bus != null && !bus.isWildcard && bus.passengerColor == color && bus.GetAvailableSeat() > 0)
                {
                    bus.ForceToLeaveEarly();
                }
            }
        }
    }
}
