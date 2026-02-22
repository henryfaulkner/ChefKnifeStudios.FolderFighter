using System.Collections.Immutable;

namespace FolderFighter.Engine.Core;

/// <summary>
/// Pure update function following the Elm Architecture.
/// Takes the current state and a message, returns a new state.
/// No side effects occur here - this is pure business logic.
/// </summary>
public static class Update
{
    private const int MaxCombatLogs = 10;

    /// <summary>
    /// Process a message and return the new game state.
    /// This function is pure - no side effects, no I/O.
    /// </summary>
    public static GameState Apply(Msg msg, GameState state) => msg switch
    {
        GameStarted g => GameState.Create(g.PlayerName),

        SpellCast s => HandleSpellCast(state, s),

        SelfCast s => HandleSelfCast(state, s),

        SpellReceived r => HandleSpellReceived(state, r),

        OpponentJoined o => HandleOpponentJoined(state, o),

        OpponentLeft o => HandleOpponentLeft(state, o),

        OpponentHealthChanged h => HandleOpponentHealthChanged(state, h),

        ItemDrawn d => state with
        {
            AvailableItems = state.AvailableItems.Add(d.ItemName),
            CombatLogs = EnqueueLog(state.CombatLogs, $"[ITEM] New item appeared: {d.ItemName}")
        },

        ItemConsumed c => state with
        {
            AvailableItems = state.AvailableItems.Remove(c.ItemName)
        },

        ItemEquipped e => HandleItemEquipped(state, e),

        OpponentEquipmentChanged o => HandleOpponentEquipmentChanged(state, o),

        SpellDrawn d => state with
        {
            AvailableSpells = state.AvailableSpells.Add(d.SpellName),
            CombatLogs = EnqueueLog(state.CombatLogs, $"[DRAW] New spell appeared: {d.SpellName}")
        },

        SpellConsumed c => state with
        {
            AvailableSpells = state.AvailableSpells.Remove(c.SpellName)
        },

        LogsPruned p => state with
        {
            CombatLogs = PruneLogs(state.CombatLogs, p.KeepCount)
        },

        GoldEarned g => state with
        {
            Gold = state.Gold + g.Amount
        },

        ShopRefreshed s => state with
        {
            ShopSpells = s.SpellNames,
            ShopItems = s.ItemNames
        },

        SpellPurchased p => HandleSpellPurchased(state, p),

        ItemPurchased p => HandleItemPurchased(state, p),

        OpponentGoldChanged g => state with
        {
            OpponentGold = state.OpponentGold.SetItem(g.OpponentName, g.Gold)
        },

        _ => state
    };

    private static GameState HandleSpellCast(GameState state, SpellCast cast)
    {
        var baseDamage = SpellDefinitions.GetDamage(cast.SpellName);

        // Apply item damage modifiers
        var damageModifier = ItemDefinitions.CalculateTotalDamageModifier(state.EquippedItems);
        var damage = (int)(baseDamage * damageModifier);

        var log = damageModifier > 1.0f
            ? $"[CAST] You cast {cast.SpellName} at {cast.TargetName} for {baseDamage} → {damage} damage!"
            : $"[CAST] You cast {cast.SpellName} at {cast.TargetName} for {damage} damage!";

        return state with
        {
            AvailableSpells = state.AvailableSpells.Remove(cast.SpellName),
            CombatLogs = EnqueueLog(state.CombatLogs, log)
        };
    }

    private static GameState HandleSelfCast(GameState state, SelfCast cast)
    {
        var healAmount = SpellDefinitions.GetHealAmount(cast.SpellName);
        var newHealth = Math.Min(state.Health + healAmount, state.MaxHealth);
        var log = $"[BUFF] You used {cast.SpellName} on yourself (+{healAmount} HP)";

        return state with
        {
            Health = newHealth,
            AvailableSpells = state.AvailableSpells.Remove(cast.SpellName),
            CombatLogs = EnqueueLog(state.CombatLogs, log)
        };
    }

    private static GameState HandleSpellReceived(GameState state, SpellReceived received)
    {
        var baseDamage = received.Damage;

        // Apply item armor (flat reduction)
        var armor = ItemDefinitions.CalculateTotalArmor(state.EquippedItems);
        var damageAfterArmor = Math.Max(1, baseDamage - armor); // Minimum 1 damage

        // Apply item resistances (multiplier based on spell type)
        var spell = SpellDefinitions.GetSpell(received.SpellName);
        var resistanceMultiplier = spell != null
            ? ItemDefinitions.GetResistanceMultiplier(state.EquippedItems, spell.Type)
            : 1.0f;

        var finalDamage = (int)(damageAfterArmor * resistanceMultiplier);
        var newHealth = Math.Max(0, state.Health - finalDamage);

        var log = armor > 0 || resistanceMultiplier < 1.0f
            ? $"[HIT] {received.Attacker} hit you with {received.SpellName} for {baseDamage} damage ({finalDamage} after armor/resist)!"
            : $"[HIT] {received.Attacker} hit you with {received.SpellName} for {finalDamage} damage!";

        var newState = state with
        {
            Health = newHealth,
            CombatLogs = EnqueueLog(state.CombatLogs, log)
        };

        if (newState.IsGameOver)
        {
            return newState with
            {
                CombatLogs = EnqueueLog(newState.CombatLogs, "[DEFEAT] You have been defeated!")
            };
        }

        return newState;
    }

