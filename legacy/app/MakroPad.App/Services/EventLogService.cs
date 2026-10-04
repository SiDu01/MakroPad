using System.Collections.ObjectModel;

namespace MakroPad.App.Services;

public sealed class EventLogService
{
    private readonly int capacity;

    public EventLogService(int capacity = 200)
    {
        this.capacity = capacity;
    }

    public ObservableCollection<string> Entries { get; } = [];

    public void Add(string message)
    {
        Entries.Insert(0, message);

        while (Entries.Count > capacity)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }
    }
}
