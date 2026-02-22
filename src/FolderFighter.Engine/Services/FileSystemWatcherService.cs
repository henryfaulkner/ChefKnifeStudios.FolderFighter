using FolderFighter.Engine.Core;

namespace FolderFighter.Engine.Services;

/// <summary>
/// Monitors the arena directory for file movements.
/// Translates file system events into game messages.
///
/// Directory structure:
/// ./Arena/
///   ├── Fireball.txt        (available spells)
///   ├── Shield.txt
///   ├── .self/              (drop spells here for self-buff)
///   ├── Opponent_Alpha/     (drop spells here to attack)
///   └── Opponent_Bravo/
/// </summary>
public sealed class FileSystemWatcherService : IDisposable
{
    private readonly string _arenaPath;
    private readonly GameLoop _gameLoop;
    private readonly ILogger<FileSystemWatcherService> _logger;
    private readonly FileSystemWatcher _watcher;
    private readonly string _selfDirectoryName = ".self";
    private readonly string _shopDirectoryName = ".shop";

    public FileSystemWatcherService(
        string arenaPath,
        GameLoop gameLoop,
        ILogger<FileSystemWatcherService> logger)
    {
        _arenaPath = arenaPath;
        _gameLoop = gameLoop;
        _logger = logger;

        EnsureArenaStructure();

        _watcher = new FileSystemWatcher(_arenaPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = false
        };

        _watcher.Created += OnFileCreated;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Error += OnWatcherError;
    }

