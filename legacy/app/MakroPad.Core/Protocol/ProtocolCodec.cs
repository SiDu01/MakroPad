using System.Text;

namespace MakroPad.Core.Protocol;

public static class ProtocolCodec
{
    public static ProtocolMessage Parse(string line)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        var trimmed = line.Trim();
        var segments = trimmed.Split(';', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            throw new FormatException("Protocol line does not contain a message type.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in segments.Skip(1))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = segment[..separator];
            var value = segment[(separator + 1)..];
            values[key] = PercentDecode(value);
        }

        return new ProtocolMessage(segments[0], values);
    }

    public static string Format(string type, IReadOnlyDictionary<string, string?>? values = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var builder = new StringBuilder(type);

        if (values is not null)
        {
            foreach (var (key, value) in values)
            {
                if (string.IsNullOrWhiteSpace(key) || value is null)
                {
                    continue;
                }

                builder.Append(';');
                builder.Append(key);
                builder.Append('=');
                builder.Append(PercentEncode(value));
            }
        }

        return builder.ToString();
    }

    public static string PercentEncode(string value)
    {
        return value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace(";", "%3B", StringComparison.Ordinal)
            .Replace("=", "%3D", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);
    }

    public static string PercentDecode(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '%' && index + 2 < value.Length)
            {
                var hex = value.Substring(index + 1, 2);
                if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var code))
                {
                    builder.Append((char)code);
                    index += 2;
                    continue;
                }
            }

            builder.Append(value[index]);
        }

        return builder.ToString();
    }
}
