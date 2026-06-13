using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MakroPad.App.ViewModels;

/// <summary>
/// Slot-centric view model for the Pad screen. Mirrors the physical device as a grid of 20
/// button tiles plus 3 slider strips, supports an Edit and a Test mode, and reflects live
/// input activity. Binding data and persistence are delegated to the existing
/// <see cref="ProfilesEditorViewModel"/>.
/// </summary>
public partial class PadViewModel : ObservableObject
{
    private const int KeyCount = 20;
    private const int SliderCount = 3;
    private static readonly TimeSpan ActivePulse = TimeSpan.FromMilliseconds(280);

    private readonly ProfilesEditorViewModel profiles;
    private readonly Func<string, bool> simulateInput;

    public PadViewModel(ProfilesEditorViewModel profiles, Func<string, bool> simulateInput)
    {
        this.profiles = profiles;
        this.simulateInput = simulateInput;

        for (var i = 1; i <= KeyCount; i++)
        {
            Keys.Add(new KeyViewModel(i));
        }

        for (var i = 1; i <= SliderCount; i++)
        {
            Sliders.Add(new SliderViewModel(i));
        }

        profiles.ProfileBindings.CollectionChanged += OnProfileBindingsChanged;
        RefreshFromProfile();
    }

    public ObservableCollection<KeyViewModel> Keys { get; } = [];

    public ObservableCollection<SliderViewModel> Sliders { get; } = [];

    [ObservableProperty]
    private bool isTestMode;

    public bool IsEditMode => !IsTestMode;

    partial void OnIsTestModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditMode));
    }

    [ObservableProperty]
    private PadSlotViewModel? selectedSlot;

    [RelayCommand]
    private void SetEditMode() => IsTestMode = false;

    [RelayCommand]
    private void SetTestMode() => IsTestMode = true;

    [RelayCommand]
    private void SelectSlot(PadSlotViewModel? slot)
    {
        if (slot is null)
        {
            return;
        }

        if (IsTestMode)
        {
            simulateInput(slot.Input);
            return;
        }

        foreach (var other in AllSlots())
        {
            other.IsSelected = ReferenceEquals(other, slot);
        }

        SelectedSlot = slot;
        LoadSlotIntoEditor(slot);
    }

    /// <summary>Pulses the matching slot when an input fires (hardware or simulator).</summary>
    public void OnInputActivated(string input)
    {
        var slot = AllSlots().FirstOrDefault(s => string.Equals(s.Input, input, StringComparison.OrdinalIgnoreCase));
        if (slot is null)
        {
            return;
        }

        slot.IsActive = true;
        DispatcherTimer.RunOnce(() => slot.IsActive = false, ActivePulse);
    }

    private void OnProfileBindingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshFromProfile();
    }

    private void RefreshFromProfile()
    {
        foreach (var slot in AllSlots())
        {
            var binding = profiles.ProfileBindings.FirstOrDefault(
                b => string.Equals(b.Input, slot.Input, StringComparison.OrdinalIgnoreCase));
            slot.HasBinding = binding is not null;
            slot.ActionLabel = binding is null || string.IsNullOrWhiteSpace(binding.Action)
                ? "Leer"
                : binding.Action;
        }
    }

    private void LoadSlotIntoEditor(PadSlotViewModel slot)
    {
        var match = profiles.ProfileBindings.FirstOrDefault(
            b => string.Equals(b.Input, slot.Input, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            profiles.SelectedBinding = match;
            return;
        }

        profiles.SelectedBinding = null;
        profiles.BindingEditorInput = slot.Input;
        profiles.ActionEditor.Load(null, null);
    }

    private System.Collections.Generic.IEnumerable<PadSlotViewModel> AllSlots()
    {
        foreach (var key in Keys)
        {
            yield return key;
        }

        foreach (var slider in Sliders)
        {
            yield return slider;
        }
    }
}
