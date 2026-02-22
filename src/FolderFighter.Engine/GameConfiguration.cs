namespace FolderFighter.Engine;

/// <summary>
/// Configuration options for the game engine.
/// </summary>
public sealed class GameConfiguration
{
    public const string SectionName = "Game";

    private string _playerName = "";
    private string _arenaPath = "";

    /// <summary>
    /// The player's display name.
    /// </summary>
    public string PlayerName
    {
        get => string.IsNullOrWhiteSpace(_playerName) ? $"Player_{Environment.MachineName}" : _playerName;
        set => _playerName = value;
    }

    /// <summary>
    /// Path to the arena directory where spell files and opponent folders are created.
    /// Defaults to %TEMP% to avoid OneDrive file locking issues.
    /// </summary>
    public string ArenaPath
    {
        get => string.IsNullOrWhiteSpace(_arenaPath)
            ? Path.Combine(Path.GetTempPath(), "FolderFighter_Arena")
            : _arenaPath;
        set => _arenaPath = value;
    }

    /// <summary>
    /// Azure Storage connection string.
    /// Leave empty for offline/solo mode.
    /// </summary>
    public string? StorageConnectionString { get; set; }

    /// <summary>
    /// Azure Storage Queue name for game events.
    /// </summary>
    public string QueueName { get; set; } = "folder-fighter-events";

    /// <summary>
    /// Interval between spell generation in seconds.
    /// </summary>
    public int SpellIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Maximum number of spells that can exist in the arena at once.
    /// </summary>
    public int MaxSpells { get; set; } = 5;

    /// <summary>
    /// Whether multiplayer mode is enabled.
    /// </summary>
    public bool IsMultiplayerEnabled => !string.IsNullOrEmpty(StorageConnectionString);

    /// <summary>
    /// Interval between shop refreshes in seconds.
    /// </summary>
    public int ShopIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gold earned per damage dealt to opponents.
    /// </summary>
    public int GoldPerDamage { get; set; } = 1;

    /// <summary>
    /// Gold earned passively on each shop refresh tick.
    /// </summary>
    public int PassiveGoldPerTick { get; set; } = 5;
}
