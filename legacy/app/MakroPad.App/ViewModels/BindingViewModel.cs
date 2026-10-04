namespace MakroPad.App.ViewModels;

public sealed class BindingViewModel
{
    public BindingViewModel(int index, string input, string action, string parameters)
    {
        Index = index;
        Input = input;
        Action = action;
        Parameters = parameters;
    }

    public int Index { get; }

    public string Input { get; }

    public string Action { get; }

    public string Parameters { get; }
}
