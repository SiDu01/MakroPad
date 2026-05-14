using MakroPad.Core.Configuration;

namespace MakroPad.Actions;

public sealed class ActionResolver
{
    public ActionBinding? Resolve(MakroPadConfig config, string input)
    {
        var profile = config.Profiles.FirstOrDefault(
            candidate => candidate.Name.Equals(config.ActiveProfile, StringComparison.OrdinalIgnoreCase));

        return profile?.Bindings.FirstOrDefault(
            binding => binding.Input.Equals(input, StringComparison.OrdinalIgnoreCase));
    }
}