    public void Start()
    {
        _logger.LogInformation("Starting file system watcher on {ArenaPath}", _arenaPath);
        _watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        _watcher.EnableRaisingEvents = false;
        _logger.LogInformation("File system watcher stopped");
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        // Ignore directories
        if (Directory.Exists(e.FullPath))
        {
            return;
        }

        var relativePath = Path.GetRelativePath(_arenaPath, e.FullPath);
        var pathParts = relativePath.Split(Path.DirectorySeparatorChar);

        // File created directly in arena = new spell/item drawn (handled by generators)
        if (pathParts.Length == 1)
        {
            return;
        }

        // File moved into a subdirectory = spell cast, item equipped, or shop item moved
        if (pathParts.Length == 2)
        {
            var targetDirectory = pathParts[0];
            var fileName = pathParts[1];

            // Ignore files in .shop folder (these are shop listings, not interactions)
            if (targetDirectory.Equals(_shopDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                var spellName = Path.GetFileNameWithoutExtension(fileName);
                ProcessSpellCast(targetDirectory, spellName, e.FullPath);
            }
            else if (fileName.EndsWith(".item", StringComparison.OrdinalIgnoreCase))
            {
                var itemName = Path.GetFileNameWithoutExtension(fileName);
                ProcessItemEquip(targetDirectory, itemName, e.FullPath);
            }
        }
    }

    private void ProcessSpellCast(string targetDirectory, string spellName, string filePath)
    {
        try
        {
            // Check if this is a self-cast (defensive spell)
            if (targetDirectory.Equals(_selfDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Self-cast detected: {SpellName}", spellName);
                _gameLoop.Dispatch(new SelfCast(spellName));
            }
            else
            {
                // Attack on opponent
                _logger.LogInformation("Spell cast detected: {SpellName} -> {Target}", spellName, targetDirectory);
                _gameLoop.Dispatch(new SpellCast(spellName, targetDirectory));
            }

            // Consume the spell file (delete it)
            ConsumeSpellFile(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing spell cast: {SpellName} -> {Target}", spellName, targetDirectory);
        }
    }

    private void ProcessItemEquip(string targetDirectory, string itemName, string filePath)
    {
        try
        {
            // Only self folder allowed for items
            if (!targetDirectory.Equals(_selfDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Item {ItemName} dropped in {Target}, ignored (use .self folder)", itemName, targetDirectory);
                ConsumeSpellFile(filePath); // Still consume the file
                return;
            }

            // Determine item slot
            if (ItemDefinitions.GetItem(itemName) is { } itemInfo)
            {
                _logger.LogInformation("Item equipped: {ItemName} to {Slot}", itemName, itemInfo.Slot);
                _gameLoop.Dispatch(new ItemEquipped(itemInfo.Slot, itemName));
                ConsumeSpellFile(filePath);
            }
            else
            {
                _logger.LogWarning("Unknown item: {ItemName}", itemName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing item equip: {ItemName}", itemName);
        }
    }

    private void ConsumeSpellFile(string filePath)
    {
        // Fire and forget - retry deletion in background
        Task.Run(async () =>
        {
            const int maxRetries = 5;
            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Wait for file handle to be released
                    await Task.Delay(200 * (i + 1));

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        _logger.LogDebug("Spell file consumed: {FilePath}", filePath);
                        return;
                    }
                }
                catch (UnauthorizedAccessException) when (i < maxRetries - 1)
                {
                    // File still locked, retry
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    // File still locked, retry
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not consume spell file: {FilePath}", filePath);
                    return;
                }
            }

            _logger.LogWarning("Failed to consume spell file after {Retries} retries: {FilePath}", maxRetries, filePath);
        });
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        var relativePath = Path.GetRelativePath(_arenaPath, e.FullPath);
        var pathParts = relativePath.Split(Path.DirectorySeparatorChar);

        // Track spell deletions from root arena
        if (pathParts.Length == 1 && (e.Name?.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            var spellName = Path.GetFileNameWithoutExtension(e.Name!);
            _gameLoop.Dispatch(new SpellConsumed(spellName));
        }

        // Track item deletions from root arena
        if (pathParts.Length == 1 && (e.Name?.EndsWith(".item", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            var itemName = Path.GetFileNameWithoutExtension(e.Name!);
            _gameLoop.Dispatch(new ItemConsumed(itemName));
        }

        // Track shop item purchases (file removed from .shop folder)
        if (pathParts.Length == 2 && pathParts[0].Equals(_shopDirectoryName, StringComparison.OrdinalIgnoreCase))
        {
            ProcessShopPurchase(e.Name!, e.FullPath);
        }
    }

    private void ProcessShopPurchase(string fileName, string filePath)
    {
        try
        {
            // Parse filename: "spellName_30g.txt" or "itemName_50g.item"
            if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseShopFilename(fileName, out var itemName, out var cost))
                {
                    _logger.LogInformation("Shop purchase detected: {SpellName} for {Cost}g", itemName, cost);
                    _gameLoop.Dispatch(new SpellPurchased(itemName, cost));
                    // File is already deleted by the system, no need to consume
                }
            }
            else if (fileName.EndsWith(".item", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseShopFilename(fileName, out var itemName, out var cost))
                {
                    _logger.LogInformation("Shop purchase detected: {ItemName} for {Cost}g", itemName, cost);
                    _gameLoop.Dispatch(new ItemPurchased(itemName, cost));
                    // File is already deleted by the system, no need to consume
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing shop purchase: {FileName}", fileName);
        }
    }

    private static bool TryParseShopFilename(string fileName, out string itemName, out int cost)
    {
        itemName = "";
        cost = 0;

        // Remove extension
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // Find last underscore (separates item name from cost)
        var lastUnderscoreIndex = nameWithoutExt.LastIndexOf('_');
        if (lastUnderscoreIndex <= 0)
        {
            return false;
        }

        itemName = nameWithoutExt[..lastUnderscoreIndex];
        var costString = nameWithoutExt[(lastUnderscoreIndex + 1)..];

        // Remove trailing 'g' and parse
        if (costString.EndsWith("g", StringComparison.OrdinalIgnoreCase))
        {
            costString = costString[..^1];
        }

        return int.TryParse(costString, out cost);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "File system watcher error");
    }

    private void EnsureArenaStructure()
    {
        Directory.CreateDirectory(_arenaPath);
        Directory.CreateDirectory(Path.Combine(_arenaPath, _selfDirectoryName));
        _logger.LogInformation("Arena structure ensured at {ArenaPath}", _arenaPath);
    }

    public string CreateOpponentDirectory(string opponentName)
    {
        var opponentPath = Path.Combine(_arenaPath, opponentName);
        Directory.CreateDirectory(opponentPath);
        _logger.LogInformation("Created opponent directory: {OpponentPath}", opponentPath);
        return opponentPath;
    }

    public void RemoveOpponentDirectory(string opponentName)
    {
        var opponentPath = Path.Combine(_arenaPath, opponentName);
        if (Directory.Exists(opponentPath))
        {
            Directory.Delete(opponentPath, recursive: true);
            _logger.LogInformation("Removed opponent directory: {OpponentPath}", opponentPath);
        }
    }

    public void Dispose()
    {
        Stop();
        _watcher.Dispose();
    }
}
