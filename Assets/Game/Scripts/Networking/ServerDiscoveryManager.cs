
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class ServerManager : MonoBehaviour
{
    public static ServerManager Instance { get; private set; }

    [SerializeField] private ushort port = 7777;

    private NetworkManager networkManager;
    private UnityTransport transport;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        networkManager = NetworkManager.Singleton;

        if (networkManager != null)
            transport = networkManager.GetComponent<UnityTransport>();
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback
                += OnClientConnected;

            NetworkManager.Singleton.OnClientDisconnectCallback
                += OnClientDisconnected;

            NetworkManager.Singleton.OnTransportFailure
                += OnTransportFailure;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback
                -= OnClientConnected;

            NetworkManager.Singleton.OnClientDisconnectCallback
                -= OnClientDisconnected;

            NetworkManager.Singleton.OnTransportFailure
                -= OnTransportFailure;
        }
    }

    public void Host()
    {
        if (!CanStart())
            return;

        // Listen on all network interfaces.
        transport.SetConnectionData(
            "127.0.0.1",
            port,
            "0.0.0.0"
        );

        if (!networkManager.StartHost())
        {
            Debug.LogError("Failed to start host.");
            return;
        }

        Debug.Log($"Host started on UDP port {port}.");
    }

    public void Join(string hostAddress)
    {
        if (!CanStart())
            return;

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            Debug.LogError("Host IP address is empty.");
            return;
        }

        transport.SetConnectionData(hostAddress.Trim(), port);

        if (!networkManager.StartClient())
        {
            Debug.LogError("Failed to start client.");
            return;
        }

        Debug.Log($"Connecting to {hostAddress}:{port}...");
    }

    public void Disconnect()
    {
        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();
    }

    private bool CanStart()
    {
        if (networkManager == null || transport == null)
        {
            Debug.LogError(
                "NetworkManager or UnityTransport is missing."
            );
            return false;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning("Already in a network session.");
            return false;
        }

        return true;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
    }

    private void OnTransportFailure()
    {
        Debug.LogError("Network transport failure.");
    }
}
