namespace MakroPad.App.Services;

/// <summary>Captures the current <see cref="SystemStateSnapshot"/>. Abstracted for testing.</summary>
public interface ISystemStateProvider
{
    SystemStateSnapshot Capture();
}
