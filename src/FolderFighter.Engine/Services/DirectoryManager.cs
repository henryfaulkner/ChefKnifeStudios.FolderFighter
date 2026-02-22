using FolderFighter.Engine.Core;

namespace FolderFighter.Engine.Services;

/// <summary>
/// Manages opponent directories in the arena.
/// Creates directories when opponents join, removes them when they leave or are defeated.
/// Subscribes to state changes to keep directories in sync with game state.
/// </summary>
public sealed class DirectoryManager : IDisposable
{
    private readonly string _arenaPath;
    private readonly GameLoop _gameLoop;
    private readonly ILogger<DirectoryManager> _logger;
    private readonly HashSet<string> _trackedOpponents = new();
    private readonly object _lock = new();

    public DirectoryManager(
        string arenaPath,
        GameLoop gameLoop,
        ILogger<DirectoryManager> logger)
    {
        _arenaPath = arenaPath;
        _gameLoop = gameLoop;
        _logger = logger;
    }

    public void Start()
    {
        _gameLoop.OnStateChanged += SyncDirectoriesWithState;
        _logger.LogInformation("Directory manager started");
    }

    public void Stop()
    {
        _gameLoop.OnStateChanged -= SyncDirectoriesWithState;
        _logger.LogInformation("Directory manager stopped");
    }

    private void SyncDirectoriesWithState(GameState state)
    {
        lock (_lock)
        {
            var currentOpponents = state.Opponents.Keys.ToHashSet();

            // Create directories for new opponents
            foreach (var opponent in currentOpponents)
            {
                if (!_trackedOpponents.Contains(opponent))
                {
                    CreateOpponentDirectory(opponent);
                    _trackedOpponents.Add(opponent);
                }
            }

            // Remove directories for opponents who left or were defeated
            var toRemove = _trackedOpponents.Except(currentOpponents).ToList();
            foreach (var opponent in toRemove)
            {
                RemoveOpponentDirectory(opponent);
                _trackedOpponents.Remove(opponent);
            }
        }
    }

    private void CreateOpponentDirectory(string opponentName)
    {
        try
        {
            var path = Path.Combine(_arenaPath, opponentName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                _logger.LogInformation("Created opponent directory: {OpponentName}", opponentName);

                // Create a README in the directory
                var readmePath = Path.Combine(path, "_TARGET.txt");
                File.WriteAllText(readmePath, $"""
                    ========================================
                    TARGET: {opponentName}
                    ========================================

                    Drop spell files here to attack this opponent!

                    Spells will be consumed on use.
                    ========================================
                    """);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create opponent directory: {OpponentName}", opponentName);
        }
    }

    private void RemoveOpponentDirectory(string opponentName)
    {
        try
        {
            var path = Path.Combine(_arenaPath, opponentName);
            if (Directory.Exists(path))
            {
                // Delete all files first
                foreach (var file in Directory.GetFiles(path))
                {
                    File.Delete(file);
                }
                Directory.Delete(path, recursive: true);
                _logger.LogInformation("Removed opponent directory: {OpponentName}", opponentName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove opponent directory: {OpponentName}", opponentName);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
