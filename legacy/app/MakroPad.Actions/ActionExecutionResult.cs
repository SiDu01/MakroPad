namespace MakroPad.Actions;

public sealed record ActionExecutionResult(bool Succeeded, string Message)
{
    public static ActionExecutionResult Ok(string message)
    {
        return new ActionExecutionResult(true, message);
    }

    public static ActionExecutionResult Failed(string message)
    {
        return new ActionExecutionResult(false, message);
    }
}
