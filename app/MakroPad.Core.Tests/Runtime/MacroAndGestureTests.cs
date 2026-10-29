using System.Net;
using MakroPad.Core.Actions;
using MakroPad.Core.Configuration;
using MakroPad.Core.Device;
using MakroPad.Core.Runtime;
using Microsoft.Extensions.Time.Testing;

namespace MakroPad.Core.Tests.Runtime;

public sealed class GestureDetectorTests
{
    private readonly FakeTimeProvider time = new();
    private readonly List<string> detected = [];
    private (bool Long, bool Double) bound;
    private readonly GestureDetector detector;

    public GestureDetectorTests()
    {
        detector = new GestureDetector(time, _ => bound);
        detector.Detected += g => detected.Add(g.InputId);
    }

    private void Down() => detector.OnButton(new ButtonInput(3, true));

    private void Up() => detector.OnButton(new ButtonInput(3, false));

    [Fact]
    public void WithoutGestureBindingsPressFiresOnKeyDown()
    {
        Down();
        Assert.Equal(["button:3"], detected);
        Up();
        Assert.Single(detected);
    }

    [Fact]
    public void LongPressFiresWhileHeldAndSuppressesThePress()
    {
        bound = (true, false);
        Down();
        time.Advance(TimeSpan.FromMilliseconds(499));
        Assert.Empty(detected);
        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(["button:3:long"], detected);
        Up();
        Assert.Single(detected);
    }

    [Fact]
    public void ShortPressWithLongBindingFiresOnRelease()
    {
        bound = (true, false);
        Down();
        time.Advance(TimeSpan.FromMilliseconds(200));
        Up();
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(["button:3"], detected);
    }

    [Fact]
    public void DoublePressReplacesTwoPresses()
    {
        bound = (false, true);
        Down();
        Up();
        time.Advance(TimeSpan.FromMilliseconds(150));
        Down();
        Up();
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(["button:3:double"], detected);
    }

    [Fact]
    public void SinglePressWithDoubleBindingFiresAfterTheWindow()
    {
        bound = (false, true);
        Down();
        Up();
        time.Advance(TimeSpan.FromMilliseconds(299));
        Assert.Empty(detected);
        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(["button:3"], detected);
    }

    [Fact]
    public void AllThreeGesturesOnOneKey()
    {
        bound = (true, true);
        detector.LongPress = TimeSpan.FromMilliseconds(400);

        Down();
        time.Advance(TimeSpan.FromMilliseconds(450));
        Up();
        Down();
        Up();
        time.Advance(TimeSpan.FromMilliseconds(100));
        Down();
        Up();
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(["button:3:long", "button:3:double"], detected);
    }
}

public sealed class MacroRunnerTests
{
    private readonly FakeTimeProvider time = new();
    private readonly List<string> calls = [];
    private readonly ActionRunner runner;

    public MacroRunnerTests()
    {
        runner = new ActionRunner([new Recorder("a", calls), new Recorder("b", calls), new Failing()], time);
    }

    private static Binding Macro(params MacroStep[] steps) => new("button:1", "macro", Steps: steps);

    private static MacroStep Step(string action, string? ms = null)
        => new(action, ms is null ? null : new Dictionary<string, string> { ["ms"] = ms });

    [Fact]
    public async Task StepsRunInOrderWithDelays()
    {
        var run = runner.RunAsync(new ActionContext(Macro(Step("a"), Step("delay", "200"), Step("b")), new ButtonInput(1, true)));

        await Task.Yield();
        Assert.Equal(["a"], calls);
        time.Advance(TimeSpan.FromMilliseconds(199));
        Assert.Equal(["a"], calls);
        time.Advance(TimeSpan.FromMilliseconds(1));

        var result = await run;
        Assert.Equal(["a", "b"], calls);
        Assert.True(result.Success);
        Assert.Equal("Makro, 2 Aktionen", result.Message);
    }

    [Fact]
    public async Task FailingStepStopsTheMacro()
    {
        var result = await runner.RunAsync(new ActionContext(Macro(Step("a"), Step("fail"), Step("b")), new ButtonInput(1, true)));

        Assert.False(result.Success);
        Assert.Equal("Schritt 2: kaputt", result.Message);
        Assert.Equal(["a"], calls);
    }

