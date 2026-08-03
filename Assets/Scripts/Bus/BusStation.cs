
using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BusStation : MonoBehaviour
{
    public static BusStation instance;

    public BusLane[] busLanes;

    [Header("Spawn")]
    [SerializeField] public Bus busPrefab;

    // The list of car in parking 
    [HideInInspector] public List<Bus> parkingBusList = new List<Bus>();

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        StartCoroutine(InitializeBusesRoutine());
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
}
