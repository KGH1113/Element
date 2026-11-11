using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Fonts;
using Element.Domain.Overlay.Text;
using static Element.Tests.Domain.Overlay.OverlayFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

/// <summary>
/// Editing a text: values stay whole, runs merge, words and characters, sizes,
/// fonts and formulas are kept.
/// </summary>
internal static class OverlayTextTests
{
  internal static void Run()
  {
    TextDocumentKeepsValuesAtomic();
    TextDocumentMergesRunsAndColors();
    TextDocumentWordsAndCharacters();
    TextDocumentKeepsSizes();
    TextDocumentKeepsFonts();
    FontUsesTouchOnlyTheirOwnPlaces();
    FontKeysNameOneFace();
    TextDocumentKeepsFormulas();
  }

  private static void TextDocumentKeepsValuesAtomic()
  {
    OverlayTextDocument document = new();
    OverlayColorSettings white = OverlayColorSettings.Custom("#FFFFFF");
    int caret = document.InsertText(0, 0, "FPS | ", white);
    caret = document.InsertValue(caret, caret, "fps", OverlayColorSettings.HighlightColor());
    Check(caret == 7 && document.Count == 7, "a value is one character");
    caret = document.InsertText(caret, caret, "!", document.TypingColor(caret));
    Check(document[7].Color.SameAs(white), "typing after a value continues the text color, not the value's");
    document.Delete(6, 7);
    Check(document.Count == 7 && !document[6].IsValue, "deleting one step removes the whole value");
    document.InsertValue(0, 3, "kps", null);
    Check(document[0].Value == "kps" && document.Count == 5, "a value replaces the selected text");
    Check(document.InsertValue(0, 0, "unknown", null) == 0 && document.Count == 5, "unknown values are ignored");
    Check(
      document.InsertValue(0, 0, "{combo}+1", null) == 1 && document[0].Value == "{combo}+1",
      "formulas are values too"
    );
  }

  private static void TextDocumentMergesRunsAndColors()
  {
    OverlayTextDocument document = OverlayTextDocument.FromRuns(OverlayElementDefaults.All[3].CreateRuns());
    Check(document.Count == 7, "FPS | {fps} has seven characters");
    document.SetColor(0, 3, OverlayColorSettings.HighlightColor(.5f));
    List<OverlayTextRun> runs = document.ToRuns();
    Check(Describe(runs) == "'FPS'' | '[fps|highlight]", "recolored text splits into runs: " + Describe(runs));
    Check(runs[0].Color.Highlight && runs[0].Color.Alpha == .5f, "the selection keeps its new opacity");
    document.SetColor(0, 3, OverlayColorSettings.Custom("#FFFFFF"));
    Check(Describe(document.ToRuns()) == "'FPS | '[fps|highlight]", "same-colored text merges back");
    OverlayTextDocument copy = document.Clone();
    copy.Delete(0, 1);
    Check(document.Count == 7 && !copy.SameAs(document), "clones are independent");
  }

  private static void TextDocumentWordsAndCharacters()
  {
    OverlayTextDocument document = new();
    document.InsertText(0, 0, "a😀b\r\nc\u0001", null);
    Check(
      document.Count == 5 && document[1].Text == "😀" && document[3].Text == "\n",
      "pairs and line ends are one step"
    );
    document = new OverlayTextDocument();
    int caret = document.InsertText(0, 0, "Best combo ", null);
    document.InsertValue(caret, caret, "combo", null);
    Check(document.WordStart(document.Count) == document.Count - 1, "a value is its own word");
    Check(document.WordStart(10) == 5 && document.WordEnd(0) == 4, "words skip spaces");
    Check(
      document.PlainText(0, document.Count, key => "{" + key + "}") == "Best combo {combo}",
      "copied text names its values"
    );
  }

