using System.Collections.Immutable;

namespace FolderFighter.Engine.Core;

/// <summary>
/// Defines all available items and their effects.
/// </summary>
public static class ItemDefinitions
{
    public enum ItemSlot
    {
        Weapon,
        Armor,
        Accessory,
        Head
    }

    public record ItemInfo(
        string Name,
        ItemSlot Slot,
        float DamageModifier,      // e.g., 1.1 = +10% damage
        int ArmorValue,            // flat damage reduction
        ImmutableDictionary<SpellDefinitions.SpellType, float> TypeResistances, // e.g., Fire -> 0.8 = 20% resist
        int PassiveHealth           // bonus max health
    );

    private static readonly ImmutableDictionary<string, ItemInfo> Items = new Dictionary<string, ItemInfo>
    {
        // Weapons
        ["IronSword"] = new("IronSword", ItemSlot.Weapon, DamageModifier: 1.1f, ArmorValue: 0,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 0),

        ["SteelCleaver"] = new("SteelCleaver", ItemSlot.Weapon, DamageModifier: 1.15f, ArmorValue: 0,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 0),

        ["FlameScimitar"] = new("FlameScimitar", ItemSlot.Weapon, DamageModifier: 1.12f, ArmorValue: 0,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 0),

        // Armor
        ["LeatherChest"] = new("LeatherChest", ItemSlot.Armor, DamageModifier: 1.0f, ArmorValue: 5,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 10),

        ["IronPlate"] = new("IronPlate", ItemSlot.Armor, DamageModifier: 1.0f, ArmorValue: 10,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 15),

        ["MithrilArmor"] = new("MithrilArmor", ItemSlot.Armor, DamageModifier: 1.0f, ArmorValue: 12,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 20),

        // Accessories
        ["RingOfFire"] = new("RingOfFire", ItemSlot.Accessory, DamageModifier: 1.0f, ArmorValue: 0,
            TypeResistances: new Dictionary<SpellDefinitions.SpellType, float>
            {
                [SpellDefinitions.SpellType.Offensive] = 0.9f  // 10% resistance to offensive spells
            }.ToImmutableDictionary(), PassiveHealth: 0),

        ["RingOfIce"] = new("RingOfIce", ItemSlot.Accessory, DamageModifier: 1.05f, ArmorValue: 0,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 5),

        ["RingOfPower"] = new("RingOfPower", ItemSlot.Accessory, DamageModifier: 1.08f, ArmorValue: 0,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 0),

        // Head
        ["Crown"] = new("Crown", ItemSlot.Head, DamageModifier: 1.0f, ArmorValue: 3,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 25),

        ["Helmet"] = new("Helmet", ItemSlot.Head, DamageModifier: 1.0f, ArmorValue: 8,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 10),

        ["VizardHat"] = new("VizardHat", ItemSlot.Head, DamageModifier: 1.06f, ArmorValue: 0,
            TypeResistances: ImmutableDictionary<SpellDefinitions.SpellType, float>.Empty, PassiveHealth: 15),
    }.ToImmutableDictionary();

    private static readonly ImmutableList<string> ItemPool = Items.Keys.ToImmutableList();

    public static ItemInfo? GetItem(string itemName)
    {
        // Normalize: remove .item extension if present
        var normalizedName = itemName.Replace(".item", "", StringComparison.OrdinalIgnoreCase);
        return Items.GetValueOrDefault(normalizedName);
    }

    public static string GetRandomItemName(Random random)
    {
        return ItemPool[random.Next(ItemPool.Count)];
    }

    public static ImmutableList<string> GetItemsBySlot(ItemSlot slot)
    {
        return Items.Values
            .Where(info => info.Slot == slot)
            .Select(info => info.Name)
            .ToImmutableList();
    }

    /// <summary>
    /// Calculate total damage modifier from all equipped items.
    /// </summary>
    public static float CalculateTotalDamageModifier(ImmutableDictionary<ItemSlot, string?> equipped)
    {
        var modifier = 1.0f;
        foreach (var item in equipped.Values)
        {
            if (item != null && GetItem(item) is { } itemInfo)
            {
                modifier *= itemInfo.DamageModifier;
            }
        }
        return modifier;
    }

    /// <summary>
    /// Calculate total armor value from all equipped items.
    /// </summary>
    public static int CalculateTotalArmor(ImmutableDictionary<ItemSlot, string?> equipped)
    {
        var armor = 0;
        foreach (var item in equipped.Values)
        {
            if (item != null && GetItem(item) is { } itemInfo)
            {
                armor += itemInfo.ArmorValue;
            }
        }
        return armor;
    }

    /// <summary>
    /// Calculate passive health bonus from all equipped items.
    /// </summary>
    public static int CalculatePassiveHealth(ImmutableDictionary<ItemSlot, string?> equipped)
    {
        var health = 0;
        foreach (var item in equipped.Values)
        {
            if (item != null && GetItem(item) is { } itemInfo)
            {
                health += itemInfo.PassiveHealth;
            }
        }
        return health;
    }

    /// <summary>
    /// Get resistance multiplier for a spell type (1.0 = no resistance, 0.8 = 20% resistance).
    /// </summary>
    public static float GetResistanceMultiplier(ImmutableDictionary<ItemSlot, string?> equipped, SpellDefinitions.SpellType spellType)
    {
        var multiplier = 1.0f;
        foreach (var item in equipped.Values)
        {
            if (item != null && GetItem(item) is { } itemInfo)
            {
                if (itemInfo.TypeResistances.TryGetValue(spellType, out var resistance))
                {
                    multiplier *= resistance;
                }
            }
        }
        return multiplier;
    }
}
