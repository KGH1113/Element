using System.Globalization;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.IfTool;

/// <summary>
/// A condition in plain words, with no symbols: "100 or bigger", never "≥ 100".
/// </summary>
internal static class IfWords
{
  /// <summary>
  /// When a case is on, given what its value is held against (a number as
  /// shown, or a name) and which of the three results turn it on.
  /// </summary>
  public static string When(string than, bool smaller, bool same, bool bigger)
  {
    if (smaller && same && bigger)
      return L("언제나", "always");
    if (smaller && same)
      return L(than + "이거나 더 작으면", than + " or smaller");
    if (same && bigger)
      return L(than + "이거나 더 크면", than + " or bigger");
    if (smaller && bigger)
      return L(than + Subject(than) + " 아니면", "anything but " + than);
    if (smaller)
      return L(than + "보다 작으면", "smaller than " + than);
    if (same)
      return L(than + "이면", "exactly " + than);
    if (bigger)
      return L(than + "보다 크면", "bigger than " + than);
    return L("켜질 때를 골라 주세요", "pick when it turns on");
  }

  /// <summary>A number as the overlay shows one: up to two decimals; a dash for none.</summary>
  public static string Number(double value) =>
    double.IsNaN(value) || double.IsInfinity(value) ? "—" : value.ToString("0.##", CultureInfo.InvariantCulture);

  /// <summary>The Korean subject particle for <paramref name="word"/>: 이 after a closed syllable, 가 after an open one.</summary>
  public static string Subject(string word)
  {
    if (string.IsNullOrEmpty(word))
      return "이";
    char last = word[word.Length - 1];
    if (last >= '가' && last <= '힣')
      return (last - '가') % 28 == 0 ? "가" : "이";
    // Digits as they are read: 이, 사, 오 and 구 end open; tens, hundreds and the rest end closed.
    if (last >= '0' && last <= '9')
      return last is '2' or '4' or '5' or '9' ? "가" : "이";
    return "이(가)";
  }
}
