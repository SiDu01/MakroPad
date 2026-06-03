using MakroPad.Actions;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.Services;

public sealed class ActionFeedbackCoordinatorTests
{
    [Fact]
    public void FormatActionResultReturnsPipelineMessageForUnboundInput()
    {
        var result = ActionPipelineResult.Unbound("button:99", "Studio");

        var message = ActionFeedbackCoordinator.FormatActionResult(result);

        Assert.Equal("No action binding for button:99 in profile Studio.", message);
    }

    [Theory]
    [InlineData(true, "Action matched: button:1 -> log (Done)")]
    [InlineData(false, "Action failed: button:1 -> log (Done)")]
    public void FormatActionResultDescribesExecutedAction(bool succeeded, string expected)
    {
        var binding = new ActionBinding { Input = "button:1", Action = "log" };
        var executionResult = new ActionExecutionResult(succeeded, "Done");
        var result = ActionPipelineResult.Executed("button:1", binding, executionResult);

        var message = ActionFeedbackCoordinator.FormatActionResult(result);

        Assert.Equal(expected, message);
    }

    [Fact]
    public void FormatActionResultReturnsPipelineMessageWhenExecutionDetailsAreMissing()
    {
        var result = new ActionPipelineResult(
            ActionPipelineResultKind.Executed,
            "button:1",
            "Pipeline message");

        var message = ActionFeedbackCoordinator.FormatActionResult(result);

        Assert.Equal("Pipeline message", message);
    }

    [Fact]
    public async Task HandleAsyncWithoutActiveConnectionReturnsLogWithoutSendingFeedback()
    {
        var sink = new RejectingDisplayTriggerSink();
        using var orchestrator = new DisplayTriggerOrchestrator(sink);
        var deviceSessionService = new DeviceSessionService();
        var coordinator = new ActionFeedbackCoordinator(
            CreatePipeline(),
            orchestrator,
            deviceSessionService);
        var config = CreateLogActionConfig();

        var result = await coordinator.HandleAsync(
            config,
            new DisplayTriggerRuntimeState("Default", "Disconnected"),
            "button:1");

        Assert.Equal("Action matched: button:1 -> log (Test feedback)", result.LogMessage);
        Assert.False(result.SuppressInputDisplayTriggers);
        Assert.Equal("", result.ErrorMessage);
        Assert.Equal(0, sink.SendAttempts);
        Assert.Null(deviceSessionService.ActiveConnection);
    }

    private static ActionPipeline CreatePipeline()
    {
        return new ActionPipeline(
            new ActionResolver(),
            new ActionExecutor(
                new NoopSystemMediaController(),
                new NoopSystemVolumeController(),
                new NoopApplicationVolumeController(),
                new NoopKeyboardController(),
                new FakeSystemLauncher()));
    }

    private static MakroPadConfig CreateLogActionConfig()
    {
        return MakroPadConfig.CreateDefault() with
        {
            Profiles =
            [
                new ProfileConfig
                {
                    Name = "Default",
                    Bindings =
                    [
                        new ActionBinding
                        {
                            Input = "button:1",
                            Action = "log",
                            Parameters = new Dictionary<string, string>
                            {
                                ["message"] = "Test feedback"
                            }
                        }
                    ]
                }
            ]
        };
    }

    private sealed class RejectingDisplayTriggerSink : IDisplayTriggerSink
    {
        public int SendAttempts { get; private set; }

        public Task SendTemporaryTemplateAsync(
            IReadOnlyList<string> lines,
            int durationMs,
            CancellationToken cancellationToken = default)
        {
            SendAttempts++;
            throw new InvalidOperationException("Display feedback must not be sent without a connection.");
        }
    }

    private sealed class FakeSystemLauncher : ISystemLauncher
    {
        public Task<ActionExecutionResult> OpenUrlAsync(
            string url,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ActionExecutionResult> OpenAppAsync(
            string target,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
