using System.Collections.Immutable;

namespace FolderFighter.Engine.Core;

/// <summary>
/// Defines all available spells and their effects.
/// </summary>
public static class SpellDefinitions
{
    public record SpellInfo(string Name, int Damage, int HealAmount, SpellType Type);

    public enum SpellType
    {
        Offensive,
        Defensive,
        Utility
    }

    private static readonly ImmutableDictionary<string, SpellInfo> Spells = new Dictionary<string, SpellInfo>
    {
        // Offensive spells
        ["Fireball"] = new("Fireball", Damage: 15, HealAmount: 0, SpellType.Offensive),
        ["IceBolt"] = new("IceBolt", Damage: 12, HealAmount: 0, SpellType.Offensive),
        ["Lightning"] = new("Lightning", Damage: 20, HealAmount: 0, SpellType.Offensive),
        ["ShadowStrike"] = new("ShadowStrike", Damage: 18, HealAmount: 0, SpellType.Offensive),
        ["ArcaneBlast"] = new("ArcaneBlast", Damage: 10, HealAmount: 0, SpellType.Offensive),

        // Defensive spells
        ["Shield"] = new("Shield", Damage: 0, HealAmount: 10, SpellType.Defensive),
        ["Heal"] = new("Heal", Damage: 0, HealAmount: 20, SpellType.Defensive),
        ["Barrier"] = new("Barrier", Damage: 0, HealAmount: 15, SpellType.Defensive),

        // Utility
        ["Reflect"] = new("Reflect", Damage: 5, HealAmount: 5, SpellType.Utility),
    }.ToImmutableDictionary();

    private static readonly ImmutableList<string> SpellPool = Spells.Keys.ToImmutableList();

    public static SpellInfo? GetSpell(string spellName)
    {
        // Normalize: remove .txt extension if present
        var normalizedName = spellName.Replace(".txt", "", StringComparison.OrdinalIgnoreCase);
        return Spells.GetValueOrDefault(normalizedName);
    }

    public static int GetDamage(string spellName)
    {
        return GetSpell(spellName)?.Damage ?? 5; // Default damage for unknown spells
    }

    public static int GetHealAmount(string spellName)
    {
        return GetSpell(spellName)?.HealAmount ?? 0;
    }

    public static string GetRandomSpellName(Random random)
    {
        return SpellPool[random.Next(SpellPool.Count)];
    }

    public static bool IsDefensiveSpell(string spellName)
    {
        var spell = GetSpell(spellName);
        return spell?.Type == SpellType.Defensive;
    }
}
