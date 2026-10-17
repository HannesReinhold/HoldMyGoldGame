using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    private Collider zone;

    private void Awake() => zone = GetComponent<Collider>();

    private void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        // Spieler: Positionsprüfung statt Trigger-Event, weil der CharacterController
        // auf dem Server bei Remote-Spielern evtl. deaktiviert ist
        foreach (var client in nm.ConnectedClientsList)
        {
            var po = client.PlayerObject;
            if (po == null) continue;

            if (zone.bounds.Contains(po.transform.position) &&
                po.TryGetComponent(out NetworkedPlayer player))
            {
                player.Kill();
            }
        }
    }

    // Items, die runterfallen, aufräumen
    private void OnTriggerEnter(Collider other)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var obj = other.GetComponentInParent<NetworkObject>();
        if (obj != null && obj.IsSpawned && obj.GetComponent<NetworkedPlayer>() == null)
            obj.Despawn(true);
    }
}