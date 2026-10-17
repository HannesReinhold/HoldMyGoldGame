
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class PressurePlate : NetworkBehaviour
{
    [Header("Weight")]
    [SerializeField] private float requiredWeight = 10f;

    [Header("Break Timer")]
    [SerializeField, Min(0.1f)] private float breakDelaySeconds = 1f;

    [Header("Wiggle")]
    [SerializeField, Range(0f, 1f)] private float minimumWiggleIntensity = 0.25f;
    [SerializeField] private Transform plateVisual;
    [SerializeField] private float maxWiggleAngle = 8f;
    [SerializeField] private float wiggleFrequency = 18f;
    [SerializeField] private float maxWiggleHeight = 0.025f;

    // Weight above the threshold needed to reach maximum wiggle.
    [SerializeField] private float extraWeightForMaxWiggle = 10f;


    [Header("Visuals")]
    [SerializeField] private GameObject activatedVisual;
    [SerializeField] private GameObject deactivatedVisual;
    [SerializeField] private GameObject brokenVisual;

    [Header("Events")]
    [SerializeField] private UnityEvent onActivated;
    [SerializeField] private UnityEvent onDeactivated;
    [SerializeField] private UnityEvent onBroken;

    private readonly NetworkVariable<float> totalWeight =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    // Progress is measured in seconds, from 0 to breakDelaySeconds.
    private readonly NetworkVariable<float> breakProgress =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isActivated =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isBroken =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private Quaternion initialLocalRotation;
    private Vector3 initialLocalPosition;

    public float TotalWeight => totalWeight.Value;
    public float BreakProgress => breakProgress.Value;
    public bool IsActivated => isActivated.Value;
    public bool IsBroken => isBroken.Value;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (plateVisual != null)
        {
            initialLocalRotation = plateVisual.localRotation;
            initialLocalPosition = plateVisual.localPosition;
        }
    }

    public override void OnNetworkSpawn()
    {
        isActivated.OnValueChanged += OnActivationChanged;
        isBroken.OnValueChanged += OnBrokenChanged;

        ApplyActivationVisuals(isActivated.Value);
        ApplyBrokenVisuals(isBroken.Value);
    }

    public override void OnNetworkDespawn()
    {
        isActivated.OnValueChanged -= OnActivationChanged;
        isBroken.OnValueChanged -= OnBrokenChanged;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        // Only the server controls the timer and break state.
        if (IsServer && !isBroken.Value)
        {
            bool overloaded =
                totalWeight.Value >= requiredWeight;

            float targetProgress =
                overloaded ? breakDelaySeconds : 0f;

            breakProgress.Value = Mathf.MoveTowards(
                breakProgress.Value,
                targetProgress,
                Time.deltaTime);

            if (overloaded && breakProgress.Value >= breakDelaySeconds)
            {
                BreakPlate();
            }
        }

        // Every client animates its own visual using the synced progress.
        UpdateWiggle();
    }

    private void BreakPlate()
    {
        if (!IsServer || !IsSpawned || isBroken.Value)
            return;

        isBroken.Value = true;

        // Destroy the networked plate on every client.
        NetworkObject.Despawn(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || isBroken.Value)
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
        {
            ChangeWeight(1);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer || !IsSpawned || isBroken.Value)
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
        {
            ChangeWeight(-1);
        }
    }

    private void ChangeWeight(float weightChangeKg)
    {
        totalWeight.Value = Mathf.Max(
            0f,
            totalWeight.Value + weightChangeKg);

        bool shouldActivate =
            totalWeight.Value >= requiredWeight;

        if (isActivated.Value != shouldActivate)
            isActivated.Value = shouldActivate;

        Debug.Log(
            $"{name}: {totalWeight.Value:F1} Bars / " +
            $"{requiredWeight:F1} Bars | " +
            $"Break timer: {breakProgress.Value:F2}s / " +
            $"{breakDelaySeconds:F2}s");
    }

    private void UpdateWiggle()
    {
        if (plateVisual == null)
            return;

        if (isBroken.Value)
        {
            plateVisual.localRotation = initialLocalRotation;
            plateVisual.localPosition = initialLocalPosition;
            return;
        }

        // 0 = no weight over the threshold.
        // 1 = maximum wiggle intensity.
        float excessWeight = Mathf.Max(
            0f, totalWeight.Value - requiredWeight);

        float extraIntensity = Mathf.Clamp01(
            excessWeight / Mathf.Max(0.01f, extraWeightForMaxWiggle));

        // Start at the minimum intensity when the threshold is reached.
        float intensity = 0f;

        if (totalWeight.Value >= requiredWeight)
        {
            intensity = Mathf.Lerp(
                minimumWiggleIntensity,
                1f,
                extraIntensity);
        }

        // Ease into the movement for a smoother response.
        intensity = intensity * intensity;

        float angle = Mathf.Sin(Time.time * wiggleFrequency)
            * maxWiggleAngle * intensity;

        float height = Mathf.Abs(
            Mathf.Sin(Time.time * wiggleFrequency * 0.5f))
            * maxWiggleHeight * intensity;

        plateVisual.localRotation =
            initialLocalRotation * Quaternion.Euler(0f, 0f, angle);

        plateVisual.localPosition =
            initialLocalPosition + Vector3.up * height;
    }

    private void OnActivationChanged(bool previous, bool current)
    {
        ApplyActivationVisuals(current);

        if (current)
            onActivated?.Invoke();
        else
            onDeactivated?.Invoke();
    }

    private void OnBrokenChanged(bool previous, bool current)
    {
        ApplyBrokenVisuals(current);

        if (current)
        {
            onBroken?.Invoke();
            Debug.Log($"{name} has broken!");
        }
    }

    private void ApplyActivationVisuals(bool activated)
    {
        if (activatedVisual != null)
            activatedVisual.SetActive(activated);

        if (deactivatedVisual != null)
            deactivatedVisual.SetActive(!activated);
    }

    private void ApplyBrokenVisuals(bool broken)
    {
        if (brokenVisual != null)
            brokenVisual.SetActive(broken);

        if (plateVisual != null)
            plateVisual.gameObject.SetActive(!broken);

        if (broken)
        {
            if (activatedVisual != null)
                activatedVisual.SetActive(false);

            if (deactivatedVisual != null)
                deactivatedVisual.SetActive(false);
        }
        else
        {
            ApplyActivationVisuals(isActivated.Value);
        }
    }
}
