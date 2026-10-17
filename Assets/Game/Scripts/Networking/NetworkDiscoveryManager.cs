
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class ServerDiscoveryManager : MonoBehaviour
{
    public static ServerDiscoveryManager Instance { get; private set; }

    [Header("Ports")]
    [SerializeField] private ushort gamePort = 7777;
    [SerializeField] private ushort discoveryPort = 47778;

    private NetworkManager networkManager;
    private UnityTransport transport;
    private UdpClient discoverySocket;

    private bool isHosting;
    private bool isSearching;
    private float nextSearchTime;

    public string DiscoveredHostAddress { get; private set; }

    public event Action<string> OnHostDiscovered;

    private const string DiscoveryRequest = "FIND_MY_GAME";
    private const string DiscoveryResponse = "MY_GAME";

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

    public void Host()
    {
        if (!CanStart())
            return;

        try
        {
            // Host listens for discovery requests on the LAN.
            discoverySocket = new UdpClient(discoveryPort);
            discoverySocket.EnableBroadcast = true;
            discoverySocket.Client.Blocking = false;

            isHosting = true;

            // Listen on all local network interfaces.
            transport.SetConnectionData(
                "127.0.0.1",
                gamePort,
                "0.0.0.0"
            );

            if (!networkManager.StartHost())
            {
                Debug.LogError("Failed to start host.");
                StopDiscovery();
                return;
            }

            Debug.Log(
                $"Host started. Discovery port: {discoveryPort}, " +
                $"game port: {gamePort}"
            );
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not start host discovery: {e.Message}");
            StopDiscovery();
        }
    }

    public void StartSearching()
    {
        if (networkManager == null || transport == null)
        {
            Debug.LogError("NetworkManager or UnityTransport is missing.");
            return;
        }

        StopDiscovery();

        try
        {
            // Use an ephemeral local port to receive host replies.
            discoverySocket = new UdpClient(0);
            discoverySocket.EnableBroadcast = true;
            discoverySocket.Client.Blocking = false;

            DiscoveredHostAddress = null;
            isSearching = true;
            nextSearchTime = 0f;

            Debug.Log("Searching for LAN hosts...");
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not start discovery: {e.Message}");
            StopDiscovery();
        }
    }

    private void Update()
    {
        if (discoverySocket == null)
            return;

        try
        {
            // Clients repeatedly broadcast discovery requests.
            if (isSearching && Time.unscaledTime >= nextSearchTime)
            {
                byte[] request = Encoding.UTF8.GetBytes(DiscoveryRequest);

                discoverySocket.Send(
                    request,
                    request.Length,
                    new IPEndPoint(IPAddress.Broadcast, discoveryPort)
                );

                nextSearchTime = Time.unscaledTime + 1f;
            }

            // Process incoming requests or host responses.
            while (discoverySocket != null &&
                   discoverySocket.Available > 0)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = discoverySocket.Receive(ref remote);
                string message = Encoding.UTF8.GetString(data);

                if (isHosting && message == DiscoveryRequest)
                {
                    string response = $"{DiscoveryResponse}|{gamePort}";
                    byte[] responseData = Encoding.UTF8.GetBytes(response);

                    discoverySocket.Send(
                        responseData,
                        responseData.Length,
                        remote
                    );
                }
                else if (isSearching &&
                         message.StartsWith(DiscoveryResponse + "|"))
                {
                    string[] parts = message.Split('|');

                    if (parts.Length != 2 ||
                        !ushort.TryParse(parts[1], out ushort receivedPort))
                        continue;

                    DiscoveredHostAddress = remote.Address.ToString();

                    Debug.Log(
                        $"Host discovered: {DiscoveredHostAddress}:" +
                        $"{receivedPort}"
                    );

                    OnHostDiscovered?.Invoke(DiscoveredHostAddress);

                    // Stop searching after finding a host.
                    StopSearching();
                }
            }
        }
        catch (SocketException e)
        {
            Debug.LogWarning($"Discovery socket: {e.Message}");
        }
        catch (ObjectDisposedException)
        {
            // Socket was closed while processing discovery.
        }
    }

    public void JoinDiscoveredHost()
    {
        if (string.IsNullOrEmpty(DiscoveredHostAddress))
        {
            Debug.LogWarning("No host discovered yet.");
            return;
        }

        if (!CanStart())
            return;

        StopDiscovery();

        transport.SetConnectionData(
            DiscoveredHostAddress,
            gamePort
        );

        if (!networkManager.StartClient())
            Debug.LogError("Failed to start client.");
        else
            Debug.Log($"Connecting to {DiscoveredHostAddress}:{gamePort}");
    }

    public void Disconnect()
    {
        Debug.Log("Disconnecting...");

        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        StopDiscovery();
    }

    private void StopSearching()
    {
        isSearching = false;

        if (discoverySocket != null && !isHosting)
        {
            discoverySocket.Close();
            discoverySocket = null;
        }
    }

    private void StopDiscovery()
    {
        isHosting = false;
        isSearching = false;

        if (discoverySocket != null)
        {
            discoverySocket.Close();
            discoverySocket = null;
        }
    }

    private bool CanStart()
    {
        if (networkManager == null || transport == null)
        {
            Debug.LogError("NetworkManager or UnityTransport is missing.");
            return false;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning("Already in a network session.");
            return false;
        }

        return true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            StopDiscovery();
    }
}
