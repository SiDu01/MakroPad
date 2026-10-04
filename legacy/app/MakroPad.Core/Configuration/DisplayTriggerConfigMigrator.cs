using MakroPad.Core.DisplayTriggers;

namespace MakroPad.Core.Configuration;

public static class DisplayTriggerConfigMigrator
{
    public static MakroPadConfig MigrateDefaults(MakroPadConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config = EnsureDefaultActionResultTriggers(config);
        config = EnsureDefaultMediaChangedTrigger(config);
        config = EnsureDefaultIdleScreen(config);
        config = EnsureIdleScreenConditions(config);
        return UpgradeLegacyDefaultTriggerModes(config);
    }

    /// <summary>
    /// Converts the legacy <c>Activation</c>/<c>ProcessName</c> idle-screen fields into the
    /// condition-based model so existing configs behave identically: a "process" screen becomes a
    /// single <c>processRunning</c> condition with a higher priority (so it still beats defaults),
    /// and a "default" screen becomes an unconditional (always eligible) screen.
    /// </summary>
    private static MakroPadConfig EnsureIdleScreenConditions(MakroPadConfig config)
    {
        if (config.AppSettings.IdleScreensConditionsInitialized)
        {
            return config;
        }

        var migrated = config.AppSettings.IdleScreens
            .Select(MigrateIdleScreen)
            .ToList();

        return config with
        {
            AppSettings = config.AppSettings with
            {
                IdleScreensConditionsInitialized = true,
                IdleScreens = migrated
            }
        };
    }

    private static IdleScreenConfig MigrateIdleScreen(IdleScreenConfig screen)
    {
        // Already condition-based (hand-authored or previously migrated) — leave untouched.
        if (screen.Conditions.Count > 0)
        {
            return screen;
        }

        var isProcess = string.Equals(screen.Activation, "process", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(screen.ProcessName);
        if (!isProcess)
        {
            return screen;
        }

        return screen with
        {
            Conditions =
            [
                new IdleScreenCondition { Type = "processRunning", Target = screen.ProcessName.Trim() }
            ],
            Priority = 10
        };
    }

    private static MakroPadConfig EnsureDefaultIdleScreen(MakroPadConfig config)
    {
        if (config.AppSettings.IdleScreensInitialized)
        {
            return config;
        }

        var display = config.Hardware.Display;
        var rows = display.DefaultLayoutRows == 5 ? 5 : 3;
        var fields = new (string Text, int Size)[]
        {
            (display.DefaultTitle, display.DefaultTitleSize),
            (display.DefaultLine1, display.DefaultLine1Size),
            (display.DefaultLine2, display.DefaultLine2Size),
            (display.DefaultLine3, display.DefaultLine3Size),
            (display.DefaultLine4, display.DefaultLine4Size)
        };

        var template = string.Join(
            "\n",
            fields
                .Take(rows)
                .Where(field => !string.IsNullOrWhiteSpace(field.Text))
                .Select(field => field.Size >= 2 ? "# " + field.Text.Trim() : field.Text.Trim()));

        if (string.IsNullOrWhiteSpace(template))
        {
            template = "{profile}\n{status}";
        }

        var standard = new IdleScreenConfig
        {
            Name = "Standard",
            Enabled = true,
            Template = template,
            TemplateMode = "markdown",
            Activation = "default"
        };

        return config with
        {
            AppSettings = config.AppSettings with
            {
                IdleScreensInitialized = true,
                IdleScreens = [standard]
            }
        };
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
