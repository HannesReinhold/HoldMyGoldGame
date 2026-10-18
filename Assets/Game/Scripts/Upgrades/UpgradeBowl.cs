using Unity.Netcode;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public class UpgradeBowl : NetworkBehaviour
{
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

    [Header("World Space UI")]
    [SerializeField] private TMP_Text upgradeText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text offeredText;

    [Header("Networked Effects")]
    [SerializeField] private ParticleSystem fireEffect;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip goldIntoFireSound;
    [SerializeField] private AudioClip upgradeGrantedSound;

    private readonly NetworkVariable<int> offeredBars =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    // The last player who threw a gold bar into the bowl.
    private ulong lastThrowerClientId = ulong.MaxValue;

    public int OfferedBars => offeredBars.Value;

    public override void OnNetworkSpawn()
    {
        offeredBars.OnValueChanged += OnOfferedBarsChanged;

        GetComponent<Collider>().isTrigger = true;

        UpdateUI(offeredBars.Value);
    }

    public override void OnNetworkDespawn()
    {
        offeredBars.OnValueChanged -= OnOfferedBarsChanged;
    }

    private void OnOfferedBarsChanged(int previous, int current)
    {
        UpdateUI(current);
    }

    private void UpdateUI(int count)
    {
        if (upgradeText != null)
            upgradeText.text = upgradeType.ToString();

        if (priceText != null)
            priceText.text = $"Price: {barsRequired} gold";

        if (offeredText != null)
            offeredText.text = $"{count} / {barsRequired}";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned)
            return;

        // Only count actual thrown gold bars.
        ItemHitDamage item =
            other.GetComponentInParent<ItemHitDamage>();

        if (item == null || !item.armed)
            return;

        ulong throwerId = item.throwerClientId;

        // Dropped gold without a recorded thrower doesn't count.
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

        // Play the gold/fire effect on every client.
        PlayGoldEffectClientRpc();

        offeredBars.Value++;

        Debug.Log(
            $"{name}: {offeredBars.Value}/{barsRequired} gold bars offered. " +
            $"Last thrower: {throwerId}");

        if (offeredBars.Value >= barsRequired)
        {
            TryGrantUpgrade();
        }
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

        // If the player has reached the maximum level, keep the
        // offered amount so the bowl doesn't silently lose the payment.
        if (!stats.TryGrantUpgrade(upgradeType))
        {
            Debug.Log($"{name}: Upgrade unavailable or already maxed.");
            return;
        }

        // Successful purchase: reset the bowl and play the reward sound.
        offeredBars.Value = 0;
        lastThrowerClientId = ulong.MaxValue;

        PlayUpgradeGrantedClientRpc();
    }

    [ClientRpc]
    private void PlayGoldEffectClientRpc()
    {
        if (fireEffect != null)
            fireEffect.Play();

        if (audioSource != null && goldIntoFireSound != null)
        {
            audioSource.PlayOneShot(goldIntoFireSound);
        }
    }

    [ClientRpc]
    private void PlayUpgradeGrantedClientRpc()
    {
        if (audioSource != null && upgradeGrantedSound != null)
        {
            audioSource.PlayOneShot(upgradeGrantedSound);
        }
    }
}