  private static void TextDocumentKeepsSizes()
  {
    OverlayTextDocument document = new();
    int caret = document.InsertText(0, 0, "Big", null, 40);
    document.InsertText(caret, caret, " small", null);
    Check(document.TypingSize(3) == 40 && document.TypingSize(9) == 0, "typing keeps the nearest text's size");
    List<OverlayTextRun> runs = document.ToRuns();
    Check(runs.Count == 2 && runs[0].Size == 40 && runs[1].Size == 0, "sizes split runs: " + runs.Count);
    document.SetSize(0, document.Count, 30);
    runs = document.ToRuns();
    Check(runs.Count == 1 && runs[0].Size == 30 && runs[0].Text == "Big small", "one size merges the runs again");
    document.SetSize(0, 3, float.NaN);
    Check(document[0].Size == 0, "invalid sizes fall back to the base size");
    Check(OverlayTextDocument.FromRuns(runs).SameAs(OverlayTextDocument.FromRuns(runs)), "sizes round-trip");
  }

  private static void TextDocumentKeepsFonts()
  {
    OverlayTextDocument document = new();
    int caret = document.InsertText(0, 0, "Big", null, 0, null, "Pretendard|Bold");
    caret = document.InsertValue(caret, caret, "combo", null, 0, null, document.TypingFont(caret));
    document.InsertText(caret, caret, " plain", null);
    Check(document.TypingFont(3) == "Pretendard|Bold", "typing keeps the nearest text's font");
    Check(document[3].Font == "Pretendard|Bold" && document[4].Font == "", "values carry their font");
    List<OverlayTextRun> runs = document.ToRuns();
    Check(
      runs.Count == 3 && runs[0].Font == "Pretendard|Bold" && runs[1].Font == "Pretendard|Bold" && runs[2].Font == "",
      "fonts split runs: " + runs.Count
    );
    Check(
      document.RangeFont(0, 3) == "Pretendard|Bold" && document.RangeFont(0, 5) == null,
      "a mixed range has no one font"
    );
    document.SetFont(0, document.Count, "Noto Sans KR");
    runs = document.ToRuns();
    Check(runs.Count == 3 && runs[0].Font == "Noto Sans KR|Regular", "a family alone is its regular face");
    Check(OverlayTextDocument.FromRuns(runs).SameAs(document), "fonts round-trip");
    document.SetFont(0, document.Count, "");
    Check(document.ToRuns()[0].Font == "", "an empty font follows the default again");

    OverlayTextRun saved = OverlayTextRun.Literal("가", OverlayColorSettings.Custom("#FFFFFF"));
    saved.Font = "Pretendard|Bold";
    XmlSerializer serializer = new(typeof(OverlayTextRun));
    using StringWriter writer = new();
    serializer.Serialize(writer, saved);
    using StringReader reader = new(writer.ToString());
    Check(((OverlayTextRun)serializer.Deserialize(reader)).SameAs(saved), "a run's font survives the settings file");
  }

  // A font picked for a missing one goes exactly where the missing one was.
  private static void FontUsesTouchOnlyTheirOwnPlaces()
  {
    OverlayTextRun missingRun = OverlayTextRun.Literal("A", OverlayColorSettings.Custom("#FFFFFF"));
    missingRun.Font = "Gone Font|Bold";
    OverlayTextRun keptRun = OverlayTextRun.Literal("B", OverlayColorSettings.Custom("#FFFFFF"));
    keptRun.Font = "Georgia|Regular";
    OverlayTextRun plainRun = OverlayTextRun.Literal("C", OverlayColorSettings.Custom("#FFFFFF"));
    ElementSettings settings = new()
    {
      OverlayFont = "gone font|bold",
      OverlayLayouts = new()
      {
        new OverlayLayoutSettings
        {
          Text = new OverlayTextSettings
          {
            Runs = new() { missingRun, plainRun },
          },
        },
        new OverlayLayoutSettings { Text = new OverlayTextSettings { Runs = new() { keptRun } } },
        new OverlayLayoutSettings(),
      },
    };
    List<OverlayFontUse> uses = OverlayFontUse.Find(settings);
    Check(
      uses.Count == 2 && uses[0].IsDefault && uses[0].Runs.Count == 1 && !uses[1].IsDefault && uses[1].Runs.Count == 1,
      "each font is found once, the default font first, with the texts that use it"
    );
    uses[0].Set(settings, "Georgia");
    Check(
      settings.OverlayFont == "Georgia|Regular" && missingRun.Font == "Georgia|Regular" && plainRun.Font == "",
      "a replacement goes where the missing font was"
    );
    uses[0].Set(settings, "Verdana|Regular");
    Check(
      missingRun.Font == "Verdana|Regular" && keptRun.Font == "Georgia|Regular",
      "trying another font leaves texts that already used the first pick alone"
    );
    uses[0].Set(settings, "");
    Check(settings.OverlayFont == "" && missingRun.Font == "", "the default row follows the default font again");
  }

