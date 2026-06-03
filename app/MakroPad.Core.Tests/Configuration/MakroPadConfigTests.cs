using MakroPad.Core.Configuration;

namespace MakroPad.Core.Tests.Configuration;

public sealed class MakroPadConfigTests
{
    [Fact]
    public void CreateDefaultIncludesActionResultDisplayTriggers()
    {
        var config = MakroPadConfig.CreateDefault();

        Assert.True(config.AppSettings.DisplayTriggerDefaultsInitialized);

        var actionResultTriggers = config.AppSettings.DisplayTriggers
            .Where(trigger => trigger.EventType == "actionResult")
            .ToList();

        Assert.Equal(2, actionResultTriggers.Count);
        Assert.Contains(actionResultTriggers, trigger => trigger.ActionStatus == "ok");
        Assert.Contains(actionResultTriggers, trigger => trigger.ActionStatus == "failed");
        Assert.All(actionResultTriggers, trigger => Assert.Contains("{action.message}", trigger.Template));
    }
}
