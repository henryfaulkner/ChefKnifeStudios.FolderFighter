using FolderFighter.Engine.Core;
using FolderFighter.Engine.Services;
using Microsoft.Extensions.Options;

namespace FolderFighter.Engine;

/// <summary>
/// Main background worker that orchestrates the game engine.
/// Coordinates the Elm Architecture game loop with I/O services.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly GameConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;

    private GameLoop? _gameLoop;
    private FileSystemWatcherService? _fileWatcher;
    private SpellGeneratorService? _spellGenerator;
    private ItemGeneratorService? _itemGenerator;
    private DirectoryManager? _directoryManager;
    private ConsoleRenderer? _renderer;
    private StorageQueueService? _queueService;
    private ShopService? _shopService;

    public Worker(
        ILogger<Worker> logger,
        IOptions<GameConfiguration> config,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _config = config.Value;
        _loggerFactory = loggerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Folder Fighter Engine...");
        _logger.LogInformation("Player: {PlayerName}", _config.PlayerName);
        _logger.LogInformation("Arena: {ArenaPath}", _config.ArenaPath);
        _logger.LogInformation("Multiplayer: {Multiplayer}", _config.IsMultiplayerEnabled ? "Enabled" : "Offline");

        try
        {
            // Initialize the Elm Architecture game loop
            _gameLoop = new GameLoop(_config.PlayerName);

            // Initialize services
            _renderer = new ConsoleRenderer();
            _gameLoop.OnStateChanged += _renderer.Render;

            _fileWatcher = new FileSystemWatcherService(
                _config.ArenaPath,
                _gameLoop,
                _loggerFactory.CreateLogger<FileSystemWatcherService>());

            _spellGenerator = new SpellGeneratorService(
                _config.ArenaPath,
                _gameLoop,
                _loggerFactory.CreateLogger<SpellGeneratorService>(),
                TimeSpan.FromSeconds(_config.SpellIntervalSeconds),
                _config.MaxSpells);

            _itemGenerator = new ItemGeneratorService(
                _config.ArenaPath,
                _gameLoop,
                _loggerFactory.CreateLogger<ItemGeneratorService>(),
                TimeSpan.FromSeconds(_config.SpellIntervalSeconds * 2), // Items slower than spells
                _config.MaxSpells);

            _directoryManager = new DirectoryManager(
                _config.ArenaPath,
                _gameLoop,
                _loggerFactory.CreateLogger<DirectoryManager>());

            _shopService = new ShopService(
                _config.ArenaPath,
                _gameLoop,
                _loggerFactory.CreateLogger<ShopService>(),
                TimeSpan.FromSeconds(_config.ShopIntervalSeconds),
                _config.PassiveGoldPerTick);

            // Connect to Azure Storage Queue if configured
            if (_config.IsMultiplayerEnabled)
            {
                _queueService = new StorageQueueService(
                    _config.StorageConnectionString!,
                    _config.QueueName,
                    _config.PlayerName,
                    _gameLoop,
                    _loggerFactory.CreateLogger<StorageQueueService>());

                // Wire up network events
                _gameLoop.OnMessageProcessed += async (msg, state) =>
                {
                    if (_queueService == null) return;

                    // Broadcast health changes
                    if (msg is SpellReceived or SelfCast)
                    {
                        var healthMsg = new HealthChangedMessage
                        {
                            SenderId = _config.PlayerName,
                            CurrentHealth = state.Health,
                            MaxHealth = state.MaxHealth
                        };
                        await _queueService.SendMessageAsync(healthMsg, stoppingToken);
                    }

                    // Broadcast equipment changes
                    if (msg is ItemEquipped)
                    {
                        var equipmentDict = state.EquippedItems.ToDictionary(
                            kvp => kvp.Key.ToString(),
                            kvp => kvp.Value);
                        var itemsMsg = new ItemsStateMessage
                        {
                            SenderId = _config.PlayerName,
                            EquippedItems = equipmentDict
                        };
                        await _queueService.SendMessageAsync(itemsMsg, stoppingToken);
                    }

                    // Broadcast gold changes
                    if (msg is GoldEarned or SpellPurchased or ItemPurchased)
                    {
                        var goldMsg = new GoldStateMessage
                        {
                            SenderId = _config.PlayerName,
                            Gold = state.Gold
                        };
                        await _queueService.SendMessageAsync(goldMsg, stoppingToken);
                    }

                    // Send attacks
                    if (msg is SpellCast cast)
                    {
                        var damage = SpellDefinitions.GetDamage(cast.SpellName);
                        await _queueService.SendAttackAsync(cast.TargetName, cast.SpellName, damage, stoppingToken);
                    }
                };

                await _queueService.StartAsync(stoppingToken);
            }

            // Wire up gold reward for spell casts
            _gameLoop.OnMessageProcessed += (msg, state) =>
            {
                // Award gold when a spell is cast based on damage
                if (msg is SpellCast cast)
                {
                    var damage = SpellDefinitions.GetDamage(cast.SpellName);
                    var goldReward = damage * _config.GoldPerDamage;
                    if (goldReward > 0)
                    {
                        _gameLoop.Dispatch(new GoldEarned(goldReward));
                    }
                }
            };

            // Start all services
            _directoryManager.Start();
            _fileWatcher.Start();
            _spellGenerator.Start();
            _itemGenerator.Start();
            _shopService.Start();

            // Initial render
            _renderer.Render(_gameLoop.CurrentState);

            // Run the game loop
            await _gameLoop.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Game shutdown requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in game engine");
            throw;
        }
        finally
        {
            await ShutdownAsync();
        }
    }

    private async Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down Folder Fighter Engine...");

        _spellGenerator?.Stop();
        _itemGenerator?.Stop();
        _shopService?.Stop();
        _fileWatcher?.Stop();
        _directoryManager?.Stop();

        if (_queueService != null)
        {
            await _queueService.StopAsync();
            await _queueService.DisposeAsync();
        }

        _gameLoop?.Dispose();
        _spellGenerator?.Dispose();
        _itemGenerator?.Dispose();
        _shopService?.Dispose();
        _fileWatcher?.Dispose();
        _directoryManager?.Dispose();

        // Clean up player's arena folder
        CleanupArenaFolder();

        _logger.LogInformation("Folder Fighter Engine stopped");
    }

    private void CleanupArenaFolder()
    {
        try
        {
            if (Directory.Exists(_config.ArenaPath))
            {
                Directory.Delete(_config.ArenaPath, recursive: true);
                _logger.LogInformation("Cleaned up arena folder: {ArenaPath}", _config.ArenaPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not clean up arena folder: {ArenaPath}", _config.ArenaPath);
        }
    }
}
