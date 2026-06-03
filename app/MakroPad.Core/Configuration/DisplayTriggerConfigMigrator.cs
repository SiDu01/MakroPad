using MakroPad.Core.DisplayTriggers;

namespace MakroPad.Core.Configuration;

public static class DisplayTriggerConfigMigrator
{
    public static MakroPadConfig MigrateDefaults(MakroPadConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config = EnsureDefaultActionResultTriggers(config);
        return EnsureDefaultMediaChangedTrigger(config);
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
}
