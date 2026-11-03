using System.Text;

namespace MakroPad.Core.Device;

/// <summary>Joins text arriving in arbitrary chunks (e.g. Bluetooth notifications) into trimmed, non-empty lines.</summary>
public sealed class LineAssembler
{
    private readonly StringBuilder pending = new();

    public IReadOnlyList<string> Feed(string chunk)
    {
        pending.Append(chunk);
        var text = pending.ToString();
        var end = text.LastIndexOf('\n');
        if (end < 0)
        {
            return [];
        }

        pending.Clear().Append(text, end + 1, text.Length - end - 1);
        return text[..end]
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }
}
