using System.Collections.Immutable;

namespace FolderFighter.Engine.Core;

/// <summary>
/// Base message type for the Elm Architecture.
/// All game events are represented as messages.
/// </summary>
public abstract record Msg;

// ==========================================
// File System Driven Messages (Local Input)
// ==========================================

/// <summary>
/// Player cast a spell by dragging a spell file into an opponent's directory.
/// </summary>
public sealed record SpellCast(string SpellName, string TargetName) : Msg;

/// <summary>
/// Player used a spell on themselves (defensive/buff) by dropping into .self directory.
/// </summary>
public sealed record SelfCast(string SpellName) : Msg;

// ==========================================
// Network Driven Messages (Azure Storage Queue)
// ==========================================

/// <summary>
/// Received an attack from another player via the network.
/// </summary>
public sealed record SpellReceived(string Attacker, string SpellName, int Damage) : Msg;

/// <summary>
/// A new opponent joined the battle arena.
/// </summary>
public sealed record OpponentJoined(string OpponentName) : Msg;

/// <summary>
/// An opponent left the arena (disconnected or defeated).
/// </summary>
public sealed record OpponentLeft(string OpponentName) : Msg;

/// <summary>
/// Opponent's health changed (they took damage or healed).
/// </summary>
public sealed record OpponentHealthChanged(string OpponentName, int CurrentHealth, int MaxHealth) : Msg;

// ==========================================
// Timer Driven Messages (System Events)
// ==========================================

/// <summary>
/// A new spell has been generated and placed in the player's directory.
/// </summary>
public sealed record SpellDrawn(string SpellName) : Msg;

/// <summary>
/// A spell file was consumed (used or expired).
/// </summary>
public sealed record SpellConsumed(string SpellName) : Msg;

/// <summary>
/// A new item has been generated and placed in the player's directory.
/// </summary>
public sealed record ItemDrawn(string ItemName) : Msg;

/// <summary>
/// An item file was consumed (picked up/equipped).
/// </summary>
public sealed record ItemConsumed(string ItemName) : Msg;

/// <summary>
/// Player equipped an item in a slot (dropped into .self folder).
/// </summary>
public sealed record ItemEquipped(ItemDefinitions.ItemSlot Slot, string ItemName) : Msg;

/// <summary>
/// Opponent's equipped items changed.
/// </summary>
public sealed record OpponentEquipmentChanged(string OpponentName, ImmutableDictionary<ItemDefinitions.ItemSlot, string?> EquippedItems) : Msg;

// ==========================================
// Economy Messages
// ==========================================

/// <summary>
/// Player earned gold from landing hits or passive trickle.
/// </summary>
public sealed record GoldEarned(int Amount) : Msg;

/// <summary>
/// Shop inventory has been refreshed with new selections.
/// </summary>
public sealed record ShopRefreshed(ImmutableList<string> SpellNames, ImmutableList<string> ItemNames) : Msg;

/// <summary>
/// Player purchased a spell from the shop.
/// </summary>
public sealed record SpellPurchased(string SpellName, int Cost) : Msg;

/// <summary>
/// Player purchased an item from the shop.
/// </summary>
public sealed record ItemPurchased(string ItemName, int Cost) : Msg;

/// <summary>
/// Opponent's gold amount changed (network update).
/// </summary>
public sealed record OpponentGoldChanged(string OpponentName, int Gold) : Msg;

// ==========================================
// Game Flow Messages
// ==========================================

/// <summary>
/// Initialize the game with player name.
/// </summary>
public sealed record GameStarted(string PlayerName) : Msg;

/// <summary>
/// Clear old combat logs to keep the view clean.
/// </summary>
public sealed record LogsPruned(int KeepCount) : Msg;
