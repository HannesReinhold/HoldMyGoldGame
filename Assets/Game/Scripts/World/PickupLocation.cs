using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PickupLocation : HighlightableZone
{
    public int amountPerPickup = 1;
    public float interactRadius = 3f;   // Server-Check der Distanz
    public float cooldown = 2f;         // 0 = unbegrenzt/sofort wieder nutzbar

    private float nextAvailableTime;    // nur Server

    [Header("Gold")]
    public int numBars = 1000;

    private NetworkVariable<int> syncedNumBars = new NetworkVariable<int>(
        1000,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int NumBars => syncedNumBars.Value;

    [Header("Gold Bar Visuals")]
    [SerializeField] private GameObject goldBarPrefab;
    [SerializeField] private Transform stackOrigin;

    [Header("Randomized Stack")]
    [SerializeField] private int randomSeed = 12345;
    [Min(1)][SerializeField] private int columns = 4;
    [Min(1)][SerializeField] private int rows = 4;

    [SerializeField]
    private Vector3 barSpacing =
        new Vector3(0.12f, 0.05f, 0.08f);

    [SerializeField]
    private Vector3 positionJitter =
        new Vector3(0.025f, 0.008f, 0.025f);

    [SerializeField]
    private Vector3 rotationJitter =
        new Vector3(8f, 15f, 8f);

    private readonly List<GameObject> visualBars =
        new List<GameObject>();

    private Transform Origin =>
        stackOrigin != null ? stackOrigin : transform;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        syncedNumBars.OnValueChanged += OnNumBarsChanged;

        if (IsServer)
        {
            // Initialize the networked count from the original Inspector variable.
            syncedNumBars.Value = Mathf.Max(0, numBars);
        }

        ReconcileStack(syncedNumBars.Value);
    }

    public override void OnNetworkDespawn()
    {
        syncedNumBars.OnValueChanged -= OnNumBarsChanged;
        ClearStack();

        base.OnNetworkDespawn();
    }

    private void OnNumBarsChanged(int oldValue, int newValue)
    {
        numBars = newValue;
        ReconcileStack(newValue);
    }

    // Nur auf dem Server aufrufen. Gibt die Anzahl zurück, die der Spieler bekommt.
    public int TryTake(PlayerUpgradeStats playerUpgradeStats)
    {
        if (!IsServer) return 0;
        if (Time.time < nextAvailableTime) return 0;

        int taken = Mathf.Min(Mathf.Max(0, amountPerPickup), syncedNumBars.Value);
        if (taken <= 0) return 0;

        nextAvailableTime = Time.time + (cooldown/playerUpgradeStats.PickupSpeedMultiplier);

        syncedNumBars.Value -= taken;
        numBars = syncedNumBars.Value;

        return taken;
    }

    private void ReconcileStack(int count)
    {
        if (goldBarPrefab == null)
            return;

        // Remove only excess bars, preserving the remaining visual objects.
        while (visualBars.Count > count)
        {
            int lastIndex = visualBars.Count - 1;
            GameObject bar = visualBars[lastIndex];
            visualBars.RemoveAt(lastIndex);

            if (bar != null)
                Destroy(bar);
        }

        // Create only the new bars.
        while (visualBars.Count < count)
        {
            int index = visualBars.Count;
            GameObject bar = Instantiate(goldBarPrefab, Origin);

            ApplyStableTransform(bar.transform, index);
            visualBars.Add(bar);
        }
    }

    private void ApplyStableTransform(Transform bar, int index)
    {
        Random.State previousState = Random.state;

        // Each bar has its own deterministic random sequence.
        Random.InitState(unchecked(randomSeed + index * 7919));

        int safeColumns = Mathf.Max(1, columns);
        int safeRows = Mathf.Max(1, rows);
        int barsPerLayer = safeColumns * safeRows;

        int column = index % safeColumns;
        int row = (index / safeColumns) % safeRows;
        int layer = index / barsPerLayer;

        float x = (column - (safeColumns - 1) * 0.5f) * barSpacing.x;
        float z = (row - (safeRows - 1) * 0.5f) * barSpacing.z;
        float y = layer * barSpacing.y;

        x += Random.Range(-positionJitter.x, positionJitter.x);
        y += Random.Range(-positionJitter.y, positionJitter.y);
        z += Random.Range(-positionJitter.z, positionJitter.z);

        bar.localPosition = new Vector3(x, y, z);

        bar.localRotation = Quaternion.Euler(
            Random.Range(-rotationJitter.x, rotationJitter.x),
            Random.Range(-rotationJitter.y, rotationJitter.y),
            Random.Range(-rotationJitter.z, rotationJitter.z)
        );

        bar.localScale = Vector3.one * 1.5f;

        Random.state = previousState;
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