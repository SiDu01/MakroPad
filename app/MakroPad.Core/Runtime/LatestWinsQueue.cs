namespace MakroPad.Core.Runtime;

/// <summary>
/// Runs work one item at a time. While a run is in progress, newer submissions replace any
/// waiting one, so a fast-moving slider never queues up stale values but its last value always runs.
/// Exceptions from the work are swallowed; the work is expected to report its own errors.
/// </summary>
public sealed class LatestWinsQueue<T>
{
    private readonly Func<T, Task> work;
    private readonly object gate = new();
    private bool running;
    private bool hasPending;
    private T? pending;

    public LatestWinsQueue(Func<T, Task> work)
    {
        this.work = work;
    }

    /// <summary>
    /// Returns the drain task when this call started processing, otherwise a completed task
    /// (the item was handed to the run already in progress).
    /// </summary>
    public Task Submit(T item)
    {
        lock (gate)
        {
            if (running)
            {
                pending = item;
                hasPending = true;
                return Task.CompletedTask;
            }

            running = true;
        }

        return DrainAsync(item);
    }

    private async Task DrainAsync(T item)
    {
        var current = item;
        while (true)
        {
            try
            {
                await work(current);
            }
            catch (Exception)
            {
                // Reported by the work itself.
            }

            lock (gate)
            {
                if (!hasPending)
                {
                    running = false;
                    return;
                }

                current = pending!;
                pending = default;
                hasPending = false;
            }
        }
    }
}