    [Fact]
    public async Task NestedMacrosAndEmptyMacrosFail()
    {
        Assert.False((await runner.RunAsync(new ActionContext(Macro(Step("macro")), new ButtonInput(1, true)))).Success);
        Assert.False((await runner.RunAsync(new ActionContext(Macro(), new ButtonInput(1, true)))).Success);
    }

    [Fact]
    public async Task ToggleRunsOneStepPerPressAndRetriesAFailedOne()
    {
        var toggle = new Binding("button:1", "toggle", Steps: [Step("a"), Step("fail"), Step("b")]);
        async Task<ActionResult> Press() => await runner.RunAsync(new ActionContext(toggle, new ButtonInput(1, true)));

        Assert.Equal("a", (await Press()).Message);
        Assert.Equal("Schritt 2: kaputt", (await Press()).Message);
        Assert.Equal("Schritt 2: kaputt", (await Press()).Message);
        Assert.Equal(["a"], calls);

        var other = new Binding("button:2", "toggle", Steps: [Step("b"), Step("a")]);
        await runner.RunAsync(new ActionContext(other, new ButtonInput(2, true)));
        await runner.RunAsync(new ActionContext(other, new ButtonInput(2, true)));
        await runner.RunAsync(new ActionContext(other, new ButtonInput(2, true)));
        Assert.Equal(["a", "b", "a", "b"], calls);
    }

    private sealed class Recorder(string id, List<string> calls) : IActionHandler
    {
        public string ActionId => id;

        public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
        {
            calls.Add(id);
            return Task.FromResult(ActionResult.Ok(id));
        }
    }

    private sealed class Failing : IActionHandler
    {
        public string ActionId => "fail";

        public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.Failed("kaputt"));
    }
}

public sealed class HttpRequestActionTests
{
    private static async Task<(ActionResult Result, HttpRequestMessage? Request, string? Body)> Send(
        Dictionary<string, string> parameters, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeHandler(status);
        var action = new HttpRequestAction(new HttpClient(handler));
        var result = await action.ExecuteAsync(new ActionContext(new Binding("button:1", "http.request", parameters), new ButtonInput(1, true)), default);
        return (result, handler.Request, handler.Body);
    }

    [Fact]
    public async Task GetByDefaultAndPostWithBody()
    {
        var get = await Send(new() { ["url"] = "http://ha.local/api" });
        Assert.Equal(HttpMethod.Get, get.Request!.Method);
        Assert.Equal("ha.local: 200", get.Result.Message);

        var post = await Send(new() { ["url"] = "http://ha.local/api", ["body"] = "{\"on\":true}" });
        Assert.Equal(HttpMethod.Post, post.Request!.Method);
        Assert.Equal("{\"on\":true}", post.Body);
        Assert.Equal("application/json", post.Request.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task ErrorStatusAndBadUrlFail()
    {
        var notFound = await Send(new() { ["url"] = "https://x.test/a", ["method"] = "put" }, HttpStatusCode.NotFound);
        Assert.False(notFound.Result.Success);
        Assert.Equal(HttpMethod.Put, notFound.Request!.Method);

        var bad = await Send(new() { ["url"] = "ftp://x" });
        Assert.False(bad.Result.Success);
        Assert.Null(bad.Request);
    }

    private sealed class FakeHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }
}

public sealed class MacroAndGestureValidationTests
{
    private static IReadOnlyList<ConfigIssue> Validate(params Binding[] bindings)
        => ConfigValidator.Validate(new MakroPadConfig { Profiles = [new ProfileConfig { Name = "Default", Bindings = bindings }] });

    [Fact]
    public void GestureInputsAreValidForKeysOnly()
    {
        Assert.Empty(Validate(
            new Binding("button:1:long", "media.stop"),
            new Binding("button:1:double", "media.mute"),
            new Binding("button:1", "media.playPause")));

        Assert.Single(Validate(new Binding("slider:1:long", "media.stop")));
        Assert.Single(Validate(new Binding("button:1:triple", "media.stop")));
    }