    private static GameState HandleOpponentJoined(GameState state, OpponentJoined joined)
    {
        if (state.Opponents.ContainsKey(joined.OpponentName))
        {
            return state; // Already tracking this opponent
        }

        var log = $"[JOIN] {joined.OpponentName} entered the arena!";

        return state with
        {
            Opponents = state.Opponents.Add(joined.OpponentName, 100),
            CombatLogs = EnqueueLog(state.CombatLogs, log)
        };
    }

    private static GameState HandleOpponentLeft(GameState state, OpponentLeft left)
    {
        if (!state.Opponents.ContainsKey(left.OpponentName))
        {
            return state;
        }

        var log = $"[LEFT] {left.OpponentName} left the arena.";

        return state with
        {
            Opponents = state.Opponents.Remove(left.OpponentName),
            CombatLogs = EnqueueLog(state.CombatLogs, log)
        };
    }

    private static GameState HandleOpponentHealthChanged(GameState state, OpponentHealthChanged health)
    {
        if (!state.Opponents.ContainsKey(health.OpponentName))
        {
            return state;
        }

        var oldHealth = state.Opponents[health.OpponentName];
        var damage = oldHealth - health.CurrentHealth;

        var log = damage > 0
            ? $"[DMG] {health.OpponentName} took {damage} damage (HP: {health.CurrentHealth}/{health.MaxHealth})"
            : $"[HEAL] {health.OpponentName} healed (HP: {health.CurrentHealth}/{health.MaxHealth})";

        var newState = state with
        {
            Opponents = state.Opponents.SetItem(health.OpponentName, health.CurrentHealth),
            CombatLogs = EnqueueLog(state.CombatLogs, log)
        };

        if (health.CurrentHealth <= 0)
        {
            return newState with
            {
                Opponents = newState.Opponents.Remove(health.OpponentName),
                CombatLogs = EnqueueLog(newState.CombatLogs, $"[KILL] {health.OpponentName} has been defeated!")
            };
        }

        return newState;
    }

    private static GameState HandleItemEquipped(GameState state, ItemEquipped equipped)
    {
        if (ItemDefinitions.GetItem(equipped.ItemName) is not { } itemInfo)
        {
            return state;
        }

        var newEquipped = state.EquippedItems.SetItem(equipped.Slot, equipped.ItemName);
        var newMaxHealth = 100 + ItemDefinitions.CalculatePassiveHealth(newEquipped);
        var healthBoost = newMaxHealth - state.MaxHealth;

        var log = $"[EQUIP] {equipped.ItemName} equipped to {equipped.Slot}";

        return state with
        {
            EquippedItems = newEquipped,
            MaxHealth = newMaxHealth,
            Health = Math.Min(state.Health + healthBoost, newMaxHealth),
            AvailableItems = state.AvailableItems.Remove(equipped.ItemName),
            CombatLogs = EnqueueLog(state.CombatLogs, log)
        };
    }

    private static GameState HandleOpponentEquipmentChanged(GameState state, OpponentEquipmentChanged equipment)
    {
        if (!state.Opponents.ContainsKey(equipment.OpponentName))
        {
            return state;
        }

        return state with
        {
            OpponentEquipment = state.OpponentEquipment.SetItem(equipment.OpponentName, equipment.EquippedItems)
        };
    }

    private static ImmutableQueue<string> EnqueueLog(ImmutableQueue<string> logs, string message)
    {
        var timestamped = $"{DateTime.Now:HH:mm:ss} {message}";
        var newLogs = logs.Enqueue(timestamped);

        // Keep only the last MaxCombatLogs entries
        while (newLogs.Count() > MaxCombatLogs)
        {
            newLogs = newLogs.Dequeue(out _);
        }

        return newLogs;
    }

    private static ImmutableQueue<string> PruneLogs(ImmutableQueue<string> logs, int keepCount)
    {
        while (logs.Count() > keepCount)
        {
            logs = logs.Dequeue(out _);
        }
        return logs;
    }

    private static GameState HandleSpellPurchased(GameState state, SpellPurchased purchase)
    {
        if (state.Gold < purchase.Cost)
        {
            return state; // Can't afford, state unchanged
        }

        return state with
        {
            Gold = state.Gold - purchase.Cost,
            AvailableSpells = state.AvailableSpells.Add(purchase.SpellName),
            CombatLogs = EnqueueLog(state.CombatLogs, $"[BUY] Purchased {purchase.SpellName} for {purchase.Cost}g")
        };
    }

    private static GameState HandleItemPurchased(GameState state, ItemPurchased purchase)
    {
        if (state.Gold < purchase.Cost)
        {
            return state; // Can't afford, state unchanged
        }

        return state with
        {
            Gold = state.Gold - purchase.Cost,
            AvailableItems = state.AvailableItems.Add(purchase.ItemName),
            CombatLogs = EnqueueLog(state.CombatLogs, $"[BUY] Purchased {purchase.ItemName} for {purchase.Cost}g")
        };
    }
}
