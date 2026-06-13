using MakroPad.App.Services;

namespace MakroPad.App.Tests.Services;

public sealed class ProcessWindowNowPlayingProviderTests
{
    [Fact]
    public void ParsesArtistAndTitleFromSpotifyWindowTitle()
    {
        var track = ProcessWindowNowPlayingProvider.ParseSpotifyWindowTitle("Daft Punk - Get Lucky");

        Assert.NotNull(track);
        Assert.Equal("Daft Punk", track!.Artist);
        Assert.Equal("Get Lucky", track.Title);
    }

    [Fact]
    public void TrimsSurroundingWhitespaceAroundSeparator()
    {
        var track = ProcessWindowNowPlayingProvider.ParseSpotifyWindowTitle("  Adele   -   Hello  ");

        Assert.NotNull(track);
        Assert.Equal("Adele", track!.Artist);
        Assert.Equal("Hello", track.Title);
    }

    [Fact]
    public void KeepsTrailingSeparatorInsideTrackTitle()
    {
        var track = ProcessWindowNowPlayingProvider.ParseSpotifyWindowTitle("Queen - Bohemian Rhapsody - Remastered");

        Assert.NotNull(track);
        Assert.Equal("Queen", track!.Artist);
        Assert.Equal("Bohemian Rhapsody - Remastered", track.Title);
    }

    [Theory]
    [InlineData("Spotify")]
    [InlineData("Spotify Free")]
    [InlineData("Spotify Premium")]
    [InlineData("Advertisement")]
    [InlineData("spotify premium")]
    public void IgnoresIdleAndAdvertisementTitles(string title)
    {
        Assert.Null(ProcessWindowNowPlayingProvider.ParseSpotifyWindowTitle(title));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("No separator here")]
    [InlineData(" - Get Lucky")]
    [InlineData("Daft Punk - ")]
    public void ReturnsNullForUnparseableTitles(string? title)
    {
        Assert.Null(ProcessWindowNowPlayingProvider.ParseSpotifyWindowTitle(title));
    }
}
