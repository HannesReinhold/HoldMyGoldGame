using Unity.Netcode;
using UnityEngine;

public class ServerManager : NetworkManager
{


    public GameObject playerPrefab;

    public static ServerManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void Host()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void Join()
    {
        NetworkManager.Singleton.StartClient();
    }

    public void Disconnect()
    {
        bool wasHost = NetworkManager.Singleton.IsHost;

        NetworkManager.Singleton.Shutdown();

        Debug.Log(wasHost ? "Host shut down. All clients disconnected." : "Disconnected from host.");
    }
}


