using Unity.Netcode;
using UnityEngine;

public class PlayerUpgradeStats : NetworkBehaviour
{
    [Header("Upgrade Limits")]
    [Min(1)]
    [SerializeField] private int maxUpgradeLevel = 5;

    [Tooltip("Percentage increase per upgrade level. 0.15 = 15%.")]
    [SerializeField] private float increasePerLevel = 0.25f;

    private readonly NetworkVariable<int> runningSpeedLevel =
        new NetworkVariable<int>(0);

    private readonly NetworkVariable<int> widthLevel =
        new NetworkVariable<int>(0);

    private readonly NetworkVariable<int> pickupSpeedLevel =
        new NetworkVariable<int>(0);

    private readonly NetworkVariable<int> throwSpeedLevel =
        new NetworkVariable<int>(0);

    public int RunningSpeedLevel => runningSpeedLevel.Value;
    public int WidthLevel => widthLevel.Value;
    public int PickupSpeedLevel => pickupSpeedLevel.Value;
    public int ThrowSpeedLevel => throwSpeedLevel.Value;

    public float RunningSpeedMultiplier =>
        1f + RunningSpeedLevel * increasePerLevel;

    public float JumpingHeightMultiplier => 1f + RunningSpeedLevel * increasePerLevel / 2;

    public float WidthMultiplier =>
        1f + WidthLevel * increasePerLevel;

    public float PickupSpeedMultiplier =>
        1f + PickupSpeedLevel * increasePerLevel;

    public float ThrowSpeedMultiplier =>
        1f + ThrowSpeedLevel * increasePerLevel;

    // Called by UpgradeBowl on the server.
    public bool TryGrantUpgrade(UpgradeBowl.UpgradeType type)
    {
        if (!IsServer)
            return false;

        switch (type)
        {
            case UpgradeBowl.UpgradeType.RunningSpeed:
                return IncreaseLevel(runningSpeedLevel);

            case UpgradeBowl.UpgradeType.Width:
                return IncreaseLevel(widthLevel);

            case UpgradeBowl.UpgradeType.PickupSpeed:
                return IncreaseLevel(pickupSpeedLevel);

            case UpgradeBowl.UpgradeType.ThrowSpeed:
                return IncreaseLevel(throwSpeedLevel);

            default:
                return false;
        }
    }

    private bool IncreaseLevel(NetworkVariable<int> level)
    {
        if (level.Value >= maxUpgradeLevel)
            return false;

        level.Value++;
        return true;
    }
}