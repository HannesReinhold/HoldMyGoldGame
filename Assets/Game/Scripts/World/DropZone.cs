using Unity.Netcode;
using UnityEngine;

public class DropZone : NetworkBehaviour
{

    [Header("Entnahme")]
    public bool takeAll = false;     // true = beim Rausnehmen alles auf einmal
    public int takeAmount = 1;       // sonst so viele pro Tastendruck

    private NetworkVariable<int> storedItems = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int StoredItems => storedItems.Value;

    public override void OnNetworkSpawn()
    {
        storedItems.OnValueChanged += OnStoredChanged;
    }

    public override void OnNetworkDespawn()
    {
        storedItems.OnValueChanged -= OnStoredChanged;
    }

    private void OnStoredChanged(int oldValue, int newValue)
    {
        Debug.Log($"DropZone {name}: {newValue} Items");
        // TODO: Anzeige (z. B. TextMeshPro über der Zone) aktualisieren
    }

    // Nur Server
    public void Deposit(int amount)
    {
        if (!IsServer || amount <= 0) return;
        storedItems.Value += amount;
    }

    // Nur Server. Gibt zurück, wie viele tatsächlich entnommen wurden.
    public int Take()
    {
        if (!IsServer) return 0;

        int requested = takeAll ? storedItems.Value : takeAmount;
        int amount = Mathf.Clamp(requested, 0, storedItems.Value);
        storedItems.Value -= amount;
        return amount;
    }
}