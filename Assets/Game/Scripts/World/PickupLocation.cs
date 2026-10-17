using Unity.Netcode;
using UnityEngine;

public class PickupLocation : HighlightableZone
{
    public int amountPerPickup = 1;
    public float interactRadius = 3f;   // Server-Check der Distanz
    public float cooldown = 2f;         // 0 = unbegrenzt/sofort wieder nutzbar

    private float nextAvailableTime;    // nur Server

    // Nur auf dem Server aufrufen. Gibt die Anzahl zurück, die der Spieler bekommt.
    public int TryTake()
    {
        if (!IsServer) return 0;
        if (Time.time < nextAvailableTime) return 0;

        nextAvailableTime = Time.time + cooldown;
        return amountPerPickup;
    }
}