namespace MakroPad.App.Services;

/// <summary>Checks whether a named process is currently running. Abstracted for testing.</summary>
public interface IProcessPresenceProbe
{
    bool IsRunning(string processName);
}
