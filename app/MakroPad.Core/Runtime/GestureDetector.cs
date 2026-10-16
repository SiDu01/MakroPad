using MakroPad.Core.Device;

namespace MakroPad.Core.Runtime;

/// <summary>
/// Turns key down/up events into presses, long presses and double presses. A key only waits
/// when a long or double press is bound for it; otherwise it fires on key down as before:
/// <list type="bullet">
/// <item>Long press bound: the press fires on release, the long press after <see cref="LongPress"/> while held.</item>
/// <item>Double press bound: the press fires <see cref="DoublePress"/> after release unless a second press comes first.</item>
/// </list>
/// </summary>
public sealed class GestureDetector
{
    private readonly TimeProvider timeProvider;
    private readonly Func<int, (bool Long, bool Double)> boundGestures;
    private readonly Dictionary<int, KeyState> keys = [];
    private readonly object gate = new();

    public GestureDetector(TimeProvider timeProvider, Func<int, (bool Long, bool Double)> boundGestures)
    {
        this.timeProvider = timeProvider;
        this.boundGestures = boundGestures;
    }

    public TimeSpan LongPress { get; set; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan DoublePress { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Raised on the calling or a timer thread.</summary>
    public event Action<GestureInput>? Detected;

    public void OnButton(ButtonInput input)
    {
        var emit = new List<GestureInput>();
        lock (gate)
        {
            if (!keys.TryGetValue(input.Id, out var key))
            {
                key = new KeyState();
                keys[input.Id] = key;
            }

            if (input.Pressed)
            {
                OnDown(input.Id, key, emit);
            }
            else
            {
                OnUp(input.Id, key, emit);
            }
        }

        foreach (var gesture in emit)
        {
            Detected?.Invoke(gesture);
        }
    }

    private void OnDown(int id, KeyState key, List<GestureInput> emit)
    {
        var (bindsLong, bindsDouble) = boundGestures(id);
        key.Held = true;
        key.Consumed = false;

        if (key.WaitingForSecond)
        {
            key.CancelTimer();
            key.WaitingForSecond = false;
            if (bindsDouble)
            {
                key.Consumed = true;
                emit.Add(new GestureInput(id, Gesture.DoublePress));
                return;
            }

            // The gesture binding disappeared meanwhile: deliver the pending press first.
            emit.Add(new GestureInput(id, Gesture.Press));
        }

        if (!bindsLong && !bindsDouble)
        {
            key.Consumed = true;
            emit.Add(new GestureInput(id, Gesture.Press));
            return;
        }

        if (bindsLong)
        {
            var generation = ++key.Generation;
            key.Timer = timeProvider.CreateTimer(_ => OnLongTimer(id, generation), null, LongPress, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnUp(int id, KeyState key, List<GestureInput> emit)
    {
        key.Held = false;
        key.CancelTimer();
        if (key.Consumed)
        {
            return;
        }

        if (boundGestures(id).Double)
        {
            key.WaitingForSecond = true;
            var generation = ++key.Generation;
            key.Timer = timeProvider.CreateTimer(_ => OnDoubleTimeout(id, generation), null, DoublePress, Timeout.InfiniteTimeSpan);
            return;
        }

        emit.Add(new GestureInput(id, Gesture.Press));
    }

    private void OnLongTimer(int id, int generation)
    {
        lock (gate)
        {
            if (!keys.TryGetValue(id, out var key) || key.Generation != generation || !key.Held || key.Consumed)
            {
                return;
            }

            key.Consumed = true;
        }

        Detected?.Invoke(new GestureInput(id, Gesture.LongPress));
    }

    private void OnDoubleTimeout(int id, int generation)
    {
        lock (gate)
        {
            if (!keys.TryGetValue(id, out var key) || key.Generation != generation || !key.WaitingForSecond)
            {
                return;
            }

            key.WaitingForSecond = false;
        }

        Detected?.Invoke(new GestureInput(id, Gesture.Press));
    }

    private sealed class KeyState
    {
        public bool Held { get; set; }

        /// <summary>The current press already produced its gesture; its release does nothing.</summary>
        public bool Consumed { get; set; }

        public bool WaitingForSecond { get; set; }

        /// <summary>Invalidates timers that already fired but have not taken the lock yet.</summary>
        public int Generation { get; set; }

        public ITimer? Timer { get; set; }

        public void CancelTimer()
        {
            Timer?.Dispose();
            Timer = null;
            Generation++;
        }
    }
}
