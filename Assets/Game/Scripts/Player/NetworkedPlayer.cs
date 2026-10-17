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

        if (!IsServer && playerSlot.Value >= 0)
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
            var other = client.PlayerObject.GetComponent<NetworkedPlayerInteraction>();
            if (other != null && playerSlot.Value >= 0)
                used.Add(playerSlot.Value);
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
    }
}
