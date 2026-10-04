using System.Threading;
using System.Threading.Tasks;

namespace MakroPad.App.Services;

public interface INowPlayingProvider
{
    Task<NowPlayingTrack?> GetCurrentTrackAsync(CancellationToken cancellationToken = default);
}
