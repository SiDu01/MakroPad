using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MakroPad.App.Services;

public interface IDisplayTriggerSink
{
    Task SendTemporaryTemplateAsync(
        IReadOnlyList<string> lines,
        int durationMs,
        CancellationToken cancellationToken = default);
}
