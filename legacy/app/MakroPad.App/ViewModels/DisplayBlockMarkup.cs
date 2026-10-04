using System;
using System.Collections.Generic;
using System.Linq;
using MakroPad.Core.DisplayTriggers;

namespace MakroPad.App.ViewModels;

public readonly record struct DisplayBlock(DisplayBlockKind Kind, string Text, string Alignment = "left");

/// <summary>
/// Shared parse/serialize between the Markdown-like template text and the visual block list,
/// used by both the rule editor and the idle-screen editor. Blank lines map to a blank block and
/// a leading "[c]"/"[r]" alignment tag maps to the block alignment.
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
            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                blocks.Add(new DisplayBlock(DisplayBlockKind.Blank, ""));
                continue;
            }

            var (alignment, line) = DisplayTriggerRenderer.ParseAlignmentPrefix(trimmed);

            if (line.Length >= 3 && line.All(c => c == '-'))
            {
                blocks.Add(new DisplayBlock(DisplayBlockKind.Divider, "", alignment));
            }
            else if (line.StartsWith('#'))
            {
                blocks.Add(new DisplayBlock(DisplayBlockKind.Heading, line.TrimStart('#').Trim(), alignment));
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                blocks.Add(new DisplayBlock(DisplayBlockKind.ListItem, line[2..].Trim(), alignment));
            }
            else
            {
                blocks.Add(new DisplayBlock(DisplayBlockKind.Text, line, alignment));
            }
        }

        return blocks;
    }

    public static string Serialize(IEnumerable<DisplayBlock> blocks) =>
        string.Join("\n", blocks.Select(block => Serialize(block.Kind, block.Text, block.Alignment)));

    public static string Serialize(DisplayBlockKind kind, string text, string alignment = "left")
    {
        if (kind == DisplayBlockKind.Blank)
        {
            return "";
        }

        var prefix = alignment?.ToLowerInvariant() switch
        {
            "center" => "[c]",
            "right" => "[r]",
            _ => ""
        };

        var body = kind switch
        {
            DisplayBlockKind.Heading => "# " + text.Trim(),
            DisplayBlockKind.ListItem => "- " + text.Trim(),
            DisplayBlockKind.Divider => "---",
            _ => text.Trim()
        };

        return prefix + body;
    }
}
