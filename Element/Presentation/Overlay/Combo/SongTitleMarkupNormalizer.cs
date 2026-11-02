using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Element.Presentation.Overlay.Combo
{
  /// <summary>
  /// Turns the game's level name into rich text the overlay draws the way the
  /// game would. Levels write it for the game's legacy UI Text as
  /// "artist - song", and tags often run from one into the other.
  /// <list type="bullet">
  /// <item>Bold, italic, color and size are kept. Absolute sizes become
  /// percentages of the game's size.</item>
  /// <item>Text the level hides is left out: zero or negative sizes, tiny
  /// sizes and see-through colors, often used to hide the artist or the dash.</item>
  /// <item>Tags with values the game can't read, such as a seven-digit color,
  /// are dropped with their closing tags. Stray closing tags are dropped.</item>
  /// <item>Anything else in angle brackets is shown as written, as the game
  /// shows it.</item>
  /// <item>A name hidden entirely shows as plain text so the overlay still
  /// says what is playing.</item>
  /// </list>
  /// </summary>
  internal static class SongTitleMarkupNormalizer
  {
    // Below these, a level means to hide the text: a size of 1 next to 50,
    // or a color with almost no opacity.
    private const double HiddenSize = 10,
      HiddenAlpha = .05;

    private static readonly Dictionary<string, string> Names = new()
    {
      ["aqua"] = "#00FFFF",
      ["black"] = "#000000",
      ["blue"] = "#0000FF",
      ["brown"] = "#A52A2A",
      ["cyan"] = "#00FFFF",
      ["darkblue"] = "#0000A0",
      ["fuchsia"] = "#FF00FF",
      ["green"] = "#008000",
      ["grey"] = "#808080",
      ["lightblue"] = "#ADD8E6",
      ["lime"] = "#00FF00",
      ["magenta"] = "#FF00FF",
      ["maroon"] = "#800000",
      ["navy"] = "#000080",
      ["olive"] = "#808000",
      ["orange"] = "#FFA500",
      ["purple"] = "#800080",
      ["red"] = "#FF0000",
      ["silver"] = "#C0C0C0",
      ["teal"] = "#008080",
      ["white"] = "#FFFFFF",
      ["yellow"] = "#FFFF00",
    };

    private struct Open
    {
      public string Name;
      public bool Written;
      public double Size; // percent, NaN unless a size
      public double Alpha; // 0..1, NaN unless a color
    }

    public static string Normalize(string text, int sourceFontSize)
    {
      if (string.IsNullOrEmpty(text))
        return text ?? string.Empty;
      StringBuilder rich = new(text.Length + 16),
        plain = new(text.Length);
      List<Open> open = new();
      bool anyVisible = false;
      int index = 0;
      while (index < text.Length)
      {
        int start = text.IndexOf('<', index);
        if (start < 0)
          start = text.Length;
        if (start > index)
          anyVisible |= AppendText(text, index, start, open, rich, plain);
        if (start >= text.Length)
          break;
        int end = text.IndexOf('>', start + 1);
        int next = text.IndexOf('<', start + 1);
        if (end < 0 || (next >= 0 && next < end))
        {
          // A lone '<' is just a character.
          anyVisible |= AppendText(text, start, start + 1, open, rich, plain);
          index = start + 1;
          continue;
        }
        string tag = text.Substring(start + 1, end - start - 1);
        if (!ReadTag(tag, sourceFontSize, open, rich))
        {
          // Not markup the game reads: it shows as written.
          string literal = "<noparse><" + tag + "></noparse>";
          if (Visible(open))
          {
            rich.Append(literal);
            anyVisible = true;
          }
          plain.Append(literal);
        }
        index = end + 1;
      }
      // Spaces at either end only push a centered name off center.
      return (anyVisible ? rich.ToString() : plain.ToString()).Trim();
    }

    // Text the level shows goes into the rich text; all of it goes into the
    // plain fallback. Carriage returns are dropped; line breaks stay.
    private static bool AppendText(
      string text,
      int start,
      int end,
      List<Open> open,
      StringBuilder rich,
      StringBuilder plain
    )
    {
      bool visible = Visible(open);
      bool shown = false;
      for (int i = start; i < end; i++)
      {
        char c = text[i];
        if (c == '\r')
          continue;
        plain.Append(c);
        if (!visible)
          continue;
        rich.Append(c);
        shown |= !char.IsWhiteSpace(c);
      }
      return shown;
    }

    private static bool Visible(List<Open> open)
    {
      double size = double.NaN,
        alpha = double.NaN;
      for (int i = open.Count - 1; i >= 0 && (double.IsNaN(size) || double.IsNaN(alpha)); i--)
      {
        if (double.IsNaN(size) && !double.IsNaN(open[i].Size))
          size = open[i].Size;
        if (double.IsNaN(alpha) && !double.IsNaN(open[i].Alpha))
          alpha = open[i].Alpha;
      }
      return (double.IsNaN(size) || size >= HiddenSize) && (double.IsNaN(alpha) || alpha >= HiddenAlpha);
    }

    // False for anything that isn't b, i, size or color; those are shown as text.
    private static bool ReadTag(string tag, int sourceFontSize, List<Open> open, StringBuilder rich)
    {
      string body = tag.Trim();
      bool closing = body.StartsWith("/");
      if (closing)
        body = body.Substring(1).Trim();
      int equals = body.IndexOf('=');
      string name = (equals < 0 ? body : body.Substring(0, equals)).Trim().ToLowerInvariant();
      if (name != "b" && name != "i" && name != "size" && name != "color")
        return false;
      if (closing)
      {
        // Closes the latest of its kind; one with nothing open is dropped.
        for (int i = open.Count - 1; i >= 0; i--)
          if (open[i].Name == name)
          {
            if (open[i].Written)
              rich.Append("</").Append(name).Append('>');
            open.RemoveAt(i);
            break;
          }
        return true;
      }
      string value = equals < 0 ? null : body.Substring(equals + 1).Trim().Trim('"', '\'').Trim();
      Open entry = new()
      {
        Name = name,
        Size = double.NaN,
        Alpha = double.NaN,
      };
      switch (name)
      {
        case "b":
        case "i":
          entry.Written = value == null;
          if (entry.Written)
            rich.Append('<').Append(name).Append('>');
          break;
        case "size":
          if (TrySize(value, sourceFontSize, out double percent, out string written))
          {
            entry.Size = percent;
            // Hidden text is left out, so its size tag isn't needed.
            entry.Written = percent >= HiddenSize;
            if (entry.Written)
              rich.Append("<size=").Append(written).Append('>');
          }
          break;
        case "color":
          if (TryColor(value, out string hex, out double alpha))
          {
            entry.Alpha = alpha;
            entry.Written = alpha >= HiddenAlpha;
            if (entry.Written)
              rich.Append("<color=").Append(hex).Append('>');
          }
          break;
      }
      // A tag the game can't read still pairs with its closing tag.
      open.Add(entry);
      return true;
    }

    // The game's sizes are pixels; a sign is part of the number, and zero or
    // less draws nothing. Percent and em sizes are already relative.
    private static bool TrySize(string value, int sourceFontSize, out double percent, out string written)
    {
      percent = double.NaN;
      written = null;
      if (string.IsNullOrEmpty(value))
        return false;
      string number = value;
      double unit = double.NaN;
      if (number.EndsWith("%"))
      {
        number = number.Substring(0, number.Length - 1);
        unit = 1;
      }
      else if (number.EndsWith("em", System.StringComparison.OrdinalIgnoreCase))
      {
        number = number.Substring(0, number.Length - 2);
        unit = 100;
      }
      else if (number.EndsWith("px", System.StringComparison.OrdinalIgnoreCase))
        number = number.Substring(0, number.Length - 2);
      if (
        !double.TryParse(
          number.Trim(),
          NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
          CultureInfo.InvariantCulture,
          out double size
        )
      )
        return false;
      if (!double.IsNaN(unit))
      {
        percent = size * unit;
        written = value;
        return true;
      }
      if (sourceFontSize <= 0)
        return false;
      percent = size / sourceFontSize * 100d;
      written = percent.ToString("0.######", CultureInfo.InvariantCulture) + "%";
      return true;
    }

    // #RGB, #RGBA, #RRGGBB, #RRGGBBAA or one of the game's color names.
    private static bool TryColor(string value, out string hex, out double alpha)
    {
      hex = null;
      alpha = 1;
      if (string.IsNullOrEmpty(value))
        return false;
      if (Names.TryGetValue(value.ToLowerInvariant(), out string named))
      {
        hex = named;
        return true;
      }
      if (value[0] != '#')
        return false;
      int digits = value.Length - 1;
      if (digits != 3 && digits != 4 && digits != 6 && digits != 8)
        return false;
      for (int i = 1; i < value.Length; i++)
        if (!System.Uri.IsHexDigit(value[i]))
          return false;
      if (digits == 4)
        alpha = int.Parse(value.Substring(4, 1), NumberStyles.HexNumber) / 15d;
      else if (digits == 8)
        alpha = int.Parse(value.Substring(7, 2), NumberStyles.HexNumber) / 255d;
      hex = value.ToUpperInvariant();
      return true;
    }
  }
}
