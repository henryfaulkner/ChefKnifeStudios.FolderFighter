using System.Text.Json;
using System.Text.Json.Serialization;

namespace FolderFighter.Engine.Services;

/// <summary>
/// Message types sent over the network via Azure Service Bus.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AttackMessage), "attack")]
[JsonDerivedType(typeof(JoinMessage), "join")]
[JsonDerivedType(typeof(LeaveMessage), "leave")]
[JsonDerivedType(typeof(HealthChangedMessage), "health_changed")]
[JsonDerivedType(typeof(ItemsStateMessage), "items_state")]
[JsonDerivedType(typeof(GoldStateMessage), "gold_state")]
public abstract record NetworkMessage
{
    public required string SenderId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Sent when a player casts a spell at another player.
/// </summary>
public sealed record AttackMessage : NetworkMessage
{
    public required string TargetId { get; init; }
    public required string SpellName { get; init; }
    public required int Damage { get; init; }
}

/// <summary>
/// Sent when a player joins the arena.
/// </summary>
public sealed record JoinMessage : NetworkMessage
{
    public required string PlayerName { get; init; }
}

/// <summary>
/// Sent when a player leaves the arena.
/// </summary>
public sealed record LeaveMessage : NetworkMessage;

/// <summary>
/// Broadcast when a player's health changes (after taking damage or healing).
/// </summary>
public sealed record HealthChangedMessage : NetworkMessage
{
    public required int CurrentHealth { get; init; }
    public required int MaxHealth { get; init; }
}

/// <summary>
/// Broadcast when a player's equipped items change.
/// </summary>
public sealed record ItemsStateMessage : NetworkMessage
{
    public required Dictionary<string, string?> EquippedItems { get; init; }
}

/// <summary>
/// Broadcast when a player's gold amount changes.
/// </summary>
public sealed record GoldStateMessage : NetworkMessage
{
    public required int Gold { get; init; }
}

/// <summary>
/// Serialization helpers for network messages.
/// </summary>
public static class NetworkMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(NetworkMessage message)
    {
        return JsonSerializer.Serialize(message, Options);
    }

    public static NetworkMessage? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<NetworkMessage>(json, Options);
    }
}
