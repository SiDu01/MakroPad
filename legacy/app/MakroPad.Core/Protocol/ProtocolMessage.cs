namespace MakroPad.Core.Protocol;

public sealed record ProtocolMessage(string Type, IReadOnlyDictionary<string, string> Values)
{
    public string? GetValue(string key)
    {
        return Values.TryGetValue(key, out var value) ? value : null;
    }
}
