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

        if (IsServer)
        {
            int slot = FindFreeSlot();
            playerSlot.Value = slot;
            Debug.Log($"Slot {slot}");
            /*TeleportToSpawnClientRpc(slot, GetSpawnPosition(slot, out Quaternion rot), rot,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
                });*/
        }

        if (IsOwner) SetEnablePlayerCamera(true);
        playerMovement.SetEnableMovement(IsOwner);
        playerMovement.SetEnablePlayer(IsOwner);


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

    [ClientRpc]
    private void TeleportToSpawnClientRpc(int slot, Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
    {
        // läuft nur beim Owner
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        if (TryGetComponent(out Unity.Netcode.Components.NetworkTransform nt))
            nt.Teleport(position, rotation, transform.localScale);
        else
            transform.SetPositionAndRotation(position, rotation);

        if (cc != null) cc.enabled = true;
    }
}
