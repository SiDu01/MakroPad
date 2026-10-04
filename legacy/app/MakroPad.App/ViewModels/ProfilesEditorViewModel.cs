using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MakroPad.App.Services;
using MakroPad.Core.Configuration;

namespace MakroPad.App.ViewModels;

public partial class ProfilesEditorViewModel : ViewModelBase
{
    private readonly ProfileConfigService profileConfigService;
    private readonly BindingEditorService bindingEditorService;
    private bool isRefreshing;
    private MakroPadConfig currentConfig = MakroPadConfig.CreateDefault();

    public ProfilesEditorViewModel(
        ProfileConfigService profileConfigService,
        BindingEditorService bindingEditorService,
        ActionDefinitionCatalog actionDefinitionCatalog,
        ObservableCollection<string> availableAudioProcessNames)
    {
        this.profileConfigService = profileConfigService;
        this.bindingEditorService = bindingEditorService;
        ActionEditor = new ActionEditorViewModel(
            actionDefinitionCatalog,
            bindingEditorService,
            availableAudioProcessNames);
    }

    public event EventHandler<ProfilesEditorChangedEventArgs>? Changed;

    public ActionEditorViewModel ActionEditor { get; }

    public ObservableCollection<string> ProfileNames { get; } = [];

    public ObservableCollection<BindingViewModel> ProfileBindings { get; } = [];

    public IReadOnlyList<string> AvailableInputs { get; } =
    [
        ..Enumerable.Range(1, 20).Select(id => $"button:{id}"),
        ..Enumerable.Range(1, 3).Select(id => $"slider:{id}")
    ];

    [ObservableProperty]
    private string activeProfileName = "Default";