  private static void FontKeysNameOneFace()
  {
    Check(OverlayFontKey.Create("Pretendard", "Bold") == "Pretendard|Bold", "family and style make a key");
    Check(OverlayFontKey.Create("Pretendard", "") == "Pretendard|Regular", "no style is the regular face");
    Check(OverlayFontKey.Create(" ", "Bold") == "", "a font needs a family");
    Check(OverlayFontKey.Create("A|B", "Bold") == "A B|Bold", "separators in names can't split the key");
    Check(
      OverlayFontKey.TryParse("A B|Bold", out string family, out string style) && family == "A B" && style == "Bold",
      "keys read back"
    );
    Check(OverlayFontKey.Normalize(null) == "" && OverlayFontKey.Normalize("|Bold") == "", "nothing names no font");
    Check(OverlayFontKey.Same("pretendard|bold", "Pretendard|Bold"), "keys compare without case");
    Check(OverlayFontKey.Normalize(" Pretendard ") == "Pretendard|Regular", "a typed family is saved in key form");
    Check(
      OverlayFontFace.StyleRank("Thin") < OverlayFontFace.StyleRank("Regular")
        && OverlayFontFace.StyleRank("Regular") < OverlayFontFace.StyleRank("Italic")
        && OverlayFontFace.StyleRank("SemiBold") < OverlayFontFace.StyleRank("Bold")
        && OverlayFontFace.StyleRank("Bold") < OverlayFontFace.StyleRank("ExtraBold")
        && OverlayFontFace.StyleRank("Extra Light") < OverlayFontFace.StyleRank("Light"),
      "styles sort from thin to black"
    );
  }

  private static void TextDocumentKeepsFormulas()
  {
    OverlayTextDocument document = new();
    int caret = document.InsertValue(0, 0, "{combo}*2", OverlayColorSettings.HighlightColor(), 0, "{combo}/10");
    Check(caret == 1 && document[0].Value == "{combo}*2", "a calculated value is one character");
    document.SetValue(0, "{combo}+{miss}");
    Check(document[0].Value == "{combo}+{miss}", "the keypad changes a value's formula");
    document.InsertText(1, 1, " hits", null, 0, "{combo}/10");
    List<OverlayTextRun> runs = document.ToRuns();
    Check(
      runs.Count == 2 && runs[0].SizeFormula == "{combo}/10" && runs[1].SizeFormula == "{combo}/10",
      "size formulas save"
    );
    document.SetAlpha(0, document.Count, .5f, "{progress-current}");
    Check(
      document[2].Color.AlphaFormula == "progress-current" && document[2].Color.Alpha == .5f,
      "opacity formulas save"
    );
    OverlayTextDocument loaded = OverlayTextDocument.FromRuns(document.ToRuns());
    Check(loaded.SameAs(document), "formulas round-trip through runs");
    document.SetValue(0, "");
    Check(document.Count == 5 && !document[0].IsValue, "an emptied value is removed");
  }
}
