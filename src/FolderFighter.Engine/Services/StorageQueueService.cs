using System.Collections.Immutable;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using FolderFighter.Engine.Core;

namespace FolderFighter.Engine.Services;

/// <summary>
/// Handles multiplayer communication via Azure Storage Queue.
/// Uses a single shared queue with client-side filtering.
///
/// PoC Approach:
/// - All players send to the same queue
/// - All players peek (not receive) messages to avoid deletion race conditions
/// - Messages expire via TTL (time-to-live) instead of explicit deletion
/// - Each player tracks seen message IDs to avoid duplicate processing
/// </summary>
public sealed class StorageQueueService : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly string _queueName;
    private readonly string _playerName;
    private readonly GameLoop _gameLoop;
    private readonly ILogger<StorageQueueService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _messageTtl;

    // Track processed message IDs to avoid duplicates
    private readonly HashSet<string> _processedMessageIds = new();
    private readonly object _processedLock = new();

    private QueueClient? _queueClient;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    public StorageQueueService(
        string connectionString,
        string queueName,
        string playerName,
        GameLoop gameLoop,
        ILogger<StorageQueueService> logger,
        TimeSpan? pollInterval = null,
        TimeSpan? messageTtl = null)
    {
        _connectionString = connectionString;
        _queueName = queueName;
        _playerName = playerName;
        _gameLoop = gameLoop;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
        _messageTtl = messageTtl ?? TimeSpan.FromMinutes(1); // Messages expire after 1 minute
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to Storage Queue as {PlayerName}...", _playerName);

        // Create queue client
        _queueClient = new QueueClient(_connectionString, _queueName);
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // Start polling for messages
        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollTask = PollMessagesAsync(_pollCts.Token);

        // Announce that we joined
        await SendMessageAsync(new JoinMessage
        {
            SenderId = _playerName,
            PlayerName = _playerName
        }, cancellationToken);

        _logger.LogInformation("Connected to Storage Queue. Polling for events...");
    }

    private async Task PollMessagesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // PEEK messages instead of receiving - this doesn't hide them from other clients
                var messages = await _queueClient!.PeekMessagesAsync(
                    maxMessages: 32,
                    cancellationToken: cancellationToken);

                foreach (var message in messages.Value)
                {
                    ProcessPeekedMessage(message);
                }

                // Periodically clean up old message IDs to prevent memory growth
                CleanupProcessedIds();

                // Small delay between polls
                await Task.Delay(_pollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling messages");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private void ProcessPeekedMessage(PeekedMessage message)
    {
        try
        {
            // Check if we've already processed this message
            lock (_processedLock)
            {
                if (_processedMessageIds.Contains(message.MessageId))
                {
                    return; // Already processed
                }
            }

            var json = message.MessageText;
            var networkMessage = NetworkMessageSerializer.Deserialize(json);

            if (networkMessage == null)
            {
                _logger.LogWarning("Failed to deserialize message {MessageId}", message.MessageId);
                MarkAsProcessed(message.MessageId);
                return;
            }

            // Client-side filtering: check if this message is for us
            var targetId = GetTargetId(networkMessage);
            var isForUs = targetId == _playerName || targetId == "global";
            var isFromUs = networkMessage.SenderId == _playerName;

            // Mark as processed regardless of whether it's for us
            MarkAsProcessed(message.MessageId);

            if (isForUs && !isFromUs)
            {
                // Process the message
                DispatchToGameLoop(networkMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", message.MessageId);
        }
    }

    private void MarkAsProcessed(string messageId)
    {
        lock (_processedLock)
        {
            _processedMessageIds.Add(messageId);
        }
    }

    private void CleanupProcessedIds()
    {
        // Keep the set from growing indefinitely
        // Since messages expire after TTL, we can safely clear old IDs periodically
        lock (_processedLock)
        {
            if (_processedMessageIds.Count > 1000)
            {
                _processedMessageIds.Clear();
                _logger.LogDebug("Cleared processed message ID cache");
            }
        }
    }

    private static string GetTargetId(NetworkMessage message) => message switch
    {
        AttackMessage attack => attack.TargetId,
        JoinMessage => "global",
        LeaveMessage => "global",
        HealthChangedMessage => "global",
        ItemsStateMessage => "global",
        GoldStateMessage => "global",
        _ => "global"
    };

    private void DispatchToGameLoop(NetworkMessage message)
    {
        switch (message)
        {
            case AttackMessage attack:
                _logger.LogInformation("Received attack from {Attacker}: {Spell} ({Damage} dmg)",
                    attack.SenderId, attack.SpellName, attack.Damage);
                _gameLoop.Dispatch(new SpellReceived(attack.SenderId, attack.SpellName, attack.Damage));
                break;

            case JoinMessage join:
                _logger.LogInformation("Player joined: {PlayerName}", join.PlayerName);
                _gameLoop.Dispatch(new OpponentJoined(join.PlayerName));
                break;

            case LeaveMessage leave:
                _logger.LogInformation("Player left: {SenderId}", leave.SenderId);
                _gameLoop.Dispatch(new OpponentLeft(leave.SenderId));
                break;

            case HealthChangedMessage health:
                _logger.LogDebug("Health update from {SenderId}: {Health}/{MaxHealth}",
                    health.SenderId, health.CurrentHealth, health.MaxHealth);
                _gameLoop.Dispatch(new OpponentHealthChanged(health.SenderId, health.CurrentHealth, health.MaxHealth));
                break;

            case ItemsStateMessage items:
                _logger.LogDebug("Items update from {SenderId}", items.SenderId);
                var equipmentDict = items.EquippedItems.ToImmutableDictionary(
                    kvp => (ItemDefinitions.ItemSlot)Enum.Parse(typeof(ItemDefinitions.ItemSlot), kvp.Key),
                    kvp => kvp.Value);
                _gameLoop.Dispatch(new OpponentEquipmentChanged(items.SenderId, equipmentDict));
                break;

            case GoldStateMessage gold:
                _logger.LogDebug("Gold update from {SenderId}: {Gold}g", gold.SenderId, gold.Gold);
                _gameLoop.Dispatch(new OpponentGoldChanged(gold.SenderId, gold.Gold));
                break;
        }
    }

    public async Task SendMessageAsync(NetworkMessage message, CancellationToken cancellationToken = default)
    {
        if (_queueClient == null)
        {
            _logger.LogWarning("Queue client not initialized");
            return;
        }

        try
        {
            var json = NetworkMessageSerializer.Serialize(message);

            // Send with TTL so messages auto-expire
            await _queueClient.SendMessageAsync(
                json,
                visibilityTimeout: TimeSpan.Zero,  // Visible immediately
                timeToLive: _messageTtl,           // Auto-delete after TTL
                cancellationToken: cancellationToken);

            _logger.LogDebug("Sent {MessageType}", message.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
        }
    }

    /// <summary>
    /// Send an attack to a specific opponent.
    /// </summary>
    public Task SendAttackAsync(string targetName, string spellName, int damage, CancellationToken cancellationToken = default)
    {
        var message = new AttackMessage
        {
            SenderId = _playerName,
            TargetId = targetName,
            SpellName = spellName,
            Damage = damage
        };

        return SendMessageAsync(message, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        // Announce that we're leaving
        if (_queueClient != null)
        {
            await SendMessageAsync(new LeaveMessage
            {
                SenderId = _playerName
            }, cancellationToken);
        }

        // Stop polling
        _pollCts?.Cancel();

        if (_pollTask != null)
        {
            try
            {
                await _pollTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _logger.LogInformation("Disconnected from Storage Queue");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _pollCts?.Dispose();
    }
}
