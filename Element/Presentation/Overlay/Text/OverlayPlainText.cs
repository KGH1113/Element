using System.Text;

namespace Element.Presentation.Overlay.Text;

/// <summary>
/// An overlay value's rich text as plain words, for a place that shows the
/// value in its own look, such as a chip in the editor's dock: tags are left
/// out, words kept from markup inside <c>noparse</c> stay, and line breaks
/// read as spaces. Appends without allocating once the target has room.
/// </summary>
internal static class OverlayPlainText
{
  private const string NoParse = "<noparse>",
    NoParseEnd = "</noparse>";

  public static void Append(StringBuilder to, StringBuilder rich)
  {
    int length = rich.Length;
    int i = 0;
    while (i < length)
    {
      char c = rich[i];
      if (c != '<')
      {
        AppendChar(to, c);
        i++;
        continue;
      }
      if (StartsWith(rich, i, NoParse))
      {
        i += NoParse.Length;
        while (i < length && !StartsWith(rich, i, NoParseEnd))
          AppendChar(to, rich[i++]);
        i += NoParseEnd.Length;
        continue;
      }
      int close = i + 1;
      while (close < length && rich[close] != '>')
        close++;
      // A '<' that opens nothing is a word of its own.
      if (close >= length)
      {
        AppendChar(to, c);
        i++;
        continue;
      }
      i = close + 1;
    }
  }

  private static void AppendChar(StringBuilder to, char c) => to.Append(c is '\n' or '\r' or '\t' ? ' ' : c);

  private static bool StartsWith(StringBuilder text, int at, string part)
  {
    if (at + part.Length > text.Length)
      return false;
    for (int i = 0; i < part.Length; i++)
      if (text[at + i] != part[i])
        return false;
    return true;
  }
}
