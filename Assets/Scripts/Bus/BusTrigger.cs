using UnityEngine;

public class BusTrigger : MonoBehaviour
{
    Bus bus;
    private void Awake()
    {
        bus = GetComponentInParent<Bus>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Passenger passenger = other.GetComponent<Passenger>();

        if (passenger == null) return;

        if (bus.isWildcard)
        {
            if (!BusStation.instance.CanWildcardAccept(passenger.PassengerColor)) return;
        }
        else
        {
            if (passenger.PassengerColor != bus.passengerColor)
                return;
        }

        passenger.EnterBus(bus);
    }
}
