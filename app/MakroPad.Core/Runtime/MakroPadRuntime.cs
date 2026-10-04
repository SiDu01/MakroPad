using System.Collections.Concurrent;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Device;
using MakroPad.Core.Protocol;

namespace MakroPad.Core.Runtime;

public enum ConnectionStatus
{
    Searching,
    Connected,
    Unresponsive,
    Incompatible
}

/// <summary>
/// The app's headless core: keeps a device session alive, routes inputs to actions and sends
/// feedback to the OLED. The UI only observes the events.
/// </summary>
public sealed class MakroPadRuntime : IAsyncDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ClockResyncInterval = TimeSpan.FromMinutes(10);

    private readonly IDeviceConnector connector;
    private readonly ActionRunner actionRunner;
    private readonly ProfileManager profiles;
    private readonly InputRouter router;
    private readonly TimeProvider timeProvider;
    private readonly string appVersion;
    private readonly ConcurrentDictionary<string, LatestWinsQueue<(Binding, InputEvent)>> sliderQueues = new();
    private readonly CancellationTokenSource stopping = new();
    private Task? loop;
    private DeviceSession? session;
    private ITimer? clockTimer;
    private AppSettings settings = new();

    public MakroPadRuntime(
        IDeviceConnector connector,
        ActionRunner actionRunner,
        ProfileManager profiles,
        TimeProvider timeProvider,
        string appVersion)
    {
        this.connector = connector;
        this.actionRunner = actionRunner;
        this.profiles = profiles;
        this.timeProvider = timeProvider;
        this.appVersion = appVersion;
        router = new InputRouter(profiles.Active.Bindings);
        profiles.ActiveChanged += OnProfileChanged;
    }

    /// <summary>Settings from config.json; replaced on reload.</summary>
    public AppSettings Settings
    {
        get => settings;
        set
        {
            settings = value;
            router.SliderDeadbandPercent = value.SliderDeadbandPercent;
        }
    }

    public ProfileManager Profiles => profiles;

    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Searching;

    public string? Endpoint => session?.Endpoint;

    public DeviceInfo? Device => session?.Device;

    public event Action<ConnectionStatus>? StatusChanged;

    public event Action<LineDirection, string>? LineLogged;

    public event Action<InputEvent>? InputReceived;

    public event Action<Binding, ActionResult>? ActionCompleted;

    public void Start()
    {
        loop ??= Task.Run(() => RunAsync(stopping.Token));
        clockTimer ??= timeProvider.CreateTimer(_ => SendState(), null, ClockResyncInterval, ClockResyncInterval);
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync();
        clockTimer?.Dispose();
        if (loop is not null)
        {
            try
            {
                await loop;
            }
            catch (OperationCanceledException)
            {
            }
        }

        stopping.Dispose();
    }

    /// <summary>Sends an overlay to the device (no-op while disconnected).</summary>
    public bool ShowScreen(ScreenRequest screen) => session?.TrySend(DeviceMessages.Screen(screen)) ?? false;

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            SetStatus(ConnectionStatus.Searching);
            ILineTransport? transport = null;
            try
            {
                transport = await connector.ConnectAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (transport is null)
            {
                await DelayAsync(RetryDelay, cancellationToken);
                continue;
            }

            await RunSessionAsync(transport, cancellationToken);
            await DelayAsync(RetryDelay, cancellationToken);
        }
    }

    private async Task RunSessionAsync(ILineTransport transport, CancellationToken cancellationToken)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var current = new DeviceSession(transport, timeProvider, appVersion);
        current.LineLogged += (direction, line) => LineLogged?.Invoke(direction, line);
        current.InputReceived += OnInput;
        current.HandshakeCompleted += _ => SendState();
        current.StateChanged += state =>
        {
            switch (state)
            {
                case SessionState.Connected:
                    SetStatus(ConnectionStatus.Connected);
                    break;
                case SessionState.Unresponsive:
                    SetStatus(ConnectionStatus.Unresponsive);
                    break;
                case SessionState.Incompatible:
                    SetStatus(ConnectionStatus.Incompatible);
                    break;
                case SessionState.Closed:
                    closed.TrySetResult();
                    break;
            }
        };

        session = current;
        current.Start();
        try
        {
            await closed.Task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            session = null;
            SetStatus(ConnectionStatus.Searching);
        }
    }

    private void OnInput(InputEvent input)
    {
        InputReceived?.Invoke(input);

        var binding = router.Route(input);
        if (binding is null)
        {
            return;
        }

        if (input is SliderInput)
        {
            var queue = sliderQueues.GetOrAdd(input.InputId, _ => new LatestWinsQueue<(Binding, InputEvent)>(item => ExecuteAsync(item.Item1, item.Item2)));
            _ = queue.Submit((binding, input));
            return;
        }

        _ = Task.Run(() => ExecuteAsync(binding, input));
    }

    private async Task ExecuteAsync(Binding binding, InputEvent input)
    {
        var result = await actionRunner.RunAsync(new ActionContext(binding, input), stopping.Token);
        ActionCompleted?.Invoke(binding, result);
        if (FeedbackBuilder.IsEnabled(Settings.Feedback, result))
        {
            ShowScreen(FeedbackBuilder.ForResult(binding, result));
        }
    }

    private void OnProfileChanged(ProfileConfig profile, bool userSwitch)
    {
        router.SetBindings(profile.Bindings);
        SendState();
        if (userSwitch && Settings.Feedback.ProfileChange)
        {
            ShowScreen(FeedbackBuilder.ForProfile(profile.Name));
        }
    }

    private void SendState()
    {
        var now = timeProvider.GetLocalNow().DateTime;
        session?.TrySend(DeviceMessages.State(new DeviceState(Profile: profiles.Active.Name, Now: now)));
    }

    private void SetStatus(ConnectionStatus status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        StatusChanged?.Invoke(status);
    }

    private async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, timeProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
