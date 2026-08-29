using System;
using System.Text;

namespace AnvilMap.Generator;

// Tracks indentation depth instead of hand-counting spaces in every literal.
internal sealed partial class CodeWriter
{
    private const int SpacesPerLevel = 4;

    private readonly StringBuilder _sb;
    private int _indent;

    public CodeWriter() => _sb = StringBuilderCache.Acquire();

    public CodeWriter WriteLine()
    {
        _sb.AppendLine();
        return this;
    }

    public CodeWriter WriteLine(string line)
    {
        if (line.Length == 0)
        {
            _sb.AppendLine();
        }
        else
        {
            _sb.Append(' ', _indent * SpacesPerLevel).AppendLine(line);
        }

        return this;
    }

    // No braces - covers an expression-bodied `=> ...;` continuation and a guarded
    // single-statement `if` body.
    public IDisposable Indent() => new IndentScope(this, writeClosingBrace: false, closeSuffix: "");

    public IDisposable Block(string? header = null, string closeSuffix = "")
    {
        if (header is not null)
        {
            WriteLine(header);
        }

        WriteLine("{");
        return new IndentScope(this, writeClosingBrace: true, closeSuffix);
    }

    public CodeWriter Summary(string xmlContent) => WriteLine($"/// <summary>{xmlContent}</summary>");

    // Only escape dynamic text (e.g. a type's display name) - literal markup like <c> must
    // stay unescaped or it turns into inert text instead of a tag.
    public static string Escape(string text) => text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    public string Build() => StringBuilderCache.GetStringAndRelease(_sb);

    public override string ToString() => _sb.ToString();
}
