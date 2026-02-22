using System.Collections.Immutable;
using FolderFighter.Engine.Core;

namespace FolderFighter.Engine.Services;

/// <summary>
/// Manages shop rotation and passive gold generation.
/// Periodically refreshes shop inventory and awards passive trickle gold.
/// </summary>
public sealed class ShopService : IDisposable
{
    private readonly string _arenaPath;
    private readonly GameLoop _gameLoop;
    private readonly ILogger<ShopService> _logger;
    private readonly TimeSpan _refreshInterval;
    private readonly int _passiveGoldPerTick;

    private Timer? _timer;
    private readonly Random _random = new();

    public ShopService(
        string arenaPath,
        GameLoop gameLoop,
        ILogger<ShopService> logger,
        TimeSpan? refreshInterval = null,
        int passiveGoldPerTick = 5)
    {
        _arenaPath = arenaPath;
        _gameLoop = gameLoop;
        _logger = logger;
        _refreshInterval = refreshInterval ?? TimeSpan.FromSeconds(30);
        _passiveGoldPerTick = passiveGoldPerTick;
    }

    public void Start()
    {
        _logger.LogInformation("Starting Shop Service (refresh interval: {Interval}s)", _refreshInterval.TotalSeconds);
        _timer = new Timer(OnRefreshTick, null, TimeSpan.Zero, _refreshInterval);
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping Shop Service");
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        Stop();
        _timer?.Dispose();
    }

    private void OnRefreshTick(object? state)
    {
        try
        {
            // Award passive gold
            _gameLoop.Dispatch(new GoldEarned(_passiveGoldPerTick));

            // Refresh shop inventory
            var allSpells = ShopDefinitions.GetAllSpellNames();
            var allItems = ShopDefinitions.GetAllItemNames();

            // Pick random subset (up to 5 of each)
            var selectedSpells = PickRandom(allSpells, 5).ToImmutableList();
            var selectedItems = PickRandom(allItems, 5).ToImmutableList();

            // Create .shop folder if needed
            var shopPath = Path.Combine(_arenaPath, ".shop");
            Directory.CreateDirectory(shopPath);

            // Clear old shop listings
            foreach (var file in Directory.GetFiles(shopPath))
            {
                File.Delete(file);
            }

            // Write new spell listings
            foreach (var spellName in selectedSpells)
            {
                var cost = ShopDefinitions.GetSpellCost(spellName);
                var filename = $"{spellName}_{cost}g.txt";
                var filePath = Path.Combine(shopPath, filename);
                File.WriteAllText(filePath, $"Spell: {spellName}\nCost: {cost}g");
            }

            // Write new item listings
            foreach (var itemName in selectedItems)
            {
                var cost = ShopDefinitions.GetItemCost(itemName);
                var filename = $"{itemName}_{cost}g.item";
                var filePath = Path.Combine(shopPath, filename);
                File.WriteAllText(filePath, $"Item: {itemName}\nCost: {cost}g");
            }

            _logger.LogDebug("Shop refreshed: {SpellCount} spells, {ItemCount} items", selectedSpells.Count, selectedItems.Count);

            // Dispatch shop refresh event
            _gameLoop.Dispatch(new ShopRefreshed(selectedSpells, selectedItems));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in shop refresh");
        }
    }

    /// <summary>
    /// Randomly select N items from a list.
    /// </summary>
    private List<T> PickRandom<T>(List<T> items, int count)
    {
        if (items.Count <= count)
        {
            return new(items);
        }

        var result = new List<T>();
        var indices = new HashSet<int>();

        while (indices.Count < count)
        {
            indices.Add(_random.Next(items.Count));
        }

        foreach (var index in indices)
        {
            result.Add(items[index]);
        }

        return result;
    }
}
