using System;
using System.Collections.Generic;
using System.Linq;

namespace MakroPad.App.ViewModels;

public readonly record struct DisplayBlock(DisplayBlockKind Kind, string Text);

/// <summary>
/// Shared parse/serialize between the Markdown-like template text and the visual block list,
/// used by both the rule editor and the idle-screen editor.
/// </summary>
public static class DisplayBlockMarkup
{
    public static IReadOnlyList<DisplayBlock> Parse(string? template)
    {
        var blocks = new List<DisplayBlock>();
        var lines = (template ?? "")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Length >= 3 && line.All(c => c == '-'))
            {
                blocks.Add(new DisplayBlock(DisplayBlockKind.Divider, ""));
            }
            else if (line.StartsWith('#'))
            {
                blocks.Add(new DisplayBlock(DisplayBlockKind.Heading, line.TrimStart('#').Trim()));
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                blocks.Add(new DisplayBlock(DisplayBlockKind.ListItem, line[2..].Trim()));
            }
            else
            {
                blocks.Add(new DisplayBlock(DisplayBlockKind.Text, line));
            }
        }

        return blocks;
    }

    public static string Serialize(IEnumerable<DisplayBlock> blocks) =>
        string.Join("\n", blocks.Select(block => Serialize(block.Kind, block.Text)));

    public static string Serialize(DisplayBlockKind kind, string text) => kind switch
    {
        DisplayBlockKind.Heading => "# " + text.Trim(),
        DisplayBlockKind.ListItem => "- " + text.Trim(),
        DisplayBlockKind.Divider => "---",
        _ => text.Trim()
    };
}
