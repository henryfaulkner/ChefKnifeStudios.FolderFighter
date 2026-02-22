using FolderFighter.Engine;

var builder = Host.CreateApplicationBuilder(args);

// Bind configuration
builder.Services.Configure<GameConfiguration>(config =>
{
    // Bind from appsettings.json
    builder.Configuration.GetSection(GameConfiguration.SectionName).Bind(config);

    // Override with command-line arguments for local multi-player testing
    // Usage: dotnet run -- --player Alpha
    //        dotnet run -- --player Bravo
    var playerIndex = Array.IndexOf(args, "--player");
    if (playerIndex >= 0 && playerIndex < args.Length - 1)
    {
        config.PlayerName = args[playerIndex + 1];
    }

    // Each player gets their own arena folder at top level
    config.ArenaPath = Path.Combine(config.ArenaPath, config.PlayerName);
});

// Register the game worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
