using MakroPad.Core.DisplayTriggers;

namespace MakroPad.Core.Configuration;

public static class DisplayTriggerConfigMigrator
{
    public static MakroPadConfig MigrateDefaults(MakroPadConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config = EnsureDefaultActionResultTriggers(config);
        config = EnsureDefaultMediaChangedTrigger(config);
        return UpgradeLegacyDefaultTriggerModes(config);
    }

    private static MakroPadConfig EnsureDefaultActionResultTriggers(MakroPadConfig config)
    {
        if (config.AppSettings.DisplayTriggerDefaultsInitialized)
        {
            return config;
        }

        var triggers = config.AppSettings.DisplayTriggers.ToList();
        if (!triggers.Any(trigger =>
                DisplayTriggerNormalizer.NormalizeEventType(trigger.EventType) == "actionResult"))
        {
            triggers.Add(
                new DisplayTriggerConfig
                {
                    Enabled = true,
                    EventType = "actionResult",
                    Input = "*",
                    ActionStatus = "ok",
                    DurationMs = config.Hardware.Display.ActionResultTimeoutMs,
                    TemplateMode = "layout",
                    Template = "Action\n{action.message}"
                });
            triggers.Add(
                new DisplayTriggerConfig
                {
                    Enabled = true,
                    EventType = "actionResult",
                    Input = "*",
                    ActionStatus = "failed",
                    DurationMs = config.Hardware.Display.ActionResultTimeoutMs,
                    TemplateMode = "layout",
                    Template = "Action failed\n{action.message}"
                });
        }

        return config with
        {
            AppSettings = config.AppSettings with
            {
                DisplayTriggerDefaultsInitialized = true,
                DisplayTriggers = triggers
            }
        };
    }

    private static MakroPadConfig EnsureDefaultMediaChangedTrigger(MakroPadConfig config)
    {
        if (config.AppSettings.MediaTriggerDefaultsInitialized)
        {
            return config;
        }

        var triggers = config.AppSettings.DisplayTriggers.ToList();
        if (!triggers.Any(trigger =>
                DisplayTriggerNormalizer.NormalizeEventType(trigger.EventType) == "mediaChanged"))
        {
            triggers.Add(
                new DisplayTriggerConfig
                {
                    Enabled = true,
                    EventType = "mediaChanged",
                    Input = "*",
                    DurationMs = 0,
                    TemplateMode = "layout",
                    Template = "Media\n{media.artist}\n{media.title}"
                });
        }

        return config with
        {
            AppSettings = config.AppSettings with
            {
                MediaTriggerDefaultsInitialized = true,
                DisplayTriggers = triggers
            }
        };
    }

    private static MakroPadConfig UpgradeLegacyDefaultTriggerModes(MakroPadConfig config)
    {
        var changed = false;
        var triggers = config.AppSettings.DisplayTriggers
            .Select(trigger =>
            {
                if (IsLegacyDefaultTrigger(trigger))
                {
                    changed = true;
                    return trigger with { TemplateMode = "layout" };
                }

                return trigger;
            })
            .ToList();

        return changed
            ? config with
            {
                AppSettings = config.AppSettings with { DisplayTriggers = triggers }
            }
            : config;
    }

    private static bool IsLegacyDefaultTrigger(DisplayTriggerConfig trigger)
    {
        var eventType = DisplayTriggerNormalizer.NormalizeEventType(trigger.EventType);
        var templateMode = DisplayTriggerNormalizer.NormalizeTemplateMode(trigger.TemplateMode);
        if (templateMode != "normal")
        {
            return false;
        }

        if (eventType == "actionResult"
            && trigger.Input == "*"
            && DisplayTriggerNormalizer.NormalizeActionStatus(trigger.ActionStatus) is "ok"
            && trigger.Template == "Action\n{action.message}")
        {
            return true;
        }

        if (eventType == "actionResult"
            && trigger.Input == "*"
            && DisplayTriggerNormalizer.NormalizeActionStatus(trigger.ActionStatus) is "failed"
            && trigger.Template == "Action failed\n{action.message}")
        {
            return true;
        }

        return eventType == "mediaChanged"
            && trigger.Input == "*"
            && trigger.Template == "Media\n{media.artist}\n{media.title}";
    }
}
