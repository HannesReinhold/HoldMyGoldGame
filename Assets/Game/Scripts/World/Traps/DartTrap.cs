using Unity.Netcode;
using UnityEngine;
using FMODUnity;

public class DartTrap : NetworkBehaviour
{
    [Header("Dart")]
    [SerializeField] private NetworkObject dartPrefab;
    [SerializeField] private Transform dartSpawnPoint;
    [SerializeField] private float dartSpeed = 20f;

    [Header("FMOD Sound")]
    [SerializeField] private EventReference shootSoundEvent;

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

        ItemHitDamage hitDamage =
            dart.GetComponent<ItemHitDamage>();

        if (hitDamage != null)
        {
            hitDamage.Arm(ulong.MaxValue);
        }

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

        // Play the shooting sound on every client, including the host.
        PlayShootSoundClientRpc(dartSpawnPoint.position);
    }

    [ClientRpc]
    private void PlayShootSoundClientRpc(Vector3 soundPosition)
    {
        if (shootSoundEvent.IsNull)
            return;

        RuntimeManager.PlayOneShot(
            shootSoundEvent,
            soundPosition);
    }
}