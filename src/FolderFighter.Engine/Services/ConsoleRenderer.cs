using FolderFighter.Engine.Core;

namespace FolderFighter.Engine.Services;

/// <summary>
/// Pure console renderer following the Elm Architecture "View" pattern.
/// Renders the game state to the console - no side effects except display.
/// </summary>
public sealed class ConsoleRenderer
{
    private readonly object _renderLock = new();
    private int _lastRenderHash;

    /// <summary>
    /// Render the current game state to the console.
    /// Only re-renders if state has changed.
    /// </summary>
    public void Render(GameState state)
    {
        var currentHash = ComputeStateHash(state);
        if (currentHash == _lastRenderHash)
        {
            return;
        }

        lock (_renderLock)
        {
            _lastRenderHash = currentHash;
            RenderInternal(state);
        }
    }

    private void RenderInternal(GameState state)
    {
        Console.Clear();

        RenderHeader(state);
        RenderHealthBar(state);
        RenderGold(state);
        RenderEquipment(state);
        RenderShop(state);
        RenderSpells(state);
        RenderItems(state);
        RenderOpponents(state);
        RenderCombatLog(state);
        RenderFooter();
    }

    private void RenderHeader(GameState state)
    {
        var title = state.IsGameOver ? "GAME OVER" : "FOLDER FIGHTER";
        Console.ForegroundColor = state.IsGameOver ? ConsoleColor.Red : ConsoleColor.Cyan;
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"  {title}");
        Console.WriteLine(new string('=', 50));
        Console.ResetColor();
    }

    private void RenderHealthBar(GameState state)
    {
        Console.WriteLine();
        Console.Write($"  PLAYER: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(state.PlayerName.PadRight(20));
        Console.ResetColor();

        RenderHealthIndicator(state.Health, state.MaxHealth);
        Console.WriteLine();
    }

    private void RenderHealthIndicator(int health, int maxHealth)
    {
        var percentage = (double)health / maxHealth;
        var barLength = 20;
        var filledLength = (int)(percentage * barLength);

        Console.Write(" HP: [");

        Console.ForegroundColor = percentage switch
        {
            > 0.6 => ConsoleColor.Green,
            > 0.3 => ConsoleColor.Yellow,
            _ => ConsoleColor.Red
        };

        Console.Write(new string('#', filledLength));
        Console.Write(new string('-', barLength - filledLength));
        Console.ResetColor();

        Console.Write($"] {health}/{maxHealth}");
    }

    private void RenderGold(GameState state)
    {
        Console.WriteLine();
        Console.Write("  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("💰 GOLD: ");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"{state.Gold}g");
        Console.ResetColor();
    }

    private void RenderEquipment(GameState state)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  EQUIPMENT:");
        Console.ResetColor();

        var hasEquipped = false;
        foreach (var (slot, itemName) in state.EquippedItems)
        {
            if (itemName != null && ItemDefinitions.GetItem(itemName) is { } itemInfo)
            {
                hasEquipped = true;
                var slotIcon = slot switch
                {
                    ItemDefinitions.ItemSlot.Weapon => "🗡️",
                    ItemDefinitions.ItemSlot.Armor => "🛡️",
                    ItemDefinitions.ItemSlot.Accessory => "💍",
                    ItemDefinitions.ItemSlot.Head => "👑",
                    _ => "?"
                };

                Console.Write($"    {slotIcon}  {itemName}");
                if (itemInfo.DamageModifier > 1.0f)
                    Console.Write($" (+{(itemInfo.DamageModifier - 1) * 100:F0}% dmg)");
                if (itemInfo.ArmorValue > 0)
                    Console.Write($" ({itemInfo.ArmorValue} armor)");
                if (itemInfo.PassiveHealth > 0)
                    Console.Write($" (+{itemInfo.PassiveHealth} HP)");
                Console.WriteLine();
            }
        }

        if (!hasEquipped)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("    (nothing equipped)");
            Console.ResetColor();
        }
    }

    private void RenderItems(GameState state)
    {
        if (state.AvailableItems.IsEmpty)
            return;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  AVAILABLE ITEMS:");
        Console.ResetColor();

        foreach (var item in state.AvailableItems)
        {
            Console.Write("    📦 ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{item}.item");
            Console.ResetColor();
        }
    }

    private void RenderShop(GameState state)
    {
        if (state.ShopSpells.IsEmpty && state.ShopItems.IsEmpty)
            return;

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  SHOP ROTATION:");
        Console.ResetColor();

        if (!state.ShopSpells.IsEmpty)
        {
            foreach (var spell in state.ShopSpells)
            {
                var cost = ShopDefinitions.GetSpellCost(spell);
                Console.Write("    ✨ ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{spell}.txt");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($" ({cost}g)");
                Console.ResetColor();
            }
        }

        if (!state.ShopItems.IsEmpty)
        {
            foreach (var item in state.ShopItems)
            {
                var cost = ShopDefinitions.GetItemCost(item);
                Console.Write("    💎 ");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"{item}.item");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($" ({cost}g)");
                Console.ResetColor();
            }
        }
    }

    private void RenderSpells(GameState state)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("  AVAILABLE SPELLS:");
        Console.ResetColor();

        if (state.AvailableSpells.IsEmpty)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("    (no spells available - wait for new draws)");
            Console.ResetColor();
        }
        else
        {
            foreach (var spell in state.AvailableSpells)
            {
                var spellInfo = SpellDefinitions.GetSpell(spell);
                var icon = spellInfo?.Type switch
                {
                    SpellDefinitions.SpellType.Offensive => "*",
                    SpellDefinitions.SpellType.Defensive => "+",
                    _ => "?"
                };

                Console.Write("    ");
                Console.ForegroundColor = spellInfo?.Type == SpellDefinitions.SpellType.Defensive
                    ? ConsoleColor.Green
                    : ConsoleColor.Red;
                Console.Write($"{icon} ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{spell}.txt");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($" (DMG:{spellInfo?.Damage ?? 0} HEAL:{spellInfo?.HealAmount ?? 0})");
                Console.ResetColor();
            }
        }
    }

    private void RenderOpponents(GameState state)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("  ENEMIES:");
        Console.ResetColor();

        if (state.Opponents.IsEmpty)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("    (waiting for opponents to join...)");
            Console.ResetColor();
        }
        else
        {
            foreach (var (name, health) in state.Opponents.OrderBy(o => o.Key))
            {
                Console.Write($"    - {name.PadRight(20)}");
                RenderHealthIndicator(health, 100);

                // Show opponent gold if available
                if (state.OpponentGold.TryGetValue(name, out var gold))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($" 💰{gold}g");
                    Console.ResetColor();
                }

                Console.WriteLine();
            }
        }
    }

    private void RenderCombatLog(GameState state)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  COMBAT LOG:");
        Console.ResetColor();
        Console.WriteLine(new string('-', 50));

        if (state.CombatLogs.IsEmpty)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("    (no activity yet)");
            Console.ResetColor();
        }
        else
        {
            foreach (var log in state.CombatLogs)
            {
                Console.Write("    ");
                RenderLogEntry(log);
                Console.WriteLine();
            }
        }
    }

    private void RenderLogEntry(string log)
    {
        // Color-code based on log type
        var color = log switch
        {
            _ when log.Contains("[CAST]") => ConsoleColor.Cyan,
            _ when log.Contains("[HIT]") => ConsoleColor.Red,
            _ when log.Contains("[BUFF]") => ConsoleColor.Green,
            _ when log.Contains("[DMG]") => ConsoleColor.Yellow,
            _ when log.Contains("[KILL]") => ConsoleColor.Magenta,
            _ when log.Contains("[JOIN]") => ConsoleColor.Blue,
            _ when log.Contains("[LEFT]") => ConsoleColor.DarkGray,
            _ when log.Contains("[DRAW]") => ConsoleColor.DarkCyan,
            _ when log.Contains("[DEFEAT]") => ConsoleColor.DarkRed,
            _ => ConsoleColor.Gray
        };

        Console.ForegroundColor = color;
        Console.Write(log);
        Console.ResetColor();
    }

    private void RenderFooter()
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 50));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Drag spell files into opponent folders to attack!");
        Console.WriteLine("  Drop into .self folder for defensive buffs.");
        Console.WriteLine("  Press Ctrl+C to exit.");
        Console.ResetColor();
        Console.WriteLine(new string('=', 50));
    }

    private static int ComputeStateHash(GameState state)
    {
        var hash = HashCode.Combine(
            state.PlayerName,
            state.Health,
            state.MaxHealth,
            state.Gold,
            state.Opponents.Count,
            state.CombatLogs.Count(),
            state.AvailableSpells.Count,
            state.AvailableItems.Count
        );

        hash = HashCode.Combine(hash, state.ShopSpells.Count, state.ShopItems.Count);

        foreach (var (name, health) in state.Opponents)
        {
            hash = HashCode.Combine(hash, name, health);
        }

        foreach (var (name, gold) in state.OpponentGold)
        {
            hash = HashCode.Combine(hash, name, gold);
        }

        return hash;
    }
}
