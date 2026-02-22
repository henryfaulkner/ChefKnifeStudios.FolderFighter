# Azure Storage Queue Implementation

This document describes how Folder Fighter uses Azure Storage Queues for multiplayer communication.

## Overview

The game uses a **single shared queue** with **client-side filtering** and **peek-based polling**. This is a simple PoC approach optimized for low cost (~$0.05/month) rather than scale.

```
┌─────────────┐     ┌─────────────────────┐     ┌─────────────┐
│  Player A   │────▶│   Azure Storage     │◀────│  Player B   │
│  (Console)  │◀────│   Queue (shared)    │────▶│  (Console)  │
└─────────────┘     └─────────────────────┘     └─────────────┘
                              │
                    "folder-fighter-events"
                              │
                    Messages expire via TTL
```

## Key Design Decisions

### 1. Peek Instead of Receive

**Problem:** Azure Storage Queues are designed for competing consumers. When one client receives a message, it becomes invisible to others. If that client deletes it, other clients never see it.

**Solution:** Use `PeekMessagesAsync()` instead of `ReceiveMessagesAsync()`. Peeking reads messages without hiding them from other clients.

```csharp
// DON'T DO THIS - hides message from other clients
var messages = await _queueClient.ReceiveMessagesAsync(maxMessages: 32);

// DO THIS - all clients can see the message
var messages = await _queueClient.PeekMessagesAsync(maxMessages: 32);
```

### 2. TTL-Based Expiration

**Problem:** If we don't delete messages, the queue grows forever.

**Solution:** Set a time-to-live (TTL) when sending. Messages auto-delete after expiration.

```csharp
await _queueClient.SendMessageAsync(
    json,
    visibilityTimeout: TimeSpan.Zero,  // Visible immediately
    timeToLive: TimeSpan.FromMinutes(1) // Auto-delete after 1 minute
);
```

### 3. Client-Side Deduplication

**Problem:** Since we peek (not receive), the same message appears on every poll until it expires.

**Solution:** Track processed message IDs in a `HashSet`. Skip messages we've already seen.

```csharp
private readonly HashSet<string> _processedMessageIds = new();

private void ProcessPeekedMessage(PeekedMessage message)
{
    if (_processedMessageIds.Contains(message.MessageId))
        return; // Already processed

    _processedMessageIds.Add(message.MessageId);
    // Process message...
}
```

### 4. Client-Side Filtering

**Problem:** All players see all messages, but attacks are targeted at specific players.

**Solution:** Each message has a `TargetId`. Clients ignore messages not meant for them.

```csharp
var targetId = GetTargetId(networkMessage);
var isForUs = targetId == _playerName || targetId == "global";
var isFromUs = networkMessage.SenderId == _playerName;

if (isForUs && !isFromUs)
{
    DispatchToGameLoop(networkMessage);
}
```

## Message Types

All messages inherit from `NetworkMessage`:

```csharp
public abstract record NetworkMessage
{
    public required string SenderId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
```

| Message Type | TargetId | Purpose |
|--------------|----------|---------|
| `AttackMessage` | Player name | Spell cast at specific player |
| `JoinMessage` | "global" | Broadcast: player entered arena |
| `LeaveMessage` | "global" | Broadcast: player left arena |
| `DamageConfirmMessage` | Attacker name | Confirm damage was dealt |

## Message Flow

### Sending an Attack

```
1. Alpha drags Fireball.txt into Bravo/ folder
2. FileSystemWatcher detects move
3. GameLoop receives SpellCast("Fireball", "Bravo")
4. Worker.OnMessageProcessed triggers
5. StorageQueueService.SendAttackAsync("Bravo", "Fireball", 15)
6. Message sent to Azure Queue with 1-minute TTL:
   {
     "type": "attack",
     "senderId": "Alpha",
     "targetId": "Bravo",
     "spellName": "Fireball",
     "damage": 15,
     "timestamp": "2026-02-21T19:30:00Z"
   }
```

### Receiving an Attack

```
1. Bravo's PollMessagesAsync peeks messages every 500ms
2. Sees message with targetId="Bravo"
3. Checks _processedMessageIds - not seen before
4. Adds to _processedMessageIds
5. Dispatches SpellReceived("Alpha", "Fireball", 15)
6. GameLoop.Update reduces Bravo's health by 15
7. ConsoleRenderer shows: "[HIT] Alpha hit you with Fireball!"
8. Message expires after 1 minute (auto-deleted by Azure)
```

## Configuration

```json
{
  "Game": {
    "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    "QueueName": "folder-fighter-events"
  }
}
```

Or via environment variables:
```bash
set Game__StorageConnectionString=DefaultEndpointsProtocol=https;...
set Game__QueueName=folder-fighter-events
```

## Limitations

| Limitation | Impact | Mitigation |
|------------|--------|------------|
| No true pub/sub | All clients poll all messages | Client-side filtering |
| 32 message peek limit | May miss messages in high traffic | Poll frequently (500ms) |
| 1-minute TTL | Late joiners miss old messages | Only affects join/leave broadcasts |
| Memory growth | HashSet grows with messages | Clear after 1000 entries |

## Cost Analysis

| Operation | Price | Notes |
|-----------|-------|-------|
| Send message | $0.00000004 | Per message |
| Peek messages | $0.00000004 | Per batch (up to 32) |
| Storage | $0.045/GB/month | Minimal for text messages |

**Example:** 2 players, 1 hour session, 500ms polling:
- Peek operations: 2 players × 7,200 polls = 14,400 ops = **$0.0006**
- Send operations: ~100 attacks = **$0.000004**
- **Total: < $0.001 per hour**

## Future Improvements

For production scale, consider:

1. **Queue-per-player**: Each player has an inbox queue. Senders push to target's queue directly.

2. **Azure Service Bus Topics**: Built-in pub/sub with SQL filtering. Higher cost (~$10/month) but cleaner architecture.

3. **SignalR**: Real-time WebSocket connections. Better latency, no polling.

## Files

| File | Purpose |
|------|---------|
| `Services/StorageQueueService.cs` | Queue client, polling, send/receive |
| `Services/NetworkMessage.cs` | Message DTOs + JSON serialization |
| `GameConfiguration.cs` | Connection string, queue name |
| `Worker.cs` | Wires GameLoop to queue service |
