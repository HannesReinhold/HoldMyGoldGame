
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkedGoldBarStack : NetworkBehaviour
{
    [Header("Player")]
    [SerializeField] private NetworkedPlayer targetPlayer;

    [Header("Gold Bar Visual")]
    [SerializeField] private GameObject goldBarPrefab;
    [SerializeField] private Transform stackOrigin;

    [Header("Grid Layout")]
    [Min(1)][SerializeField] private int columns = 4;
    [Min(1)][SerializeField] private int rows = 4;
    [SerializeField]
    private Vector3 barSpacing =
        new Vector3(0.12f, 0.05f, 0.12f);

    private readonly NetworkVariable<int> syncedGoldBarCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly List<GameObject> visualBars =
        new List<GameObject>();

    private NetworkedPlayer subscribedPlayer;

    public int GoldBarCount => syncedGoldBarCount.Value;

    public override void OnNetworkSpawn()
    {
        syncedGoldBarCount.OnValueChanged += OnCountChanged;

        if (IsServer)
        {
            if (targetPlayer != null)
            {
                SubscribeToPlayer(targetPlayer);
            }
            else
            {
                TryFindPlayer();
            }
        }

        RebuildStack(syncedGoldBarCount.Value);
    }

    public override void OnNetworkDespawn()
    {
        syncedGoldBarCount.OnValueChanged -= OnCountChanged;
        UnsubscribeFromPlayer();
        ClearStack();
    }

    private void Update()
    {
        // Retry while the player reference is not yet available.
        if (IsServer && subscribedPlayer == null)
        {
            if (targetPlayer != null)
                SubscribeToPlayer(targetPlayer);
            else
                TryFindPlayer();
        }
    }

    private void TryFindPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

        // For a wheelbarrow associated with the server's own player.
        // For a specific remote player, assign Target Player instead.
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            NetworkedPlayer player =
                client.PlayerObject.GetComponent<NetworkedPlayer>();

            if (player != null && player.IsOwner)
            {
                SubscribeToPlayer(player);
                return;
            }
        }
    }

    private void SubscribeToPlayer(NetworkedPlayer player)
    {
        if (player == null || subscribedPlayer == player)
            return;

        UnsubscribeFromPlayer();

        targetPlayer = player;
        subscribedPlayer = player;

        subscribedPlayer.itemCount.OnValueChanged += OnPlayerCountChanged;

        SetGoldBarCountServer(subscribedPlayer.GoldBarCount);
    }

    private void UnsubscribeFromPlayer()
    {
        if (subscribedPlayer != null)
            subscribedPlayer.itemCount.OnValueChanged -= OnPlayerCountChanged;

        subscribedPlayer = null;
    }

    private void OnPlayerCountChanged(int previous, int current)
    {
        if (IsServer)
            SetGoldBarCountServer(current);
    }

    public void SetGoldBarCountServer(int count)
    {
        if (!IsServer || !IsSpawned)
            return;

        syncedGoldBarCount.Value = Mathf.Max(0, count);
    }

    private void OnCountChanged(int previous, int current)
    {
        RebuildStack(current);
    }

    private void RebuildStack(int count)
    {
        ClearStack();

        if (goldBarPrefab == null || stackOrigin == null)
            return;

        int barsPerLayer = columns * rows;

        for (int i = 0; i < count; i++)
        {
            int column = i % columns;
            int row = (i / columns) % rows;
            int layer = i / barsPerLayer;

            Vector3 localPosition = new Vector3(
                column * barSpacing.x,
                layer * barSpacing.y,
                row * barSpacing.z);

            GameObject bar = Instantiate(goldBarPrefab, stackOrigin);
            bar.transform.localPosition = localPosition;
            bar.transform.localRotation = Quaternion.identity;

            visualBars.Add(bar);
        }
    }

    private void ClearStack()
    {
        foreach (GameObject bar in visualBars)
        {
            if (bar != null)
                Destroy(bar);
        }

        visualBars.Clear();
    }
}
