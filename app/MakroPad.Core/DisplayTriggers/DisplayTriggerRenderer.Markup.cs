using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MakroPad.Core.DisplayTriggers;

public static partial class DisplayTriggerRenderer
{
    private static readonly string DividerLine = new('-', MaxPreviewCharacters);

    /// <summary>
    /// Renders a documented HTML subset into OLED text lines. Supported tags:
    /// h1/h2 (text size 2), h3-h6/p/div/section/header/tr (block break), br (line break),
    /// hr (divider line), ul/ol/li (bulleted or numbered list items). Inline tags such as
    /// b, strong, i, em, span and a are ignored but their text is kept; entities are decoded.
    /// Unknown tags are ignored. Output is limited to 5 lines.
    /// </summary>
    public static IReadOnlyList<DisplayTemplateLine> RenderHtmlLines(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var lines = new List<DisplayTemplateLine>();
        var current = new StringBuilder();
        var currentSize = 1;
        var listStack = new Stack<ListContext>();

        void Flush()
        {
            var text = CollapseWhitespace(WebUtility.HtmlDecode(current.ToString())).Trim();
            current.Clear();
            if (text.Length > 0)
            {
                lines.Add(new DisplayTemplateLine(text, currentSize));
            }

            currentSize = 1;
        }

        void HandleTag(string rawTag)
        {
            if (rawTag.StartsWith('!'))
            {
                return;
            }

            var isClosing = rawTag.StartsWith('/');
            var body = (isClosing ? rawTag[1..] : rawTag).TrimStart();
            var nameEnd = 0;
            while (nameEnd < body.Length && char.IsLetterOrDigit(body[nameEnd]))
            {
                nameEnd++;
            }

            switch (body[..nameEnd].ToLowerInvariant())
            {
                case "h1":
                case "h2":
                    Flush();
                    if (!isClosing)
                    {
                        currentSize = 2;
                    }

                    break;
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                case "p":
                case "div":
                case "section":
                case "header":
                case "tr":
                    Flush();
                    break;
                case "br":
                    Flush();
                    break;
                case "hr":
                    Flush();
                    lines.Add(new DisplayTemplateLine(DividerLine));
                    break;
                case "ul":
                    Flush();
                    if (!isClosing)
                    {
                        listStack.Push(new ListContext(false));
                    }
                    else if (listStack.Count > 0)
                    {
                        listStack.Pop();
                    }

                    break;
                case "ol":
                    Flush();
                    if (!isClosing)
                    {
                        listStack.Push(new ListContext(true));
                    }
                    else if (listStack.Count > 0)
                    {
                        listStack.Pop();
                    }

                    break;
                case "li":
                    Flush();
                    if (!isClosing)
                    {
                        var prefix = "- ";
                        if (listStack.Count > 0 && listStack.Peek() is { Ordered: true } ordered)
                        {
                            ordered.Counter++;
                            prefix = $"{ordered.Counter}. ";
                        }

                        current.Append(prefix);
                    }

                    break;
                default:
                    // Inline or unsupported tag: ignore the tag, keep any text content.
                    break;
            }
        }

        var i = 0;
        while (i < html.Length)
        {
            var c = html[i];
            if (c == '<')
            {
                var close = html.IndexOf('>', i);
                if (close < 0)
                {
                    current.Append(html.AsSpan(i));
                    break;
                }

                HandleTag(html.Substring(i + 1, close - i - 1).Trim());
                i = close + 1;
                continue;
            }

            current.Append(c);
            i++;
        }

        Flush();
        return lines.Take(5).ToList();
    }

    /// <summary>
    /// Renders a small Markdown-like pseudocode into OLED text lines: lines starting with '#'
    /// become a size-2 heading, lines starting with '- ' or '* ' become bulleted list items,
    /// a line of three or more dashes becomes a divider, blank lines are dropped, and any other
    /// line is shown as-is. Placeholders are resolved before this runs. Limited to 5 lines.
    /// </summary>
    public static IReadOnlyList<DisplayTemplateLine> RenderMarkdownLines(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var lines = new List<DisplayTemplateLine>();
        var rawLines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (var raw in rawLines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Length >= 3 && line.All(c => c == '-'))
            {
                lines.Add(new DisplayTemplateLine(DividerLine));
            }
            else if (line.StartsWith('#'))
            {
                var heading = line.TrimStart('#').Trim();
                if (heading.Length > 0)
                {
                    lines.Add(new DisplayTemplateLine(heading, 2));
                }
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                lines.Add(new DisplayTemplateLine("- " + line[2..].Trim()));
            }
            else
            {
                lines.Add(new DisplayTemplateLine(line));
            }

            if (lines.Count >= 5)
            {
                break;
            }
        }

        return lines;
    }

    /// <summary>
    /// Renders a content template (without resolving placeholders) to OLED lines for the given
    /// content mode (normal/markdown/html). Used for idle screens and previews so that idle and
    /// rule content render identically.
    /// </summary>
    public static IReadOnlyList<DisplayTemplateLine> RenderContentLines(string template, string mode)
    {
        ArgumentNullException.ThrowIfNull(template);

        return DisplayTriggerNormalizer.NormalizeTemplateMode(mode) switch
        {
            "html" => RenderHtmlLines(template),
            "markdown" => RenderMarkdownLines(template),
            _ => SplitDisplayTextLines(template)
                .Select(line => new DisplayTemplateLine(line))
                .ToList()
        };
    }

    private static string CollapseWhitespace(string value) => WhitespaceRegex().Replace(value, " ");

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed class ListContext(bool ordered)
    {
        public bool Ordered { get; } = ordered;

        public int Counter { get; set; }
    }
}
