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
    private static readonly TimeSpan ActivePulse = TimeSpan.FromMilliseconds(280);

    private readonly ProfilesEditorViewModel profiles;
    private readonly Func<string, bool> simulateInput;

    public PadViewModel(ProfilesEditorViewModel profiles, Func<string, bool> simulateInput)
    {
        this.profiles = profiles;
        this.simulateInput = simulateInput;

        profiles.ProfileBindings.CollectionChanged += OnProfileBindingsChanged;
        ApplyLayout(4, 5, 3);
    }

    /// <summary>Number of columns the key grid is arranged in.</summary>
    [ObservableProperty]
    private int columns = 5;

    /// <summary>
    /// Rebuilds the key and slider slots for the given grid geometry. The key count is
    /// rows * columns; inputs are numbered button:1..N and slider:1..M row by row.
    /// </summary>
    public void ApplyLayout(int rows, int columns, int sliderCount)
    {
        rows = Math.Clamp(rows, 1, 12);
        columns = Math.Clamp(columns, 1, 12);
        sliderCount = Math.Clamp(sliderCount, 0, 12);
        var keyCount = rows * columns;

        Columns = columns;

        Keys.Clear();
        for (var i = 1; i <= keyCount; i++)
        {
            Keys.Add(new KeyViewModel(i));
        }

        Sliders.Clear();
        for (var i = 1; i <= sliderCount; i++)
        {
            Sliders.Add(new SliderViewModel(i));
        }

        SelectedSlot = null;
        RefreshFromProfile();
    }

    public ObservableCollection<KeyViewModel> Keys { get; } = [];

    public ObservableCollection<SliderViewModel> Sliders { get; } = [];

    /// <summary>Exposes the underlying profile/binding editor for the inspector bindings.</summary>
    public ProfilesEditorViewModel Profiles => profiles;

    [ObservableProperty]
    private bool isTestMode;

    public bool IsEditMode => !IsTestMode;

    partial void OnIsTestModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditMode));
    }

    [ObservableProperty]
    private PadSlotViewModel? selectedSlot;

    public bool HasSelection => SelectedSlot is not null;

    partial void OnSelectedSlotChanged(PadSlotViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
    }

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

    /// <summary>Persists the editor content to the selected slot's binding.</summary>
    [RelayCommand]
    private void SaveSlot()
    {
        if (SelectedSlot is null)
        {
            return;
        }

        if (profiles.SelectedBinding is null)
        {
            profiles.AddBindingCommand.Execute(null);
        }
        else
        {
            profiles.ApplyBindingCommand.Execute(null);
        }

        LoadSlotIntoEditor(SelectedSlot);
    }

    /// <summary>Removes the binding from the selected slot and resets the editor.</summary>
    [RelayCommand]
    private void ClearSlot()
    {
        if (SelectedSlot is null)
        {
            return;
        }

        if (profiles.SelectedBinding is not null)
        {
            profiles.RemoveBindingCommand.Execute(null);
        }

        LoadSlotIntoEditor(SelectedSlot);
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
