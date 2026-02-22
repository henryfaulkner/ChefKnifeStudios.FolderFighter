using System.Collections.Immutable;

namespace FolderFighter.Engine.Core;

/// <summary>
/// Immutable game state following the Elm Architecture.
/// This is the single source of truth for the local player's game instance.
/// </summary>
public sealed record GameState
{
    public required string PlayerName { get; init; }
    public int Health { get; init; } = 100;
    public int MaxHealth { get; init; } = 100;
    public ImmutableDictionary<string, int> Opponents { get; init; } = ImmutableDictionary<string, int>.Empty;
    public ImmutableDictionary<string, ImmutableDictionary<ItemDefinitions.ItemSlot, string?>> OpponentEquipment { get; init; } =
        ImmutableDictionary<string, ImmutableDictionary<ItemDefinitions.ItemSlot, string?>>.Empty;
    public ImmutableQueue<string> CombatLogs { get; init; } = ImmutableQueue<string>.Empty;
    public ImmutableList<string> AvailableSpells { get; init; } = ImmutableList<string>.Empty;
    public ImmutableList<string> AvailableItems { get; init; } = ImmutableList<string>.Empty;
    public ImmutableDictionary<ItemDefinitions.ItemSlot, string?> EquippedItems { get; init; } =
        ImmutableDictionary<ItemDefinitions.ItemSlot, string?>.Empty
            .Add(ItemDefinitions.ItemSlot.Weapon, null)
            .Add(ItemDefinitions.ItemSlot.Armor, null)
            .Add(ItemDefinitions.ItemSlot.Accessory, null)
            .Add(ItemDefinitions.ItemSlot.Head, null);
    public int Gold { get; init; } = 0;
    public ImmutableList<string> ShopSpells { get; init; } = ImmutableList<string>.Empty;
    public ImmutableList<string> ShopItems { get; init; } = ImmutableList<string>.Empty;
    public ImmutableDictionary<string, int> OpponentGold { get; init; } = ImmutableDictionary<string, int>.Empty;
    public bool IsGameOver => Health <= 0;

    public static GameState Create(string playerName) => new()
    {
        PlayerName = playerName
    };
}
