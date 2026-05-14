namespace MakroPad.App.ViewModels;

public sealed class BindingViewModel
{
    public BindingViewModel(string input, string action, string parameters)
    {
        Input = input;
        Action = action;
        Parameters = parameters;
    }

    public string Input { get; }

    public string Action { get; }

    public string Parameters { get; }
}
