using Unity.Netcode;
using UnityEngine;

public class NetworkedPlayer : NetworkBehaviour
{
    public Camera playerCamera;
    public AudioListener playerAudioListener;
    public PlayerMovement playerMovement;

    public Behaviour[] localOnlyBehaviours;

    public NetworkVariable<int> playerSlot = new NetworkVariable<int>(
    -1,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);
    public int PlayerSlot => playerSlot.Value;

    [SerializeField] public NetworkVariable<int> health = new NetworkVariable<int>(
    4,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

    // item numbers
    public NetworkVariable<int> itemCount = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        ); // TODO set to 0
    public int GoldBarCount => itemCount.Value;

    public NetworkedPlayerInteraction interaction;


    [SerializeField] public int timesDied = 0;

    [Header("Health")]
    public int maxHealth = 4;
    public float invulnerableTime = 0.5f;   // Schutz gegen Mehrfachtreffer
    public float respawnLockTime = 1f;      // Schutz gegen doppeltes Sterben beim Respawn

    private float lastHitTime = -10f;
    private float lastRespawnTime = -10f;

    [Header("Death Drop")]
    public NetworkObject goldItemPrefab;            // dasselbe Prefab wie beim Werfen
    [Range(0f, 1f)] public float dropFraction = 1f; // 1 = alles, 0.5 = die Hälfte
    public int maxDroppedItems = 100;                // Obergrenze physischer Items
    public float scatterForce = 2f;
    public float dropRadius = 0.9f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerSlot.OnValueChanged += OnSlotChanged;

        if (IsServer)
        {
            int slot = FindFreeSlot();
            playerSlot.Value = slot;
            Debug.Log($"Slot {slot}");
        }

        if (IsOwner && !IsServer && playerSlot.Value >= 0)
            MoveToSpawn(playerSlot.Value);

        SetEnablePlayerCamera(IsOwner);
        playerMovement.SetEnableMovement(IsOwner);
        playerMovement.SetEnablePlayer(IsOwner, IsOwner);


        // Disable other stuff from the other player on this client
        if (localOnlyBehaviours != null)
        {
            foreach (Behaviour behaviour in localOnlyBehaviours)
            {
                if (behaviour != null && behaviour != this)
                    behaviour.enabled = IsOwner;
            }
        }

        if (IsOwner)
            GameManager.Instance.localPlayer = this;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        playerSlot.OnValueChanged -= OnSlotChanged;
        base.OnNetworkDespawn();
    }

    public void SetEnablePlayerCamera(bool enabled)
    {
        playerCamera.enabled=enabled;
        playerAudioListener.enabled=enabled;
    }

    private int FindFreeSlot()
    {
        var used = new System.Collections.Generic.HashSet<int>();

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            if (client.ClientId == OwnerClientId || client.PlayerObject == null) continue;

            var other = client.PlayerObject.GetComponent<NetworkedPlayer>();
            if (other != null && other.playerSlot.Value >= 0)
                used.Add(other.playerSlot.Value);
        }

        int slot = 0;
        while (used.Contains(slot)) slot++;
        return slot;
    }

    private Vector3 GetSpawnPosition(int slot, out Quaternion rotation)
    {
        foreach (var sp in FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None))
        {
            if (sp.slot == slot)
            {
                rotation = sp.transform.rotation;
                return sp.transform.position;
            }
        }

        Debug.LogWarning($"Kein PlayerSpawnPoint für Slot {slot}");
        rotation = transform.rotation;
        return transform.position;
    }

    private void OnSlotChanged(int oldValue, int newValue)
    {
        // Fallback, falls der Slot erst nach dem Spawn ankommt (und Host-Fall)
        if (IsOwner && newValue >= 0)
            MoveToSpawn(newValue);
    }

    private void MoveToSpawn(int slot)
    {
        Vector3 pos = GetSpawnPosition(slot, out Quaternion rot);

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();

        playerMovement.ResetMotion();
        playerMovement.ResetLook();
    }

    public void TakeDamage(int amount, Vector3 knockbackImpulse)
    {
        if (!IsServer) return;
        if (health.Value <= 0) return;
        if (Time.time - lastRespawnTime < respawnLockTime) return;
        if (Time.time - lastHitTime < invulnerableTime) return;

        lastHitTime = Time.time;
        health.Value = Mathf.Max(0, health.Value - amount);

        if (health.Value <= 0)
        {
            DropGoldOnDeath();
            Kill();
            return;
        }

        KnockbackClientRpc(knockbackImpulse, OwnerOnly());
    }

    public void Kill()
    {
        if (!IsServer) return;
        if (Time.time - lastRespawnTime < respawnLockTime) return;

        lastRespawnTime = Time.time;
        timesDied++;
        health.Value = maxHealth;
        itemCount.Value = 0;

        RespawnClientRpc(OwnerOnly());
    }

    private void DropGoldOnDeath()
    {
        if (goldItemPrefab == null) return;

        int amount = Mathf.Min(Mathf.CeilToInt(itemCount.Value * dropFraction), maxDroppedItems);
        if (amount <= 0) return;

        for (int i = 0; i < amount; i++)
        {
            // gleichmäßig auf einem Ring verteilt, leicht zufällig versetzt
            float angle = (i / (float)amount) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            // Höhe abwechselnd versetzt, damit Nachbarn sich nicht im selben Level überlappen
            Vector3 pos = transform.position + dir * dropRadius + Vector3.up * (0.6f + (i % 2) * 0.3f);

            NetworkObject item = Instantiate(goldItemPrefab, pos, Quaternion.identity);

            if (item.TryGetComponent(out Rigidbody rb))
            {
                rb.maxDepenetrationVelocity = 1f;   // begrenzt das Herausdrücken aus Überlappungen
                rb.AddForce(dir * scatterForce, ForceMode.VelocityChange);   // masseunabhängig, in m/s
            }

            item.Spawn();
        }
    }

    public void SetControlsBlocked(bool blocked)
    {
        if (!IsOwner) return;

        playerMovement.SetInputBlocked(blocked);

        if (interaction != null)
            interaction.enableInteractions = !blocked;
    }

    private ClientRpcParams OwnerOnly() => new ClientRpcParams
    {
        Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
    };

    [ClientRpc]
    private void KnockbackClientRpc(Vector3 impulse, ClientRpcParams rpcParams = default)
    {
        playerMovement.AddKnockback(impulse);
    }

    [ClientRpc]
    private void RespawnClientRpc(ClientRpcParams rpcParams = default)
    {
        MoveToSpawn(playerSlot.Value);
        playerMovement.ResetMotion();
    }
}
