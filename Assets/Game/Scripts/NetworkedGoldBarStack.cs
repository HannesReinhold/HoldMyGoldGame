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

    [Header("Cart Layout")]
    [Tooltip("Transform whose scale changes when the cart width is upgraded.")]
    [SerializeField] private Transform cartTransform;

    [Tooltip("Local-space width available for gold bars at the base cart size.")]
    [Min(0.01f)]
    [SerializeField] private float baseAvailableWidth = 0.48f;

    [Tooltip("Local-space depth available for gold bars.")]
    [Min(0.01f)]
    [SerializeField] private float availableDepth = 0.48f;

    [Tooltip("Small margin kept between the bars and the cart edges.")]
    [Min(0f)]
    [SerializeField] private float edgeMargin = 0.02f;

    [Header("Gold Bar Spacing")]
    public float randomRotation = 1f;

    [Min(0.01f)]
    [SerializeField] private float barWidth = 0.12f;

    [Min(0.01f)]
    [SerializeField] private float barDepth = 0.12f;

    [Min(0.01f)]
    [SerializeField] private float barHeight = 0.05f;

    [SerializeField]
    private Vector3 barSpacing =
        new Vector3(0.12f, 0.05f, 0.12f);

    [Header("Layout Update")]
    [Tooltip("Recalculate the layout if the cart's scale changes.")]
    [SerializeField] private bool monitorCartScale = true;

    private readonly NetworkVariable<int> syncedGoldBarCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly List<GameObject> visualBars =
        new List<GameObject>();

    private NetworkedPlayer subscribedPlayer;

    private Vector3 lastCartScale;
    private int lastLayoutCount = -1;

    public int GoldBarCount => syncedGoldBarCount.Value;

    [Header("Width Upgrade Layout")]
    [SerializeField, Min(1)] private int baseColumns = 4;
    [SerializeField, Min(0)] private int extraColumnsPerUpgrade = 1;
    [SerializeField, Min(1)] private int rows = 4;

    private int lastWidthLevel = -1;

    public override void OnNetworkSpawn()
    {
        syncedGoldBarCount.OnValueChanged += OnCountChanged;

        if (cartTransform == null && targetPlayer != null)
            cartTransform = targetPlayer.cartTransform;

        if (cartTransform != null)
            lastCartScale = cartTransform.localScale;

        if (IsServer)
        {
            if (targetPlayer != null)
                SubscribeToPlayer(targetPlayer);
            else
                TryFindPlayer();
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
        if (targetPlayer == null)
        {
            targetPlayer = GetComponentInParent<NetworkedPlayer>();
        }

        if (IsServer && subscribedPlayer == null)
        {
            if (targetPlayer != null)
                SubscribeToPlayer(targetPlayer);
            else
                TryFindPlayer();
        }

        int widthLevel = targetPlayer != null
            ? targetPlayer.CartWidthLevel
            : 0;

        if (widthLevel != lastWidthLevel)
        {
            lastWidthLevel = widthLevel;
            RebuildStack(syncedGoldBarCount.Value);
        }
    }

    private void TryFindPlayer()
    {
        if (NetworkManager.Singleton == null)
            return;

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

        if (cartTransform == null)
            cartTransform = player.cartTransform;

        if (cartTransform != null)
            lastCartScale = cartTransform.localScale;

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

        int currentColumns = Mathf.Max(
            1,
            baseColumns + lastWidthLevel * extraColumnsPerUpgrade
        );

        int currentRows = Mathf.Max(1, rows);
        int barsPerLayer = currentColumns * currentRows;

        // Center the grid around the stack origin.
        float gridWidth = (currentColumns - 1) * barSpacing.x;
        float gridDepth = (currentRows - 1) * barSpacing.z;

        float startX = -gridWidth * 0.5f;
        float startZ = -gridDepth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            int column = i % currentColumns;
            int row = (i / currentColumns) % currentRows;
            int layer = i / barsPerLayer;

            Vector3 localPosition = new Vector3(
                startX + column * barSpacing.x,
                layer * barSpacing.y,
                startZ + row * barSpacing.z
            );

            GameObject bar = Instantiate(goldBarPrefab, stackOrigin);

            // Deterministic rotation so all clients display the same layout.
            Random.State previousRandomState = Random.state;
            Random.InitState(i);

            float rotationY = Random.Range(
                -randomRotation,
                randomRotation
            );

            Random.state = previousRandomState;

            bar.transform.localPosition = localPosition;
            bar.transform.localRotation =
                Quaternion.Euler(0f, rotationY, 0f);

            visualBars.Add(bar);
        }
    }

    private float GetBaseCartScaleX()
    {
        // Use the width before upgrades, not the current enlarged scale.
        // Set this to your cart's original local X scale in the Inspector.
        return Mathf.Max(0.0001f, baseCartScaleX);
    }

    [Header("Base Cart Scale")]
    [Tooltip("Original cartTransform.localScale.x before any width upgrades.")]
    [Min(0.0001f)]
    [SerializeField] private float baseCartScaleX = 1f;

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