    partial void OnActiveProfileNameChanged(string value)
    {
        if (isRefreshing)
        {
            return;
        }

        var oldName = currentConfig.ActiveProfile;
        currentConfig = currentConfig with { ActiveProfile = value };
        ProfileEditorName = value;
        RefreshProfileBindings();
        RaiseChanged(
            $"Active profile changed: {value}",
            activeProfileChanged: !oldName.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    [ObservableProperty]
    private string profileEditorName = "Default";

    [ObservableProperty]
    private BindingViewModel? selectedBinding;

    partial void OnSelectedBindingChanged(BindingViewModel? value)
    {
        if (isRefreshing)
        {
            return;
        }

        BindingEditorInput = value?.Input ?? "";
        ActionEditor.Load(value?.Action, value?.Parameters);
    }

    [ObservableProperty]
    private string bindingEditorInput = "";

    [ObservableProperty]
    private string bindingEditorValidationMessage = "";

    public void Load(MakroPadConfig config)
    {
        currentConfig = config ?? MakroPadConfig.CreateDefault();
        isRefreshing = true;
        ActiveProfileName = currentConfig.ActiveProfile;
        ProfileEditorName = currentConfig.ActiveProfile;
        isRefreshing = false;
        RefreshProfileNames();
        RefreshProfileBindings();
    }

    public MakroPadConfig ExportConfig()
    {
        return currentConfig;
    }

    [RelayCommand]
    private void AddProfile()
    {
        var result = profileConfigService.AddProfile(currentConfig, ProfileEditorName);
        if (!result.IsSuccess || result.Value is null)
        {
            SetEditorError(result.ErrorMessage);
            return;
        }

        currentConfig = result.Value;
        RefreshProfileNames();
        SetActiveProfileFromConfig();
        RaiseChanged($"Profile added: {currentConfig.ActiveProfile}", activeProfileChanged: true);
    }

    [RelayCommand]
    private void RenameProfile()
    {
        var oldName = currentConfig.ActiveProfile;
        var result = profileConfigService.RenameActiveProfile(currentConfig, ProfileEditorName);
        if (!result.IsSuccess || result.Value is null)
        {
            SetEditorError(result.ErrorMessage);
            return;
        }

        currentConfig = result.Value;
        RefreshProfileNames();
        SetActiveProfileFromConfig();
        var message = oldName.Equals(currentConfig.ActiveProfile, StringComparison.OrdinalIgnoreCase)
            ? ""
            : $"Profile renamed: {oldName} -> {currentConfig.ActiveProfile}";
        RaiseChanged(message, activeProfileChanged: !string.IsNullOrWhiteSpace(message));
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        var deletedName = currentConfig.ActiveProfile;
        var result = profileConfigService.DeleteActiveProfile(currentConfig);
        if (!result.IsSuccess || result.Value is null)
        {
            SetEditorError(result.ErrorMessage);
            return;
        }

        currentConfig = result.Value;
        RefreshProfileNames();
        SetActiveProfileFromConfig();
        RaiseChanged($"Profile deleted: {deletedName}", activeProfileChanged: true);
    }

    [RelayCommand]
    private void ApplyBinding()
    {
        if (SelectedBinding is null)
        {
            SetEditorError("No binding selected.");
            return;
        }

        if (!TryCreateEditorBinding(out var binding, out var warning))
        {
            return;
        }

        var profile = GetActiveProfile();
        if (profile is null || SelectedBinding.Index < 0 || SelectedBinding.Index >= profile.Bindings.Count)
        {
            SetEditorError("Selected binding no longer exists.");
            RefreshProfileBindings();
            return;
        }

        var updatedBindings = profile.Bindings.ToList();
        updatedBindings[SelectedBinding.Index] = binding;
        if (!UpdateActiveProfile(profile with { Bindings = updatedBindings }))
        {
            return;
        }

        RefreshProfileBindings(SelectedBinding.Index);
        RaiseChanged(
            $"Binding updated: {binding.Input} -> {binding.Action}",
            warning);
    }

    [RelayCommand]
    private void AddBinding()
    {
        if (!TryCreateEditorBinding(out var binding, out var warning))
        {
            return;
        }

        var profile = GetActiveProfile();
        if (profile is null)
        {
            SetEditorError("Active profile not found.");
            return;
        }

        var updatedBindings = profile.Bindings.ToList();
        updatedBindings.Add(binding);
        if (!UpdateActiveProfile(profile with { Bindings = updatedBindings }))
        {
            return;
        }

        RefreshProfileBindings(updatedBindings.Count - 1);
        RaiseChanged(
            $"Binding added: {binding.Input} -> {binding.Action}",
            warning);
    }

    [RelayCommand]
    private void RemoveBinding()
    {
        if (SelectedBinding is null)
        {
            SetEditorError("No binding selected.");
            return;
        }

        var profile = GetActiveProfile();
        if (profile is null || SelectedBinding.Index < 0 || SelectedBinding.Index >= profile.Bindings.Count)
        {
            SetEditorError("Selected binding no longer exists.");
            RefreshProfileBindings();
            return;
        }

        var removed = profile.Bindings[SelectedBinding.Index];
        var updatedBindings = profile.Bindings.ToList();
        updatedBindings.RemoveAt(SelectedBinding.Index);
        var nextIndex = updatedBindings.Count == 0
            ? -1
            : Math.Min(SelectedBinding.Index, updatedBindings.Count - 1);
        if (!UpdateActiveProfile(profile with { Bindings = updatedBindings }))
        {
            return;
        }

        RefreshProfileBindings(nextIndex);
        RaiseChanged($"Binding removed: {removed.Input} -> {removed.Action}");
    }

    private void SetActiveProfileFromConfig()
    {
        isRefreshing = true;
        ActiveProfileName = currentConfig.ActiveProfile;
        ProfileEditorName = currentConfig.ActiveProfile;
        isRefreshing = false;
        RefreshProfileBindings();
    }

    private void RefreshProfileNames()
    {
        ProfileNames.Clear();
        foreach (var profile in currentConfig.Profiles)
        {
            ProfileNames.Add(profile.Name);
        }
    }

    private void RefreshProfileBindings(int selectedIndex = 0)
    {
        ProfileBindings.Clear();
        var profile = GetActiveProfile();
        if (profile is null)
        {
            SelectedBinding = null;
            return;
        }

        for (var index = 0; index < profile.Bindings.Count; index++)
        {
            var binding = profile.Bindings[index];
            var parameters = binding.Parameters.Count == 0
                ? ""
                : string.Join(", ", binding.Parameters.Select(pair => $"{pair.Key}={pair.Value}"));

            ProfileBindings.Add(new BindingViewModel(index, binding.Input, binding.Action, parameters));
        }

        isRefreshing = true;
        SelectedBinding = selectedIndex >= 0 && selectedIndex < ProfileBindings.Count
            ? ProfileBindings[selectedIndex]
            : null;
        isRefreshing = false;
        OnSelectedBindingChanged(SelectedBinding);
    }

    private ProfileConfig? GetActiveProfile()
    {
        var result = profileConfigService.FindActiveProfile(currentConfig);
        return result.IsSuccess ? result.Value : null;
    }

    private bool UpdateActiveProfile(ProfileConfig updatedProfile)
    {
        var result = profileConfigService.UpdateActiveProfile(currentConfig, updatedProfile);
        if (!result.IsSuccess || result.Value is null)
        {
            SetEditorError(result.ErrorMessage);
            return false;
        }

        currentConfig = result.Value;
        return true;
    }

    private bool TryCreateEditorBinding(out ActionBinding binding, out string warning)
    {
        binding = new ActionBinding();
        warning = "";
        BindingEditorValidationMessage = "";
        var result = bindingEditorService.CreateBinding(
            BindingEditorInput,
            ActionEditor.Action,
            ActionEditor.Parameters);
        if (!result.IsSuccess || result.Binding is null)
        {
            SetEditorError(result.Error);
            return false;
        }

        binding = result.Binding;
        warning = result.Warning;
        return true;
    }

    private void SetEditorError(string message)
    {
        BindingEditorValidationMessage = message;
        RaiseChanged(message);
    }

    private void RaiseChanged(
        string message,
        string secondaryMessage = "",
        bool activeProfileChanged = false)
    {
        Changed?.Invoke(
            this,
            new ProfilesEditorChangedEventArgs(
                currentConfig,
                message,
                secondaryMessage,
                activeProfileChanged));
    }
}

public sealed class ProfilesEditorChangedEventArgs(
    MakroPadConfig config,
    string message,
    string secondaryMessage,
    bool activeProfileChanged) : EventArgs
{
    public MakroPadConfig Config { get; } = config;

    public string Message { get; } = message;

    public string SecondaryMessage { get; } = secondaryMessage;

    public bool ActiveProfileChanged { get; } = activeProfileChanged;
}
