using MakroPad.App.Services;

namespace MakroPad.App.Tests.Services;

public sealed class EventLogServiceTests
{
    [Fact]
    public void AddKeepsNewestEntriesWithinCapacity()
    {
        var service = new EventLogService(2);

        service.Add("first");
        service.Add("second");
        service.Add("third");

        Assert.Equal(["third", "second"], service.Entries);
    }
}
