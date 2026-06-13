using System.Linq;
using MakroPad.Core.Configuration;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.Core.Tests.DisplayTriggers;

public sealed class DisplayTriggerRendererTests
{
    [Fact]
    public void FitPreviewLinesClipsToOledPreviewWidth()
    {
        var lines = DisplayTriggerRenderer.FitPreviewLines(
        [
            "Short",
            "This line is definitely longer than twenty one characters"
        ]);

        Assert.Equal("Short", lines[0]);
        Assert.Equal(21, lines[1].Length);
        Assert.EndsWith("..", lines[1]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(2047, 50)]
    [InlineData(2048, 50)]
    [InlineData(4095, 100)]
    [InlineData(5000, 100)]
    public void MapSliderValueToPercentClampsAndRounds(int value, int expected)
    {
        Assert.Equal(expected, DisplayTriggerRenderer.MapSliderValueToPercent(value));
    }

    [Theory]
    [InlineData("slider:1", true, 1)]
    [InlineData("button:42", true, 42)]
    [InlineData("custom:7", true, 7)]
    [InlineData("slider:0", false, 0)]
    [InlineData("slider:x", false, 0)]
    [InlineData("slider", false, 0)]
    public void TryParseInputIdParsesPositiveSuffix(string input, bool expectedResult, int expectedId)
    {
        var result = DisplayTriggerRenderer.TryParseInputId(input, out var id);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedId, id);
    }

    [Fact]
    public void ReplacePlaceholdersReplacesAllSupportedTokensCaseInsensitively()
    {
        var context = new DisplayTriggerRenderContext
        {
            Input = "slider:3",
            SliderValue = 2048,
            SliderPercent = 50,
            ActionStatus = "ok",
            ActionName = "media.playPause",
            ActionMessage = "Done",
            MediaArtist = "Artist",
            MediaTitle = "Title",
            ConnectionStatus = "connected",
            Profile = "Studio",
            Status = "Ready"
        };
        const string template =
            "{INPUT}|{slider.id}|{slider.value}|{slider.percent}|{action.status}|{action.name}|{action.message}|"
            + "{media.artist}|{media.title}|{media.track}|{connection.status}|{profile}|{status}";

        var result = DisplayTriggerRenderer.ReplacePlaceholders(template, context);

        Assert.Equal(
            "slider:3|3|2048|50|ok|media.playPause|Done|Artist|Title|Artist - Title|connected|Studio|Ready",
            result);
    }

    [Fact]
    public void ReplacePlaceholdersUsesEmptyValuesAndTitleOnlyTrack()
    {
        var context = new DisplayTriggerRenderContext
        {
            Input = "*",
            MediaTitle = "Title"
        };

        var result = DisplayTriggerRenderer.ReplacePlaceholders(
            "{slider.id}|{slider.value}|{slider.percent}|{media.track}",
            context);

        Assert.Equal("|||Title", result);
    }

    [Fact]
    public void RenderHtmlLinesConvertsKnownTagsStripsOthersAndDecodesEntities()
    {
        const string html = "<h1>Title</h1><div>A<br/>B</div><ul><li>One</li><li>Two &amp; Three</li></ul>";

        var result = DisplayTriggerRenderer.RenderHtmlLines(html);

        Assert.Equal(["Title", "A", "B", "- One", "- Two & Three"], result.Select(line => line.Text));
        Assert.Equal(2, result[0].NormalizedTextSize);
    }

    [Fact]
    public void RenderHtmlLinesNumbersOrderedListsAndRendersDivider()
    {
        const string html = "<ol><li>First</li><li>Second</li></ol><hr><p>End</p>";

        var result = DisplayTriggerRenderer.RenderHtmlLines(html).Select(line => line.Text).ToList();

        Assert.Equal("1. First", result[0]);
        Assert.Equal("2. Second", result[1]);
        Assert.StartsWith("---", result[2]);
        Assert.Equal("End", result[3]);
    }

    [Fact]
    public void RenderHtmlLinesKeepsTextOfInlineTags()
    {
        var result = DisplayTriggerRenderer.RenderHtmlLines("<p>Status: <b>Connected</b></p>");

        Assert.Equal("Status: Connected", result[0].Text);
    }

    [Fact]
    public void RenderMarkdownLinesParsesHeadingsListsAndDividers()
    {
        const string markdown = "# Title\nStatus: ok\n---\n- One\n* Two";

        var result = DisplayTriggerRenderer.RenderMarkdownLines(markdown);

        Assert.Equal("Title", result[0].Text);
        Assert.Equal(2, result[0].NormalizedTextSize);
        Assert.Equal("Status: ok", result[1].Text);
        Assert.StartsWith("---", result[2].Text);
        Assert.Equal("- One", result[3].Text);
        Assert.Equal("- Two", result[4].Text);
    }

