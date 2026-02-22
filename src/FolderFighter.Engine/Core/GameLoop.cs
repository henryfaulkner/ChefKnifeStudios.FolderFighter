using System.Threading.Channels;

namespace FolderFighter.Engine.Core;

/// <summary>
/// The central game loop following Elm Architecture.
/// Manages the message queue and state transitions.
/// Side effect handlers subscribe to state changes.
/// </summary>
public sealed class GameLoop : IDisposable
{
    private readonly Channel<Msg> _messageChannel;
    private readonly CancellationTokenSource _cts;
    private GameState _currentState;
    private readonly object _stateLock = new();

    public event Action<GameState>? OnStateChanged;
    public event Action<Msg, GameState>? OnMessageProcessed;

    public GameState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
    }

    public GameLoop(string playerName)
    {
        _messageChannel = Channel.CreateUnbounded<Msg>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _cts = new CancellationTokenSource();
        _currentState = GameState.Create(playerName);
    }

    /// <summary>
    /// Dispatch a message to be processed by the game loop.
    /// This is thread-safe and non-blocking.
    /// </summary>
    public void Dispatch(Msg message)
    {
        _messageChannel.Writer.TryWrite(message);
    }

    /// <summary>
    /// Start processing messages. Call this once to begin the game loop.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        try
        {
            await foreach (var msg in _messageChannel.Reader.ReadAllAsync(linkedCts.Token))
            {
                ProcessMessage(msg);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private void ProcessMessage(Msg msg)
    {
        lock (_stateLock)
        {
            var newState = Update.Apply(msg, _currentState);

            if (!ReferenceEquals(newState, _currentState))
            {
                _currentState = newState;
                OnStateChanged?.Invoke(newState);
            }

            OnMessageProcessed?.Invoke(msg, newState);
        }
    }

    /// <summary>
    /// Stop the game loop gracefully.
    /// </summary>
    public void Stop()
    {
        _messageChannel.Writer.Complete();
        _cts.Cancel();
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }
}
