using FMODUnity;
using Game.Scripts.GameLogic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using FMODUnity;

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
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        ); // TODO set to 0
    public int GoldBarCount => itemCount.Value;
    public int startItems = 0;

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
    [Range(0f, 1f)] public float dropFraction = 1f; // 1 = alles, 0.5 = die H�lfte
    public int maxDroppedItems = 100;                // Obergrenze physischer Items
    public float scatterForce = 2f;
    public float dropRadius = 0.9f;

    public const ulong NoAttacker = ulong.MaxValue;

    // sounds
    [Header("FMOD Jump Sound")]
    [SerializeField] private EventReference jumpEvent;

    [Header("FMOD Continuous Walking Sound")]
    [Tooltip("Looping FMOD event attached to this player.")]
    [SerializeField] private StudioEventEmitter walkingEmitter;

    [Header("FMOD Footsteps")]
    [SerializeField] private EventReference footstepEvent;

    [SerializeField, Min(0.05f)]
    private float fastestStepInterval = 0.22f;

    [SerializeField, Min(0.05f)]
    private float slowestStepInterval = 0.65f;

    [SerializeField, Range(0f, 1f)]
    private float movementInputThreshold = 0.1f;

    [SerializeField, Range(0.1f, 1f)]
    private float fullInputMagnitude = 1f;

    private float footstepTimer;

    // Server-authoritative audio state, replicated to all clients.
    private readonly NetworkVariable<bool> isWalkingNetworked =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private bool localWalkingSoundPlaying;

    private bool controlsBlocked;
    private int hintNumber = 0;


    [Header("FMOD Throw Sound")]
    [SerializeField] private FMODUnity.EventReference throwEvent;
    [Header("FMOD Pickup Sound")]
    [SerializeField] private FMODUnity.EventReference pickupEvent;
    [Header("FMOD Deposit Sound")]
    [SerializeField] private FMODUnity.EventReference depositEvent;


    [Header("FMOD Death Sound")]
    [SerializeField] private EventReference deathSoundEvent;

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
        {
            GameManager.Instance.localPlayer = this;
            GameManager.Instance.ApplyStageToLocalPlayer();

            health.OnValueChanged += onOwnHealthChanged;
            MenuManager.Instance.Hint.enabled = false;
            if (GameManager.Instance != null)
                GameManager.Instance.StageChanged += OnStageChanged;
            itemCount.OnValueChanged += OnOwnItemCountChanged;
        }
        
        // subscribe to walking
        isWalkingNetworked.OnValueChanged += OnWalkingStateChanged;

        footstepTimer = 0f;
        ApplyWalkingSound(isWalkingNetworked.Value);
    }
    
    private void onOwnHealthChanged(int previousValue, int newValue)
    {
        var renderer = VignetteRender.Instance;
        if (renderer != null)
        {
            renderer.SetHealth(newValue, maxHealth);
        }
    }

    private void OnStageChanged(GameStage oldStage, GameStage newStage)
    {
        if (newStage == GameStage.PLAY && oldStage == GameStage.COUNTDOWN)
        {
            MenuManager.Instance.Hint.enabled = true;
            hintNumber = 1;
            MenuManager.Instance.Hint.text = "! Pickup gold using 'E' (take)";
        } else if (newStage == GameStage.JOIN || newStage == GameStage.NONE)
        {
            MenuManager.Instance.Hint.enabled = false;
            hintNumber = 0;
        } 
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            MenuManager.Instance.Hint.enabled = false;
            hintNumber = 0;
            if (GameManager.Instance != null)
                GameManager.Instance.StageChanged -= OnStageChanged;
            itemCount.OnValueChanged -= OnOwnItemCountChanged;
        }

        playerSlot.OnValueChanged -= OnSlotChanged;
        base.OnNetworkDespawn();

        //unsubscribe walking
        isWalkingNetworked.OnValueChanged -= OnWalkingStateChanged;
        ApplyWalkingSound(false);
    }
    
    private void OnOwnItemCountChanged(int previous, int current)
    {
        if (hintNumber != 1) return;

        if (current > previous && current > 0)
        {
            hintNumber = 2;
            MenuManager.Instance.Hint.text = "! Deposit gold at your spawn using 'Q' (drop)";
        }
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

        Debug.LogWarning($"Kein PlayerSpawnPoint f�r Slot {slot}");
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

    public void TakeDamage(int amount, Vector3 knockbackImpulse, ulong attackerClientId = NoAttacker)
    {
        if (!IsServer) return;
        if (health.Value <= 0) return;
        if (Time.time - lastRespawnTime < respawnLockTime) return;
        if (Time.time - lastHitTime < invulnerableTime) return;
        if (attackerClientId != NoAttacker && attackerClientId == OwnerClientId) return;

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
        PlayDeathSoundClientRpc(transform.position);
    }

    private void DropGoldOnDeath()
    {
        if (goldItemPrefab == null) return;

        int amount = Mathf.Min(Mathf.CeilToInt(itemCount.Value * dropFraction), maxDroppedItems);
        if (amount <= 0) return;

        for (int i = 0; i < amount; i++)
        {
            // gleichm��ig auf einem Ring verteilt, leicht zuf�llig versetzt
            float angle = (i / (float)amount) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            // H�he abwechselnd versetzt, damit Nachbarn sich nicht im selben Level �berlappen
            Vector3 pos = transform.position + dir * dropRadius + Vector3.up * (0.6f + (i % 2) * 0.3f);

            NetworkObject item = Instantiate(goldItemPrefab, pos, Quaternion.identity);

            if (item.TryGetComponent(out Rigidbody rb))
            {
                rb.maxDepenetrationVelocity = 1f;   // begrenzt das Herausdr�cken aus �berlappungen
                rb.AddForce(dir * scatterForce, ForceMode.VelocityChange);   // masseunabh�ngig, in m/s
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


    // sounds
    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;
        
        // if (IsServer && Time.time - lastHitTime >= 3f)
        //     TakeDamage(1, Vector3.zero);


        UpdateMovementAudio();
    }

    private void UpdateMovementAudio()
    {
        if (playerMovement == null ||
            !playerMovement.grounded ||
            controlsBlocked ||
            health.Value <= 0)
        {
            SetWalkingStateServerRpc(false);
            footstepTimer = 0f;
            return;
        }

        Vector2 input = playerMovement.GetPlayerMovement();
        float inputMagnitude = Mathf.Clamp01(input.magnitude);

        bool wantsToWalk = inputMagnitude > movementInputThreshold;

        SetWalkingStateServerRpc(wantsToWalk);

        if (!wantsToWalk)
        {
            footstepTimer = 0f;
            return;
        }

        float normalizedInput = Mathf.InverseLerp(
            movementInputThreshold,
            fullInputMagnitude,
            inputMagnitude
        );

        float stepInterval = Mathf.Lerp(
            slowestStepInterval,
            fastestStepInterval,
            normalizedInput * playerMovement.slowdown
        );

        footstepTimer -= Time.deltaTime;

        if (footstepTimer <= 0f)
        {
            PlayFootstepServerRpc();
            footstepTimer = stepInterval;
        }
    }

    [ServerRpc]
    private void SetWalkingStateServerRpc(
    bool walking,
    ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (isWalkingNetworked.Value != walking)
            isWalkingNetworked.Value = walking;
    }

    private void OnWalkingStateChanged(bool previous, bool current)
    {
        ApplyWalkingSound(current);
    }

    private void ApplyWalkingSound(bool shouldPlay)
    {
        if (walkingEmitter == null)
            return;

        if (shouldPlay && !localWalkingSoundPlaying)
        {
            walkingEmitter.Play();
            localWalkingSoundPlaying = true;
        }
        else if (!shouldPlay && localWalkingSoundPlaying)
        {
            walkingEmitter.Stop();
            localWalkingSoundPlaying = false;
        }
    }

    [ServerRpc]
    private void PlayFootstepServerRpc(
    ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        PlayFootstepClientRpc();
    }

    [ClientRpc]
    private void PlayFootstepClientRpc()
    {
        RuntimeManager.PlayOneShot(
            footstepEvent,
            transform.position
        );
    }


    public void PlayJumpSound()
    {
        if (!IsOwner || !IsSpawned)
            return;

        PlayJumpSoundServerRpc();
    }

    [ServerRpc]
    private void PlayJumpSoundServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        PlayJumpSoundClientRpc();
    }

    [ClientRpc]
    private void PlayJumpSoundClientRpc()
    {
        RuntimeManager.PlayOneShot(
            jumpEvent,
            transform.position
        );
    }


    public void PlayThrowSound()
    {
        if (!IsOwner || !IsSpawned)
            return;

        PlayThrowSoundServerRpc();
    }

    [ServerRpc]
    private void PlayThrowSoundServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        PlayThrowSoundClientRpc();
    }

    [ClientRpc]
    private void PlayThrowSoundClientRpc()
    {
        if (throwEvent.IsNull)
            return;

        FMODUnity.RuntimeManager.PlayOneShot(
            throwEvent,
            transform.position
        );
    }


    public void PlayPickupSound()
    {
        if (!IsOwner || !IsSpawned)
            return;

        PlayPickupSoundServerRpc();
    }

    [ServerRpc]
    private void PlayPickupSoundServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        PlayPickupSoundClientRpc();
    }

    [ClientRpc]
    private void PlayPickupSoundClientRpc()
    {
        if (pickupEvent.IsNull)
            return;

        FMODUnity.RuntimeManager.PlayOneShot(
            pickupEvent,
            transform.position
        );
    }

    public void PlayDepositSound()
    {
        if (hintNumber == 2)
        {
            hintNumber = 0;
            MenuManager.Instance.Hint.enabled = false;
        }
        
        if (!IsOwner || !IsSpawned)
            return;

        PlayDepositSoundServerRpc();
    }

    [ServerRpc]
    private void PlayDepositSoundServerRpc(
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        PlayDepositSoundClientRpc();
    }

    [ClientRpc]
    private void PlayDepositSoundClientRpc()
    {
        if (depositEvent.IsNull)
            return;

        FMODUnity.RuntimeManager.PlayOneShot(
            depositEvent,
            transform.position
        );
    }

    public void ResetForRound()
    {
        if (!IsServer) return;

        lastHitTime = -10f;
        lastRespawnTime = Time.time;   // Schutz gegen sofortigen Kill im Respawn
        health.Value = maxHealth;
        itemCount.Value = startItems;
        timesDied = 0;

        RespawnClientRpc(OwnerOnly());   // zurück zum Spawnpunkt
    }

    [ClientRpc]
    private void PlayDeathSoundClientRpc(Vector3 deathPosition)
    {
        if (deathSoundEvent.IsNull)
            return;

        RuntimeManager.PlayOneShot(
            deathSoundEvent,
            deathPosition
        );
    }
}
