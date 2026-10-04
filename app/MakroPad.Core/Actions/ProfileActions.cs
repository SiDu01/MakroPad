using MakroPad.Core.Runtime;

namespace MakroPad.Core.Actions;

/// <summary><c>profile.next</c> and <c>profile.set</c>. The profile overlay is shown by the runtime.</summary>
public sealed class ProfileActions(ProfileManager profiles)
{
    public IEnumerable<IActionHandler> Create()
    {
        yield return new Handler("profile.next", _ =>
        {
            var next = profiles.Next();
            return ActionResult.Ok(next.Name) with { Quiet = true };
        });

        yield return new Handler("profile.set", context =>
        {
            var name = context.Binding.GetParameter("name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return ActionResult.Failed("profile.set: Parameter name fehlt");
            }

            return profiles.Set(name)
                ? ActionResult.Ok(name) with { Quiet = true }
                : ActionResult.Failed($"Profil \"{name}\" fehlt");
        });
    }

    private sealed class Handler(string actionId, Func<ActionContext, ActionResult> run) : IActionHandler
    {
        public string ActionId => actionId;

        public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(run(context));
    }
}
