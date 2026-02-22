namespace FolderFighter.Engine.Core;

/// <summary>
/// Defines shop pricing and economy constants.
/// </summary>
public static class ShopDefinitions
{
    // ==========================================
    // Economy Constants
    // ==========================================

    /// <summary>
    /// Gold earned per damage dealt to opponents.
    /// </summary>
    public const int GoldPerDamage = 1;

    /// <summary>
    /// Gold earned passively on each shop refresh tick.
    /// </summary>
    public const int PassiveGoldPerTick = 5;

    // ==========================================
    // Spell Prices
    // ==========================================

    private static readonly Dictionary<string, int> SpellPrices = new()
    {
        { "Fireball", 30 },
        { "Frostbolt", 25 },
        { "HealingRay", 20 },
        { "Meteor", 50 },
        { "Blizzard", 40 },
        { "Cure", 15 }
    };

    // ==========================================
    // Item Prices
    // ==========================================

    private static readonly Dictionary<string, int> ItemPrices = new()
    {
        { "IronSword", 40 },
        { "SteelArmor", 50 },
        { "SilverRing", 25 },
        { "CrownOfWisdom", 60 },
        { "FlamebrandSword", 75 },
        { "MithrilPlate", 100 },
        { "RuneRing", 45 },
        { "Tiara", 55 },
        { "VampiricDagger", 85 },
        { "ShadowCloak", 70 },
        { "EmeraldNecklace", 35 },
        { "GoldenCirclet", 80 }
    };

    // ==========================================
    // Helper Methods
    // ==========================================

    /// <summary>
    /// Get the gold cost of a spell, or 0 if not found.
    /// </summary>
    public static int GetSpellCost(string spellName)
    {
        return SpellPrices.TryGetValue(spellName, out var cost) ? cost : 0;
    }

    /// <summary>
    /// Get the gold cost of an item, or 0 if not found.
    /// </summary>
    public static int GetItemCost(string itemName)
    {
        return ItemPrices.TryGetValue(itemName, out var cost) ? cost : 0;
    }

    /// <summary>
    /// Get all available spell names for the shop.
    /// </summary>
    public static List<string> GetAllSpellNames() => new(SpellPrices.Keys);

    /// <summary>
    /// Get all available item names for the shop.
    /// </summary>
    public static List<string> GetAllItemNames() => new(ItemPrices.Keys);
}
