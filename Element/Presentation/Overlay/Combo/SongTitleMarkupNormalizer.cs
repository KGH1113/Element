using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Element.Presentation.Overlay.Combo
{
  internal static class SongTitleMarkupNormalizer
  {
    private static readonly Regex AbsoluteSizeTag = new(
      @"<size\s*=\s*(?<sign>[+-]?)(?<value>(?:\d+(?:\.\d*)?|\.\d+))\s*(?:px?)?>",
      RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    public static string Normalize(string text, int sourceFontSize)
    {
      if (string.IsNullOrEmpty(text) || sourceFontSize <= 0)
        return text ?? string.Empty;

      // Custom-level size tags were authored as absolute sizes for the game's
      // legacy UI Text. Preserve their ratio to that text's configured size so
      // TMP auto-sizing can apply the same emphasis at the overlay's scale.
      return AbsoluteSizeTag.Replace(
        text,
        match =>
        {
          if (
            !double.TryParse(
              match.Groups["value"].Value,
              NumberStyles.AllowDecimalPoint,
              CultureInfo.InvariantCulture,
              out double value
            )
          )
            return match.Value;

          string sign = match.Groups["sign"].Value;
          double size = sign switch
          {
            "+" => sourceFontSize + value,
            "-" => sourceFontSize - value,
            _ => value,
          };
          double percentage = size / sourceFontSize * 100d;
          return "<size=" + percentage.ToString("0.######", CultureInfo.InvariantCulture) + "%>";
        }
      );
    }
  }
}