    [Fact]
    public void RenderTemplateLinesSupportsMarkdownMode()
    {
        var trigger = new DisplayTriggerConfig
        {
            TemplateMode = "markdown",
            Template = "# {profile}\n- {status}"
        };
        var context = new DisplayTriggerRenderContext
        {
            Profile = "Gaming",
            Status = "Connected"
        };

        var result = DisplayTriggerRenderer.RenderTemplateLines(trigger, context);

        Assert.Equal("Gaming", result[0].Text);
        Assert.Equal(2, result[0].NormalizedTextSize);
        Assert.Equal("- Connected", result[1].Text);
    }

    [Fact]
    public void RenderTemplateLinesMapsHtmlHeadingsToLargeText()
    {
        var trigger = new DisplayTriggerConfig
        {
            TemplateMode = "html",
            Template = "<h1>{media.title}</h1><div>Artist</div>"
        };
        var context = new DisplayTriggerRenderContext
        {
            MediaTitle = "Long Track Title"
        };

        var result = DisplayTriggerRenderer.RenderTemplateLines(trigger, context);

        Assert.Equal("Long Track Title", result[0].Text);
        Assert.Equal(2, result[0].NormalizedTextSize);
        Assert.Equal("Artist", result[1].Text);
        Assert.Equal(1, result[1].NormalizedTextSize);
    }

    [Fact]
    public void FitPreviewLinesUsesTextSizeWidth()
    {
        var result = DisplayTriggerRenderer.FitPreviewLines(
        [
            new DisplayTemplateLine("This large title is too long", 2)
        ]);

        Assert.Equal(10, result.Single().Length);
        Assert.EndsWith("..", result.Single());
    }

    [Fact]
    public void RenderLinesNormalizesLineEndingsTrimsRemovesEmptyLinesAndLimitsToFive()
    {
        var trigger = new DisplayTriggerConfig
        {
            Template = " one \r\n\r\n two \r three\nfour\nfive\nsix "
        };

        var result = DisplayTriggerRenderer.RenderLines(trigger, new DisplayTriggerRenderContext());

        Assert.Equal(["one", "two", "three", "four", "five"], result);
    }

    [Fact]
    public void RenderLinesKeepsLongLinesForFirmwareScrolling()
    {
        const string longLine = "This line should scroll across the OLED instead of being clipped";
        var trigger = new DisplayTriggerConfig
        {
            Template = longLine
        };

        var result = DisplayTriggerRenderer.RenderLines(trigger, new DisplayTriggerRenderContext());

        Assert.Equal([longLine], result);
    }

    [Fact]
    public void RenderLinesAppliesHtmlModeAfterPlaceholderReplacement()
    {
        var trigger = new DisplayTriggerConfig
        {
            TemplateMode = "HTML",
            Template = "<p>{media.artist}</p><div>{media.title}<br>{slider.percent}%</div>"
        };
        var context = new DisplayTriggerRenderContext
        {
            MediaArtist = "Artist &amp; Co.",
            MediaTitle = "Track",
            SliderPercent = 75
        };

        var result = DisplayTriggerRenderer.RenderLines(trigger, context);

        Assert.Equal(["Artist & Co.", "Track", "75%"], result);
    }

    [Fact]
    public void RenderLinesKeepsHtmlLikeTextLiteralInNormalMode()
    {
        var trigger = new DisplayTriggerConfig
        {
            TemplateMode = "normal",
            Template = "<b>Value</b>"
        };

        var result = DisplayTriggerRenderer.RenderLines(trigger, new DisplayTriggerRenderContext());

        Assert.Equal(["<b>Value</b>"], result);
    }

    [Fact]
    public void RenderLayoutScreenMapsSliderInputToVolumeLayout()
    {
        var trigger = new DisplayTriggerConfig
        {
            EventType = "input",
            Input = "slider:1",
            TemplateMode = "layout",
            Template = "Volume\nSystem"
        };
        var context = new DisplayTriggerRenderContext
        {
            Input = "slider:1",
            SliderPercent = 72
        };

        var result = DisplayTriggerRenderer.RenderLayoutScreen(trigger, context);

        Assert.Equal("volume", result.NormalizedLayout);
        Assert.Equal("Volume", result.Title);
        Assert.Equal("System", result.Line1);
        Assert.Equal(72, result.NormalizedValuePercent);
    }

    [Fact]
    public void RenderLayoutScreenMapsFailedActionToWarningIcon()
    {
        var trigger = new DisplayTriggerConfig
        {
            EventType = "actionResult",
            ActionStatus = "failed",
            TemplateMode = "layout",
            Template = "Action failed\nOBS missing"
        };
        var context = new DisplayTriggerRenderContext
        {
            ActionStatus = "failed"
        };

        var result = DisplayTriggerRenderer.RenderLayoutScreen(trigger, context);

        Assert.Equal("action", result.NormalizedLayout);
        Assert.Equal("warn", result.Icon);
        Assert.Equal("OBS missing", result.Line1);
    }
}
