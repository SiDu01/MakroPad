using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MakroPad.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Protocol;

namespace MakroPad.App.Services;

public sealed class ActionFeedbackCoordinator
{
    private readonly ActionPipeline actionPipeline;
    private readonly DisplayTriggerOrchestrator displayTriggerOrchestrator;
    private readonly DeviceSessionService deviceSessionService;

    public ActionFeedbackCoordinator(
        ActionPipeline actionPipeline,
        DisplayTriggerOrchestrator displayTriggerOrchestrator,
        DeviceSessionService deviceSessionService)
    {
        this.actionPipeline = actionPipeline;
        this.displayTriggerOrchestrator = displayTriggerOrchestrator;
        this.deviceSessionService = deviceSessionService;
    }

    public async Task<ActionFeedbackResult> HandleAsync(
        MakroPadConfig config,
        DisplayTriggerRuntimeState displayState,
        string input,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        var result = await actionPipeline.HandleInputAsync(config, input, value, cancellationToken);
        if (result.Kind == ActionPipelineResultKind.Throttled)
        {
            return ActionFeedbackResult.Throttled;
        }

        var logMessage = FormatActionResult(result);
        if (deviceSessionService.ActiveConnection is null
            || !result.ShouldSendFeedback
            || result.ExecutionResult is null)
        {
            return new ActionFeedbackResult(logMessage, false, "");
        }

        var status = result.ExecutionResult.Succeeded ? "ok" : "failed";
        var actionName = result.Binding?.Action ?? "";
        var errorMessage = "";
        try
        {
            var triggerResult = await displayTriggerOrchestrator.HandleActionResultAsync(
                config.AppSettings.DisplayTriggers,
                displayState,
                input,
                status,
                actionName,
                result.ExecutionResult.Message,
                cancellationToken);
            if (triggerResult.Handled)
            {
                return new ActionFeedbackResult(logMessage, true, "");
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Action result display trigger failed: {ex.Message}";
        }

        try
        {
            await deviceSessionService.SendLineAsync(
                ProtocolCodec.Format(
                    "action-result",
                    new Dictionary<string, string?>
                    {
                        ["input"] = input,
                        ["status"] = status,
                        ["message"] = result.ExecutionResult.Message
                    }),
                cancellationToken);
            return new ActionFeedbackResult(logMessage, true, errorMessage);
        }
        catch (Exception ex)
        {
            var separator = string.IsNullOrWhiteSpace(errorMessage) ? "" : " ";
            return new ActionFeedbackResult(
                logMessage,
                false,
                $"{errorMessage}{separator}Action feedback failed: {ex.Message}");
        }
    }

    public static string FormatActionResult(ActionPipelineResult result)
    {
        if (result.Kind == ActionPipelineResultKind.Unbound)
        {
            return result.Message;
        }

        if (result.Binding is null || result.ExecutionResult is null)
        {
            return result.Message;
        }

        return result.ExecutionResult.Succeeded
            ? $"Action matched: {result.Input} -> {result.Binding.Action} ({result.ExecutionResult.Message})"
            : $"Action failed: {result.Input} -> {result.Binding.Action} ({result.ExecutionResult.Message})";
    }
}

public sealed record ActionFeedbackResult(
    string LogMessage,
    bool SuppressInputDisplayTriggers,
    string ErrorMessage)
{
    public static ActionFeedbackResult Throttled { get; } = new("", false, "");
}
