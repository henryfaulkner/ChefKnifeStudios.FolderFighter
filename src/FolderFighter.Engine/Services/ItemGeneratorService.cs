using FolderFighter.Engine.Core;

namespace FolderFighter.Engine.Services;

/// <summary>
/// Generates item files at regular intervals in the player's arena directory.
/// Items can be equipped in different slots for various bonuses.
/// </summary>
public sealed class ItemGeneratorService : IDisposable
{
    private readonly string _arenaPath;
    private readonly GameLoop _gameLoop;
    private readonly ILogger<ItemGeneratorService> _logger;
    private readonly Random _random;
    private readonly Timer _timer;
    private readonly TimeSpan _itemInterval;
    private readonly int _maxItems;

    public ItemGeneratorService(
        string arenaPath,
        GameLoop gameLoop,
        ILogger<ItemGeneratorService> logger,
        TimeSpan? itemInterval = null,
        int maxItems = 5)
    {
        _arenaPath = arenaPath;
        _gameLoop = gameLoop;
        _logger = logger;
        _random = new Random();
        _itemInterval = itemInterval ?? TimeSpan.FromSeconds(10);
        _maxItems = maxItems;

        _timer = new Timer(GenerateItem, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        _logger.LogInformation("Starting item generator (interval: {Interval}s, max: {Max})",
            _itemInterval.TotalSeconds, _maxItems);

        // Generate initial item immediately, then on interval
        GenerateItem(null);
        _timer.Change(_itemInterval, _itemInterval);
    }

    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.LogInformation("Item generator stopped");
    }

    private void GenerateItem(object? state)
    {
        try
        {
            var currentItemCount = CountCurrentItems();
            if (currentItemCount >= _maxItems)
            {
                _logger.LogDebug("Max items reached ({Count}/{Max}), skipping generation",
                    currentItemCount, _maxItems);
                return;
            }

            var itemName = ItemDefinitions.GetRandomItemName(_random);
            var fileName = $"{itemName}.item";
            var filePath = Path.Combine(_arenaPath, fileName);

            // Don't create duplicate item files
            if (File.Exists(filePath))
            {
                _logger.LogDebug("Item {ItemName} already exists, skipping", itemName);
                return;
            }

            // Create the item file with flavor text
            var itemInfo = ItemDefinitions.GetItem(itemName);
            var content = GenerateItemContent(itemName, itemInfo);

            File.WriteAllText(filePath, content);

            _logger.LogInformation("Generated item: {ItemName}", itemName);
            _gameLoop.Dispatch(new ItemDrawn(itemName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating item");
        }
    }

    private int CountCurrentItems()
    {
        if (!Directory.Exists(_arenaPath))
        {
            return 0;
        }

        return Directory.GetFiles(_arenaPath, "*.item").Length;
    }

    private static string GenerateItemContent(string itemName, ItemDefinitions.ItemInfo? info)
    {
        var slotLabel = info?.Slot switch
        {
            ItemDefinitions.ItemSlot.Weapon => "WEAPON",
            ItemDefinitions.ItemSlot.Armor => "ARMOR",
            ItemDefinitions.ItemSlot.Accessory => "ACCESSORY",
            ItemDefinitions.ItemSlot.Head => "HEAD",
            _ => "UNKNOWN"
        };

        var effects = new List<string>();
        if (info?.DamageModifier > 1.0f)
            effects.Add($"+{(info.DamageModifier - 1) * 100:F0}% Damage");
        if (info?.ArmorValue > 0)
            effects.Add($"{info.ArmorValue} Armor");
        if (info?.PassiveHealth > 0)
            effects.Add($"+{info.PassiveHealth} Max HP");

        var effectsList = effects.Any()
            ? "Effects:\n  - " + string.Join("\n  - ", effects)
            : "Effects: None";

        return $"""
                ================================
                       {itemName}
                ================================
                Slot: {slotLabel}
                {effectsList}
                ================================

                Drop this file into .self folder
                to equip it!

                Drop into opponent's folder to
                take it with you when defeated.
                ================================
                """;
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}
