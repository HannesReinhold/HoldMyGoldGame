
using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;

public class RelayHostJoinManager : MonoBehaviour
{
    [SerializeField] private int maxConnections = 8;

    private NetworkManager networkManager;
    private UnityTransport transport;

    public event System.Action<string> JoinCodeReady;

    private async Task InitializeServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private void Awake()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager != null)
        {
            transport =
                networkManager.GetComponent<UnityTransport>();
        }
    }

    public async void Host()
    {
        try
        {
            if (networkManager == null || transport == null)
            {
                Debug.LogError("NetworkManager or UnityTransport missing.");
                return;
            }

            if (networkManager.IsListening)
            {
                Debug.LogWarning("NetworkManager is already running.");
                return;
            }

            await InitializeServices();

            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(
                    maxConnections);

            transport.UseWebSockets = true;

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(
                    allocation, "wss"));

            string joinCode =
                await RelayService.Instance.GetJoinCodeAsync(
                    allocation.AllocationId);

            JoinCodeReady?.Invoke(joinCode);

            if (networkManager.StartHost())
            {
                Debug.Log("Relay host started. Join code: " + joinCode);
                // Display joinCode in your lobby UI.
            }
            else
            {
                Debug.LogError("NGO failed to start the host.");
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public async Task<bool> Join(string joinCode)
    {
        try
        {
            if (networkManager == null || transport == null)
            {
                Debug.LogError("NetworkManager or UnityTransport missing.");
                return false;
            }

            if (networkManager.IsListening)
            {
                Debug.LogWarning("NetworkManager is already running.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                Debug.LogError("Enter a Relay join code.");
                return false;
            }

            await InitializeServices();

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(
                    joinCode.Trim());

            transport.UseWebSockets = true;

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "wss"));

            if (!networkManager.StartClient())
            {
                Debug.LogError("NGO failed to start the client.");
                return false;
            }

            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.LogWarning($"Relay join failed: {e.Message}");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }
}
