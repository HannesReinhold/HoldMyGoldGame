using Unity.Netcode;
using UnityEngine;

public class ItemHitDamage : NetworkBehaviour
{
    public int damage = 1;
    public float knockbackForce = 8f;     // Knockback bei voller Aufprallgeschwindigkeit
    public float upwardBoost = 0.4f;
    public float minImpactSpeed = 3f;

    [Header("Skalierung")]
    public float fullKnockbackSpeed = 10f;   // ab dieser Aufprallgeschwindigkeit voller Knockback (z. B. throwForce)
    public float minKnockbackScale = 0.3f;   // untere Grenze, damit schwache Treffer nicht wirkungslos sind

    [Header("Werfer")]
    public float selfImmuneTime = 1f;        // so lange kann der Werfer sein eigenes Item nicht abbekommen

    // Nur Server. Ohne Arm() macht das Item nie Schaden (Drop, Todes-Drop, liegende Items).
    public bool armed;
    public ulong throwerClientId = NetworkedPlayer.NoAttacker;
    public float armTime;


    public void Arm(ulong thrower)
    {
        armed = true;
        throwerClientId = thrower;
        armTime = Time.time;

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || !IsSpawned || !armed) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;

        var player = collision.collider.GetComponentInParent<NetworkedPlayer>();
        if (player == null) return;

        if (player.OwnerClientId == throwerClientId && Time.time - armTime < selfImmuneTime)
            return;

        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;

        float scale = Mathf.Clamp(speed / fullKnockbackSpeed, minKnockbackScale, 1f);
        Vector3 impulse = (dir * knockbackForce + Vector3.up * (knockbackForce * upwardBoost)) * scale;

        player.itemCount.Value += 1;
        player.TakeDamage(damage, impulse, throwerClientId);

        NetworkObject.Despawn(true);
    }
}