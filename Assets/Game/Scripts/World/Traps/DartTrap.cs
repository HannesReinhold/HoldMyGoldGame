using Unity.Netcode;
using UnityEngine;

public class DartTrap : NetworkBehaviour
{
    [Header("Dart")]
    [SerializeField] private NetworkObject dartPrefab;
    [SerializeField] private Transform dartSpawnPoint;
    [SerializeField] private float dartSpeed = 20f;

    public void Fire()
    {
        // Only the server spawns and launches the dart.
        if (!IsServer || !IsSpawned)
            return;

        if (dartPrefab == null || dartSpawnPoint == null)
        {
            Debug.LogWarning(
                $"{name}: Assign a dart prefab and spawn point.",
                this);
            return;
        }

        NetworkObject dart = Instantiate(
            dartPrefab,
            dartSpawnPoint.position,
            dartSpawnPoint.rotation);

        dart.Spawn();

        Rigidbody rb = dart.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity =
                dartSpawnPoint.forward * dartSpeed;
        }
        else
        {
            Debug.LogWarning(
                "Dart prefab needs a Rigidbody.", dart);
        }
    }
}