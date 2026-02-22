using FolderFighter.Engine.Core;

namespace FolderFighter.Engine.Services;

/// <summary>
/// Generates spell files at regular intervals in the player's arena directory.
/// These spell files can then be dragged to attack opponents.
/// </summary>
public sealed class SpellGeneratorService : IDisposable
{
    private readonly string _arenaPath;
    private readonly GameLoop _gameLoop;
    private readonly ILogger<SpellGeneratorService> _logger;
    private readonly Random _random;
    private readonly Timer _timer;
    private readonly TimeSpan _spellInterval;
    private readonly int _maxSpells;

    public SpellGeneratorService(
        string arenaPath,
        GameLoop gameLoop,
        ILogger<SpellGeneratorService> logger,
        TimeSpan? spellInterval = null,
        int maxSpells = 5)
    {
        _arenaPath = arenaPath;
        _gameLoop = gameLoop;
        _logger = logger;
        _random = new Random();
        _spellInterval = spellInterval ?? TimeSpan.FromSeconds(5);
        _maxSpells = maxSpells;

        _timer = new Timer(GenerateSpell, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        _logger.LogInformation("Starting spell generator (interval: {Interval}s, max: {Max})",
            _spellInterval.TotalSeconds, _maxSpells);

        // Generate initial spell immediately, then on interval
        GenerateSpell(null);
        _timer.Change(_spellInterval, _spellInterval);
    }

    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.LogInformation("Spell generator stopped");
    }

    private void GenerateSpell(object? state)
    {
        try
        {
            var currentSpellCount = CountCurrentSpells();
            if (currentSpellCount >= _maxSpells)
            {
                _logger.LogDebug("Max spells reached ({Count}/{Max}), skipping generation",
                    currentSpellCount, _maxSpells);
                return;
            }

            var spellName = SpellDefinitions.GetRandomSpellName(_random);
            var fileName = $"{spellName}.txt";
            var filePath = Path.Combine(_arenaPath, fileName);

            // Don't create duplicate spell files
            if (File.Exists(filePath))
            {
                _logger.LogDebug("Spell {SpellName} already exists, skipping", spellName);
                return;
            }

            // Create the spell file with flavor text
            var spellInfo = SpellDefinitions.GetSpell(spellName);
            var content = GenerateSpellContent(spellName, spellInfo);

            File.WriteAllText(filePath, content);

            _logger.LogInformation("Generated spell: {SpellName}", spellName);
            _gameLoop.Dispatch(new SpellDrawn(spellName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating spell");
        }
    }

    private int CountCurrentSpells()
    {
        if (!Directory.Exists(_arenaPath))
        {
            return 0;
        }

        return Directory.GetFiles(_arenaPath, "*.txt").Length;
    }

    private static string GenerateSpellContent(string spellName, SpellDefinitions.SpellInfo? info)
    {
        var typeLabel = info?.Type switch
        {
            SpellDefinitions.SpellType.Offensive => "OFFENSIVE SPELL",
            SpellDefinitions.SpellType.Defensive => "DEFENSIVE SPELL",
            SpellDefinitions.SpellType.Utility => "UTILITY SPELL",
            _ => "UNKNOWN SPELL"
        };

        return $"""
                ================================
                       {spellName}
                ================================
                Type: {typeLabel}
                Damage: {info?.Damage ?? 0}
                Heal: {info?.HealAmount ?? 0}
                ================================

                Drag this file into an opponent's
                folder to cast the spell!

                Drop into .self for self-buffs.
                ================================
                """;
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}
