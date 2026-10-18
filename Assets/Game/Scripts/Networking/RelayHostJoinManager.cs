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

    // Raised with a user-readable message when hosting or joining fails,
    // so the menu does not stay stuck on "Creating..." / "Joining...".
    public event System.Action<string> OperationFailed;

    private bool busy;

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

    // NetworkManager.Singleton is only assigned in its OnEnable, so caching it in
    // Awake depends on script execution order. Resolve it lazily instead.
    private bool ResolveNetwork()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager != null && transport == null)
            transport = networkManager.GetComponent<UnityTransport>();

        return networkManager != null && transport != null;
    }

    private void Fail(string message, Exception exception = null)
    {
        if (exception != null)
            Debug.LogException(exception);
        else
            Debug.LogError(message);

        OperationFailed?.Invoke(message);
    }

    public async void Host()
    {
        if (busy)
            return;

        try
        {
            busy = true;

            if (!ResolveNetwork())
            {
                Fail("NetworkManager or UnityTransport missing.");
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

            // This object may have been destroyed (scene change) while waiting.
            if (this == null || !ResolveNetwork())
                return;

            transport.UseWebSockets = true;

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(
                    allocation, "wss"));

            string joinCode =
                await RelayService.Instance.GetJoinCodeAsync(
                    allocation.AllocationId);

            if (this == null)
                return;

            JoinCodeReady?.Invoke(joinCode);

            if (networkManager.StartHost())
            {
                Debug.Log("Relay host started. Join code: " + joinCode);
            }
            else
            {
                Fail("Failed to start the host.");
            }
        }
        catch (Exception e)
        {
            Fail("Could not create the Relay session: " + e.Message, e);
        }
        finally
        {
            busy = false;
        }
    }

    // Returns false when joining failed; MenuManager awaits this to show feedback.
    public async Task<bool> Join(string joinCode)
    {
        if (busy)
            return false;

        try
        {
            busy = true;

            if (!ResolveNetwork())
            {
                Fail("NetworkManager or UnityTransport missing.");
                return false;
            }

            if (networkManager.IsListening)
            {
                Debug.LogWarning("NetworkManager is already running.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                Fail("Enter a Relay join code.");
                return false;
            }

            await InitializeServices();

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(
                    joinCode.Trim());

            if (this == null || !ResolveNetwork())
                return false;

            transport.UseWebSockets = true;

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "wss"));

            if (!networkManager.StartClient())
            {
                Fail("Failed to start the client.");
                return false;
            }

            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.LogWarning($"Relay join failed: {e.Message}");
            OperationFailed?.Invoke("Invalid Join-Code or Connection failed.");
            return false;
        }
        catch (Exception e)
        {
            Fail("Could not join the Relay session: " + e.Message, e);
            return false;
        }
        finally
        {
            busy = false;
        }
    }
}
