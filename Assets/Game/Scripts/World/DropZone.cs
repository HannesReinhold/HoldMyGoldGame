
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DropZone : HighlightableZone
{
    [Header("Entnahme")]
    public bool takeAll = false;
    public int takeAmount = 1;

    [Header("Besitzer")]
    public int ownerSlot;
    public bool allowStealing = true;

    private NetworkVariable<int> storedItems = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int StoredItems => storedItems.Value;

    [Header("Goldbarren Anzeige")]
    [Tooltip("Prefab für die rein lokale Darstellung eines Goldbarrens.")]
    [SerializeField] private GameObject goldBarPrefab;

    [Tooltip("Position und Ausrichtung des Stapels. Falls leer, wird dieses Objekt verwendet.")]
    [SerializeField] private Transform stackOrigin;

    [Tooltip("Anzahl der Spalten pro Schicht.")]
    [Min(1)]
    [SerializeField] private int columns = 4;

    [Tooltip("Anzahl der Reihen pro Schicht.")]
    [Min(1)]
    [SerializeField] private int rows = 4;

    public float randomRotation=1;

    [Tooltip("Abstand zwischen den Goldbarren in lokalen Einheiten.")]
    [SerializeField]
    private Vector3 barSpacing =
        new Vector3(0.12f, 0.05f, 0.08f);

    // Diese Objekte existieren ausschließlich lokal auf jedem Client.
    private readonly List<GameObject> visualGoldBars =
        new List<GameObject>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        storedItems.OnValueChanged += OnStoredChanged;

        // Auch den bereits synchronisierten Anfangswert darstellen.
        RebuildGoldBarStack(storedItems.Value);
    }

    public override void OnNetworkDespawn()
    {
        storedItems.OnValueChanged -= OnStoredChanged;

        ClearGoldBarStack();

        base.OnNetworkDespawn();
    }

    private void OnStoredChanged(int oldValue, int newValue)
    {
        Debug.Log($"DropZone {name}: {newValue} Items");

        RebuildGoldBarStack(newValue);
    }

    private void RebuildGoldBarStack(int count)
    {
        ClearGoldBarStack();

        if (goldBarPrefab == null)
        {
            Debug.LogWarning(
                $"DropZone {name}: Kein Goldbarren-Prefab zugewiesen.",
                this
            );
            return;
        }

        Transform origin = stackOrigin != null
            ? stackOrigin
            : transform;

        int safeColumns = Mathf.Max(1, columns);
        int safeRows = Mathf.Max(1, rows);
        int barsPerLayer = safeColumns * safeRows;

        for (int i = 0; i < count; i++)
        {
            int column = i % safeColumns;
            int row = (i / safeColumns) % safeRows;
            int layer = i / barsPerLayer;

            // Die Schicht horizontal um den Ursprung zentrieren.
            float x = (column - (safeColumns - 1) * 0.5f)
                      * barSpacing.x;

            float z = (row - (safeRows - 1) * 0.5f)
                      * barSpacing.z;

            float y = layer * barSpacing.y;

            GameObject bar = Instantiate(goldBarPrefab, origin);

            bar.transform.localPosition = new Vector3(x, y, z);
            Random.InitState(column * rows + row + layer);
            bar.transform.localRotation = Quaternion.identity * Quaternion.Euler(0f, Random.Range(-randomRotation, randomRotation), 0f);
            bar.transform.localScale = Vector3.one * 2.5f;

            visualGoldBars.Add(bar);
        }
    }

    private void ClearGoldBarStack()
    {
        foreach (GameObject bar in visualGoldBars)
        {
            if (bar != null)
            {
                Destroy(bar);
            }
        }

        visualGoldBars.Clear();
    }

    // Nur Server
    public void Deposit(int amount)
    {
        if (!IsServer || amount <= 0)
            return;

        storedItems.Value += amount;
    }

    // Nur Server. Gibt zurück, wie viele tatsächlich entnommen wurden.
    public int Take()
    {
        if (!IsServer)
            return 0;

        int requested = takeAll ? storedItems.Value : takeAmount;
        int amount = Mathf.Clamp(requested, 0, storedItems.Value);

        storedItems.Value -= amount;

        return amount;
    }

    public void ResetStored()
    {
        if (!IsServer) return;
        storedItems.Value = 0;
    }
}