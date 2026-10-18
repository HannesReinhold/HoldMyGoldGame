using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-side bookkeeping of what currently lies on a trigger plate.
/// Players and items have several colliders, so each object is reference-counted
/// per collider instead of adding and subtracting weight on every trigger event.
/// Objects that vanish without an OnTriggerExit (items that get picked up, players
/// that disconnect) are pruned whenever the weight is recomputed.
/// </summary>
public class PlateOccupants
{
    private readonly Dictionary<NetworkedPlayer, int> players = new Dictionary<NetworkedPlayer, int>();
    private readonly Dictionary<PickableItem, int> items = new Dictionary<PickableItem, int>();
    private readonly List<NetworkedPlayer> playerBuffer = new List<NetworkedPlayer>();
    private readonly List<PickableItem> itemBuffer = new List<PickableItem>();

    public bool Any => players.Count > 0 || items.Count > 0;

    public void Clear()
    {
        players.Clear();
        items.Clear();
    }

    public void OnEnter(Collider other)
    {
        NetworkedPlayer player = other.GetComponentInParent<NetworkedPlayer>();
        if (player != null)
        {
            Add(players, player);
            return;
        }

        PickableItem item = other.GetComponentInParent<PickableItem>();
        if (item != null)
            Add(items, item);
    }

    public void OnExit(Collider other)
    {
        NetworkedPlayer player = other.GetComponentInParent<NetworkedPlayer>();
        if (player != null)
        {
            Remove(players, player);
            return;
        }

        PickableItem item = other.GetComponentInParent<PickableItem>();
        if (item != null)
            Remove(items, item);
    }

    // Current weight: one unit per gold bar a player carries, itemWeight per loose item.
    public float ComputeWeight(float itemWeight)
    {
        float weight = 0f;

        playerBuffer.Clear();
        foreach (var entry in players)
        {
            NetworkedPlayer player = entry.Key;
            if (player == null || !player.IsSpawned)
            {
                playerBuffer.Add(player);
                continue;
            }

            weight += player.GoldBarCount;
        }
        foreach (var player in playerBuffer)
            players.Remove(player);

        itemBuffer.Clear();
        foreach (var entry in items)
        {
            PickableItem item = entry.Key;
            if (item == null || !item.IsSpawned)
            {
                itemBuffer.Add(item);
                continue;
            }

            weight += itemWeight;
        }
        foreach (var item in itemBuffer)
            items.Remove(item);

        return weight;
    }

    private static void Add<T>(Dictionary<T, int> set, T occupant) where T : Component
    {
        set.TryGetValue(occupant, out int colliders);
        set[occupant] = colliders + 1;
    }

    private static void Remove<T>(Dictionary<T, int> set, T occupant) where T : Component
    {
        if (!set.TryGetValue(occupant, out int colliders))
            return;

        if (colliders <= 1)
            set.Remove(occupant);
        else
            set[occupant] = colliders - 1;
    }
}
