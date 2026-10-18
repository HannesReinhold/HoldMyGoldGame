
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using FMODUnity;

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

    [Header("Plate Movement")]
    [SerializeField] private Transform plateMesh;
    [SerializeField] private float pressDistance = 0.05f;
    [SerializeField] private float moveSpeed = 5f;

    private Vector3 plateUpPosition;
    private Vector3 plateDownPosition;
    private bool platePressed;

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

    // Synchronizes the pressed state so all clients see the same movement.
    private readonly NetworkVariable<bool> isPressed =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public float TotalWeight => totalWeight.Value;
    public bool IsActivated => isActivated.Value;

    [Header("FMOD Sound")]
    [SerializeField] private EventReference pressedSound;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (plateMesh != null)
        {
            plateUpPosition = plateMesh.localPosition;
            plateDownPosition =
                plateUpPosition - Vector3.up * pressDistance;
        }
    }

    public override void OnNetworkSpawn()
    {
        isActivated.OnValueChanged += OnActivationChanged;
        isPressed.OnValueChanged += OnPressedChanged;

        UpdatePlateTarget();

        if (isActivated.Value)
            FireTrap();
    }

    public override void OnNetworkDespawn()
    {
        isActivated.OnValueChanged -= OnActivationChanged;
        isPressed.OnValueChanged -= OnPressedChanged;
    }

    private void Update()
    {
        if (plateMesh == null)
            return;

        Vector3 target = isPressed.Value
            ? plateDownPosition
            : plateUpPosition;

        plateMesh.localPosition = Vector3.MoveTowards(
            plateMesh.localPosition,
            target,
            moveSpeed * Time.deltaTime);
    }

    private void OnPressedChanged(bool previous, bool current)
    {
        UpdatePlateTarget();
    }

    private void UpdatePlateTarget()
    {
        if (plateMesh == null)
            return;

        // Positions are initialized from the mesh's starting position.
        plateMesh.localPosition = plateMesh.localPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || isActivated.Value)
            return;

        NetworkedPlayer player =
            other.GetComponentInParent<NetworkedPlayer>();

        if (player != null && player.IsSpawned)
        {
            SetPressed(true);
            ChangeWeight(player.GoldBarCount);
            return;
        }

        PickableItem item =
            other.GetComponentInParent<PickableItem>();

        if (item != null)
        {
            SetPressed(true);
            ChangeWeight(itemWeight);
        }
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
            UpdatePressedState();
            return;
        }

        PickableItem item =
            other.GetComponentInParent<PickableItem>();

        if (item != null)
        {
            ChangeWeight(-itemWeight);
            UpdatePressedState();
        }
    }

    private void SetPressed(bool pressed)
    {
        if (!IsServer || isPressed.Value == pressed)
            return;

        isPressed.Value = pressed;
    }

    private void UpdatePressedState()
    {
        // Check whether anything remains on the plate.
        // The trigger collider is used to detect overlapping objects.
        Collider plateCollider = GetComponent<Collider>();
        Collider[] hits = Physics.OverlapBox(
            plateCollider.bounds.center,
            plateCollider.bounds.extents,
            plateCollider.transform.rotation);

        bool somethingOnPlate = false;

        foreach (Collider hit in hits)
        {
            if (hit == plateCollider || hit.isTrigger)
                continue;

            if (hit.GetComponentInParent<NetworkedPlayer>() != null ||
                hit.GetComponentInParent<PickableItem>() != null)
            {
                somethingOnPlate = true;
                PlayPressedSoundClientRpc();
                break;
            }
        }

        SetPressed(somethingOnPlate);
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

    [ClientRpc]
    private void PlayPressedSoundClientRpc()
    {
        if (!pressedSound.IsNull)
        {
            RuntimeManager.PlayOneShot(
                pressedSound,
                transform.position
            );
        }
    }
}