    [Fact]
    public void MacroStepsAreChecked()
    {
        var issues = Validate(new Binding("button:1", "macro", Steps:
        [
            new MacroStep("keyboard.shortcut", new Dictionary<string, string> { ["keys"] = "Ctrl+C" }),
            new MacroStep("delay", new Dictionary<string, string> { ["ms"] = "99999" }),
            new MacroStep("system.volume"),
            new MacroStep("macro"),
            new MacroStep("keyboard.text")
        ]));

        Assert.Collection(
            issues.Select(i => i.Message),
            m => Assert.StartsWith("Schritt 2: ms", m, StringComparison.Ordinal),
            m => Assert.StartsWith("Schritt 3: system.volume geht nicht", m, StringComparison.Ordinal),
            m => Assert.StartsWith("Schritt 4: macro geht nicht", m, StringComparison.Ordinal),
            m => Assert.StartsWith("Schritt 5: Parameter \"text\" fehlt", m, StringComparison.Ordinal));
    }

    [Fact]
    public void MacroNeedsStepsAndOthersMustNotHaveThem()
    {
        Assert.Contains(Validate(new Binding("button:1", "macro")), i => i.Message.Contains("keine Schritte", StringComparison.Ordinal));
        Assert.Contains(Validate(new Binding("button:1", "media.stop", Steps: [new MacroStep("media.stop")])),
            i => i.Message.Contains("nur bei den Aktionen macro und toggle", StringComparison.Ordinal));
        Assert.Contains(Validate(new Binding("button:1", "toggle", Steps: [new MacroStep("media.stop")])),
            i => i.Message.Contains("mindestens zwei Schritte", StringComparison.Ordinal));
        Assert.Contains(Validate(new Binding("button:1", "macro", Steps: [new MacroStep("toggle"), new MacroStep("media.stop")])),
            i => i.Message.StartsWith("Schritt 1: toggle geht nicht", StringComparison.Ordinal));
    }

    [Fact]
    public void HttpParametersAreChecked()
    {
        Assert.Empty(Validate(new Binding("button:1", "http.request", new Dictionary<string, string> { ["url"] = "http://a.b", ["method"] = "post" })));
        Assert.Equal(2, Validate(new Binding("button:1", "http.request", new Dictionary<string, string> { ["url"] = "a.b", ["method"] = "FETCH" })).Count);
    }

    [Fact]
    public void RenamingAProfileUpdatesMacroSteps()
    {
        var config = new MakroPadConfig
        {
            Profiles =
            [
                new ProfileConfig
                {
                    Name = "A",
                    Bindings = [new Binding("button:1", "macro", Steps: [new MacroStep("profile.set", new Dictionary<string, string> { ["name"] = "B" })])]
                },
                new ProfileConfig { Name = "B" }
            ]
        };

        var renamed = ConfigEditor.RenameProfile(config, "B", "C");
        Assert.Equal("C", renamed.Profiles[0].Bindings[0].Steps![0].GetParameter("name"));
        Assert.Empty(ConfigValidator.Validate(renamed));
    }

    [Fact]
    public void MacroBindingsRoundTripThroughJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), "makropad-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "config.json"), """
                { "schemaVersion": 1, "profiles": [ { "name": "D", "bindings": [
                  { "input": "button:2:long", "action": "macro", "parameters": { "label": "Stream" },
                    "steps": [ { "action": "keyboard.shortcut", "parameters": { "keys": "Ctrl+F1" } },
                               { "action": "delay", "parameters": { "ms": "500" } } ] } ] } ] }
                """);
            using var service = new ConfigService(directory);
            service.Load();

            Assert.Null(service.LoadError);
            Assert.Empty(service.Issues);
            var binding = service.Config.Profiles[0].Bindings[0];
            Assert.Equal(2, binding.Steps!.Count);
            Assert.Equal("500", binding.Steps[1].GetParameter("ms"));

            service.Save(service.Config);
            Assert.Contains("\"steps\"", File.ReadAllText(service.ConfigPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
