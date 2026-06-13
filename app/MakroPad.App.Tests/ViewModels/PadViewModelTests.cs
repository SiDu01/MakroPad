using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MakroPad.App.Services;
using MakroPad.App.ViewModels;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Tests.ViewModels;

public sealed class PadViewModelTests
{
    private static ProfilesEditorViewModel CreateProfiles(MakroPadConfig config)
    {
        var catalog = new ActionDefinitionCatalog();
        var bindingEditor = new BindingEditorService(catalog);
        var profiles = new ProfilesEditorViewModel(
            new ProfileConfigService(),
            bindingEditor,
            catalog,
            new ObservableCollection<string>());
        profiles.Load(config);
        return profiles;
    }

    private static MakroPadConfig ConfigWithBinding(string input, string action)
    {
        var profile = new ProfileConfig
        {
            Name = "Default",
            Bindings = [new ActionBinding { Input = input, Action = action }]
        };
        return MakroPadConfig.CreateDefault() with { Profiles = [profile], ActiveProfile = "Default" };
    }

    [Fact]
    public void BuildsTwentyKeysAndThreeSliderSlots()
    {
        var pad = new PadViewModel(CreateProfiles(MakroPadConfig.CreateDefault()), _ => true);

        Assert.Equal(20, pad.Keys.Count);
        Assert.Equal(3, pad.Sliders.Count);
        Assert.Equal("button:1", pad.Keys[0].Input);
        Assert.Equal("button:20", pad.Keys[19].Input);
        Assert.Equal("slider:1", pad.Sliders[0].Input);
        Assert.Equal("slider:3", pad.Sliders[2].Input);
    }

    [Fact]
    public void MapsActiveProfileBindingsOntoSlots()
    {
        var profiles = CreateProfiles(ConfigWithBinding("button:7", "media.playPause"));
        var pad = new PadViewModel(profiles, _ => true);

        var bound = pad.Keys.Single(k => k.Input == "button:7");
        Assert.True(bound.HasBinding);
        Assert.Equal("media.playPause", bound.ActionLabel);

        var empty = pad.Keys.Single(k => k.Input == "button:1");
        Assert.False(empty.HasBinding);
        Assert.Equal("Leer", empty.ActionLabel);
    }

    [Fact]
    public void TestModeSimulatesInputWithoutSelecting()
    {
        var simulated = new List<string>();
        var pad = new PadViewModel(CreateProfiles(MakroPadConfig.CreateDefault()), input =>
        {
            simulated.Add(input);
            return true;
        });

        pad.SetTestModeCommand.Execute(null);
        pad.SelectSlotCommand.Execute(pad.Keys[2]);

        Assert.Equal(["button:3"], simulated);
        Assert.Null(pad.SelectedSlot);
        Assert.False(pad.Keys[2].IsSelected);
    }

    [Fact]
    public void EditModeSelectsSlotAndLoadsEditorInput()
    {
        var profiles = CreateProfiles(ConfigWithBinding("button:7", "media.playPause"));
        var pad = new PadViewModel(profiles, _ => true);

        pad.SetEditModeCommand.Execute(null);
        pad.SelectSlotCommand.Execute(pad.Keys[6]);

        Assert.True(pad.HasSelection);
        Assert.Same(pad.Keys[6], pad.SelectedSlot);
        Assert.True(pad.Keys[6].IsSelected);
        Assert.Equal("button:7", profiles.BindingEditorInput);
        Assert.NotNull(profiles.SelectedBinding);
    }

    [Fact]
    public void SaveSlotAddsBindingForEmptySlot()
    {
        var profiles = CreateProfiles(MakroPadConfig.CreateDefault());
        var pad = new PadViewModel(profiles, _ => true);

        pad.SetEditModeCommand.Execute(null);
        pad.SelectSlotCommand.Execute(pad.Keys[0]);
        profiles.ActionEditor.Action = "media.mute";
        pad.SaveSlotCommand.Execute(null);

        var slot = pad.Keys[0];
        Assert.True(slot.HasBinding);
        Assert.Equal("media.mute", slot.ActionLabel);
    }

    [Fact]
    public void ApplyLayoutRebuildsGridForGivenGeometry()
    {
        var pad = new PadViewModel(CreateProfiles(MakroPadConfig.CreateDefault()), _ => true);

        pad.ApplyLayout(rows: 3, columns: 4, sliderCount: 2);

        Assert.Equal(12, pad.Keys.Count);
        Assert.Equal(4, pad.Columns);
        Assert.Equal(2, pad.Sliders.Count);
        Assert.Equal("button:12", pad.Keys[11].Input);
        Assert.Null(pad.SelectedSlot);
    }

    [Fact]
    public void ApplyLayoutClampsToValidRange()
    {
        var pad = new PadViewModel(CreateProfiles(MakroPadConfig.CreateDefault()), _ => true);

        pad.ApplyLayout(rows: 0, columns: 0, sliderCount: -5);

        Assert.Equal(1, pad.Keys.Count);
        Assert.Equal(1, pad.Columns);
        Assert.Empty(pad.Sliders);
    }

    [Fact]
    public void ClearSlotRemovesBinding()
    {
        var profiles = CreateProfiles(ConfigWithBinding("button:5", "media.nextTrack"));
        var pad = new PadViewModel(profiles, _ => true);

        pad.SetEditModeCommand.Execute(null);
        pad.SelectSlotCommand.Execute(pad.Keys[4]);
        pad.ClearSlotCommand.Execute(null);

        var slot = pad.Keys[4];
        Assert.False(slot.HasBinding);
        Assert.Equal("Leer", slot.ActionLabel);
    }
}
