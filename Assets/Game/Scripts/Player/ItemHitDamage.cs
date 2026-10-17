using Unity.Netcode;
using UnityEngine;

public class ItemHitDamage : NetworkBehaviour
{
    public int damage = 1;
    public float knockbackForce = 8f;
    public float upwardBoost = 0.4f;     // Anteil von knockbackForce nach oben
    public float minImpactSpeed = 3f;    // liegende/rollende Items machen keinen Schaden
    public float ignoreAfterSpawn = 0.1f; // Wurf trifft nicht sofort den Werfer

    private float spawnTime;

    public override void OnNetworkSpawn()
    {
        spawnTime = Time.time;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || !IsSpawned) return;
        if (Time.time - spawnTime < ignoreAfterSpawn) return;
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

        var player = collision.collider.GetComponentInParent<NetworkedPlayer>();
        if (player == null) return;

        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;

        Vector3 impulse = dir * knockbackForce + Vector3.up * (knockbackForce * upwardBoost);
        player.itemCount.Value += 1;
        player.TakeDamage(damage, impulse);

        NetworkObject.Despawn(true); // TODO dont despawn with no damage dealt
    }
}