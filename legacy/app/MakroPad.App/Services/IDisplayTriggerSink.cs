using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.App.Services;

public interface IDisplayTriggerSink
{
    Task SendTemporaryTemplateAsync(
        IReadOnlyList<DisplayTemplateLine> lines,
        int durationMs,
        CancellationToken cancellationToken = default);

    Task SendTemporaryLayoutAsync(
        DisplayLayoutScreen screen,
        int durationMs,
        CancellationToken cancellationToken = default);
}
