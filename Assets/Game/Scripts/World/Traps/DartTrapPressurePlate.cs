
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class DartTrapPressurePlate : NetworkBehaviour
{
    [Header("Weight")]
    [SerializeField] private float requiredWeight = 150f;
    [SerializeField] private float itemWeight = 1f;

    [Header("Dart Trap")]
    [SerializeField] private DartTrap dartTrap;

    [Header("Events")]
    [SerializeField] private UnityEvent onActivated;

    [Header("Trap Settings")]
    [SerializeField] private bool requireWeightRemovalToRearm = true;

    private bool isArmed = true;

    private readonly NetworkVariable<float> totalWeight =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isActivated =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public float TotalWeight => totalWeight.Value;
    public bool IsActivated => isActivated.Value;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    public override void OnNetworkSpawn()
    {
        isActivated.OnValueChanged += OnActivationChanged;

        if (isActivated.Value)
            FireTrap();
    }

    public override void OnNetworkDespawn()
    {
        isActivated.OnValueChanged -= OnActivationChanged;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || isActivated.Value)
            return;

        NetworkedPlayer player =
            other.GetComponentInParent<NetworkedPlayer>();

        if (player != null && player.IsSpawned)
        {
            ChangeWeight(player.GoldBarCount);
            return;
        }

        PickableItem item =
            other.GetComponentInParent<PickableItem>();

        if (item != null)
            ChangeWeight(itemWeight);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer || !IsSpawned || isActivated.Value)
            return;

        NetworkedPlayer player =
            other.GetComponentInParent<NetworkedPlayer>();

        if (player != null && player.IsSpawned)
        {
            ChangeWeight(-player.GoldBarCount);
            return;
        }

        PickableItem item =
            other.GetComponentInParent<PickableItem>();

        if (item != null)
            ChangeWeight(-itemWeight);
    }

    private void ChangeWeight(float amount)
    {
        totalWeight.Value = Mathf.Max(
            0f,
            totalWeight.Value + amount);

        bool thresholdReached =
            totalWeight.Value >= requiredWeight;

        if (thresholdReached && isArmed)
        {
            isArmed = false;
            FireTrap();
        }
        else if (!thresholdReached && requireWeightRemovalToRearm)
        {
            isArmed = true;
        }

        Debug.Log(
            $"{name}: {totalWeight.Value:F1} / " +
            $"{requiredWeight:F1} kg");
    }


    private void OnActivationChanged(bool previous, bool current)
    {
        if (current)
        {
            FireTrap();
            onActivated?.Invoke();
        }
    }

    private void FireTrap()
{
    if (!IsServer || !IsSpawned)
        return;

    if (dartTrap != null)
        dartTrap.Fire();
    else
        Debug.LogWarning($"{name}: No DartTrap assigned.", this);
}
}

