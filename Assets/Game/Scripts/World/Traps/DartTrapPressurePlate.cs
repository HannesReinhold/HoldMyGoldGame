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

    [Header("FMOD Sound")]
    [SerializeField] private EventReference pressedSound;

    private Vector3 plateUpPosition;
    private Vector3 plateDownPosition;

    private bool isArmed = true;

    // Server only: what is currently standing or lying on the plate.
    private readonly PlateOccupants occupants = new PlateOccupants();

    private readonly NetworkVariable<float> totalWeight =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    // True while the trap has fired and has not been re-armed yet.
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

        if (IsServer)
        {
            occupants.Clear();
            isArmed = true;
            totalWeight.Value = 0f;
            isActivated.Value = false;
            isPressed.Value = false;
        }

        // Snap to the synchronized state instead of animating from wherever the mesh was.
        if (plateMesh != null)
            plateMesh.localPosition = isPressed.Value ? plateDownPosition : plateUpPosition;
    }

    public override void OnNetworkDespawn()
    {
        isActivated.OnValueChanged -= OnActivationChanged;
    }

    private void Update()
    {
        if (IsServer && IsSpawned)
            RecalculateWeight();

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

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned)
            return;

        occupants.OnEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer || !IsSpawned)
            return;

        occupants.OnExit(other);
    }

    // Server only. Recomputed every frame so that gold picked up or deposited while
    // standing on the plate, picked-up items and disconnected players are all accounted for.
    private void RecalculateWeight()
    {
        float weight = occupants.ComputeWeight(itemWeight);

        SetPressed(occupants.Any);

        if (!Mathf.Approximately(totalWeight.Value, weight))
            totalWeight.Value = weight;

        bool thresholdReached = weight >= requiredWeight;

        if (thresholdReached && isArmed)
        {
            isArmed = false;
            isActivated.Value = true;
            FireTrap();
        }
        else if (!thresholdReached && !isArmed && requireWeightRemovalToRearm)
        {
            isArmed = true;
            isActivated.Value = false;
        }
    }

    private void SetPressed(bool pressed)
    {
        if (!IsServer || isPressed.Value == pressed)
            return;

        isPressed.Value = pressed;

        if (pressed)
            PlayPressedSoundClientRpc();
    }

    private void OnActivationChanged(bool previous, bool current)
    {
        if (current)
            onActivated?.Invoke();
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
