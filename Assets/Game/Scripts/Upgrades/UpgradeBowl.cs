using Unity.Netcode;
using UnityEngine;
using TMPro;
using FMODUnity;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class UpgradeBowl : NetworkBehaviour
{
    [Header("Fire Scaling")]
    [SerializeField, Min(1f)] private float maxFireScale = 2.5f;
    private Vector3 baseFireScale;

    [Header("FMOD Sounds")]
    [SerializeField] private EventReference goldIntoFireEvent;
    [SerializeField] private EventReference upgradeGrantedEvent;

    public enum UpgradeType
    {
        RunningSpeed,
        Width,
        PickupSpeed,
        ThrowSpeed
    }

    [Header("Upgrade")]
    [SerializeField]
    private UpgradeType upgradeType =
        UpgradeType.RunningSpeed;

    [Min(1)]
    [SerializeField] private int barsRequired = 10;

    [Tooltip("Additional gold bars required after each successful upgrade.")]
    [Min(1)]
    [SerializeField] private int priceIncreasePerUpgrade = 5;

    [Header("World Space UI")]
    [SerializeField] private TMP_Text upgradeText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text offeredText;

    [Header("Networked Effects")]
    [SerializeField] private ParticleSystem fireEffect;

    [Tooltip("Particle effect played when an upgrade is successfully granted.")]
    [SerializeField] private ParticleSystem upgradeGrantedEffect;

    private readonly NetworkVariable<int> offeredBars =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> networkBarsRequired =
        new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private ulong lastThrowerClientId = ulong.MaxValue;

    public int OfferedBars => offeredBars.Value;
    public int CurrentPrice => networkBarsRequired.Value;

    private void Awake()
    {
        if (fireEffect != null)
            baseFireScale = fireEffect.transform.localScale;
    }

    public override void OnNetworkSpawn()
    {
        offeredBars.OnValueChanged += OnOfferedBarsChanged;
        networkBarsRequired.OnValueChanged += OnPriceChanged;

        // The server initializes the price from the Inspector.
        if (IsServer)
            networkBarsRequired.Value = Mathf.Max(1, barsRequired);

        UpdateFlameSize(offeredBars.Value);
        UpdateUI(offeredBars.Value);
    }

    public override void OnNetworkDespawn()
    {
        offeredBars.OnValueChanged -= OnOfferedBarsChanged;
        networkBarsRequired.OnValueChanged -= OnPriceChanged;
    }

    private void OnOfferedBarsChanged(int previousValue, int newValue)
    {
        UpdateFlameSize(newValue);
        UpdateUI(newValue);
    }

    private void OnPriceChanged(int previousValue, int newValue)
    {
        UpdateFlameSize(offeredBars.Value);
        UpdateUI(offeredBars.Value);
    }

    private void UpdateFlameSize(int barCount)
    {
        if (fireEffect == null)
            return;

        float price = Mathf.Max(1, CurrentPrice);
        float progress = Mathf.Clamp01(barCount / price);
        float scale = Mathf.Lerp(1f, maxFireScale, progress);

        fireEffect.transform.localScale = baseFireScale * scale;

        if (barCount > 0 && !fireEffect.isPlaying)
            fireEffect.Play();
    }

    private void UpdateUI(int count)
    {
        if (upgradeText != null)
        {
            switch (upgradeType)
            {
                case UpgradeType.RunningSpeed:
                    upgradeText.text = "Running Speed";
                    break;
                case UpgradeType.PickupSpeed:
                    upgradeText.text = "Pickup Speed";
                    break;
                case UpgradeType.ThrowSpeed:
                    upgradeText.text = "Throw Force";
                    break;
                case UpgradeType.Width:
                    upgradeText.text = "Cart Width";
                    break;
            }
        }

        if (priceText != null)
            priceText.text = $"Price: {CurrentPrice} gold";

        if (offeredText != null)
            offeredText.text = $"{count} / {CurrentPrice}";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned)
            return;

        ItemHitDamage item =
            other.GetComponentInParent<ItemHitDamage>();

        if (item == null)
            return;

        ulong throwerId = item.throwerClientId;

        if (throwerId == NetworkedPlayer.NoAttacker)
            return;

        NetworkObject itemNetworkObject =
            item.GetComponentInParent<NetworkObject>();

        if (itemNetworkObject == null ||
            !itemNetworkObject.IsSpawned)
            return;

        // Consume the bar so it cannot be counted twice.
        itemNetworkObject.Despawn(true);

        lastThrowerClientId = throwerId;

        PlayGoldEffectClientRpc();

        offeredBars.Value++;

        Debug.Log(
            $"{name}: {offeredBars.Value}/{CurrentPrice} gold bars offered. " +
            $"Last thrower: {throwerId}");

        if (offeredBars.Value >= CurrentPrice)
            TryGrantUpgrade();
    }

    private void TryGrantUpgrade()
    {
        if (!IsServer)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                lastThrowerClientId, out var client))
        {
            Debug.LogWarning(
                $"{name}: Last thrower is no longer connected.");
            return;
        }

        if (client.PlayerObject == null)
            return;

        PlayerUpgradeStats stats =
            client.PlayerObject.GetComponent<PlayerUpgradeStats>();

        if (stats == null)
        {
            Debug.LogWarning(
                $"{name}: PlayerUpgradeStats is missing from the player's NetworkObject.");
            return;
        }

        // Do not charge the player if the upgrade cannot be granted.
        if (!stats.TryGrantUpgrade(upgradeType))
        {
            Debug.Log($"{name}: Upgrade unavailable or already maxed.");
            return;
        }

        if (upgradeType == UpgradeType.Width)
        {
            NetworkedPlayer player =
                client.PlayerObject.GetComponent<NetworkedPlayer>();

            if (player != null)
                player.GrantCartWidthUpgrade();
        }

        // Successful upgrade: reset the offered bars.
        offeredBars.Value = 0;
        lastThrowerClientId = ulong.MaxValue;

        // Increase the price for the NEXT upgrade.
        networkBarsRequired.Value += Mathf.Max(1, priceIncreasePerUpgrade);

        Debug.Log(
            $"{name}: Upgrade granted! New price: {networkBarsRequired.Value} gold.");

        PlayUpgradeGrantedClientRpc();
    }

    [ClientRpc]
    private void PlayGoldEffectClientRpc()
    {
        if (fireEffect != null && !fireEffect.isPlaying)
            fireEffect.Play();

        if (!goldIntoFireEvent.IsNull)
        {
            RuntimeManager.PlayOneShot(
                goldIntoFireEvent,
                transform.position);
        }
    }

    [ClientRpc]
    private void PlayUpgradeGrantedClientRpc()
    {
        // Stop and reset the offering flames.
        if (fireEffect != null)
        {
            fireEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            fireEffect.transform.localScale = baseFireScale;
        }

        // Play the separate upgrade-success particle effect.
        if (upgradeGrantedEffect != null)
        {
            upgradeGrantedEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            upgradeGrantedEffect.Play();
        }

        if (!upgradeGrantedEvent.IsNull)
        {
            RuntimeManager.PlayOneShot(
                upgradeGrantedEvent,
                transform.position);
        }
    }
}