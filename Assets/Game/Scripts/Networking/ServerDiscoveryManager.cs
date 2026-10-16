using Unity.Netcode;
using UnityEngine;

public class ServerDiscoveryManager : NetworkManager
{


    public GameObject playerPrefab;

    public void Host()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void Join()
    {
        NetworkManager.Singleton.StartClient();
    }

    [Rpc(SendTo.Server)]
    public void SpawnPlayerServerRpc(RpcParams rpcParams = default)
    {
        // Identify the client that sent the RPC
        ulong clientId = rpcParams.Receive.SenderClientId;

        // Prevent spawning multiple player objects for the same client
        if (NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null)
            return;

        // Instantiate the player on the server
        GameObject instance = Instantiate(playerPrefab);

        // Spawn and associate it with the requesting client
        NetworkObject networkObject = instance.GetComponent<NetworkObject>();

        networkObject.SpawnAsPlayerObject(clientId);

        Debug.Log($"Spawned player for client {clientId}");
    }
}


