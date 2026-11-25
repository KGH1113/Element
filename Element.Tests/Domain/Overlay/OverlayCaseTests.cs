using System.Collections.Generic;
using System.Linq;
using Element.Application.Settings;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
using Element.Tests.Support;
using static Element.Tests.Domain.Overlay.OverlayFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

// Cases in a formula: a comparison and what it gives, typed on the keypad
// like the rest, saved, worked out first case first, colors as values, and
// the move from colors that followed a value.
internal static class OverlayCaseTests
{
  [Suite("a formula can give something else while a comparison holds, colors included")]
  public static void Run()
  {
    CasesParseAndSave();
    TheFirstCaseThatHoldsGives();
    TheKeypadWritesCases();
    ColorsAreValues();
    ValueColorsBecomeCases();
  }

  private static void CasesParseAndSave()
  {
    OverlayFormula formula = OverlayFormula.Parse("{accuracy}>=100?60;{combo}>{miss}*2?40");
    Check(formula.HasCases && formula.Serialize() == "{accuracy}>=100?60;{combo}>{miss}*2?40", "cases round-trip");
    Check(formula[1].Kind == OverlayFormulaTokenKind.Compare && formula[1].Operator == '≥', "≥ is one comparison");
    Check(
      OverlayFormula.Parse("{miss} != 0 ? 1").Serialize() == "{miss}!=0?1"
        && OverlayFormula.Parse("{miss}<>0?1").Serialize() == "{miss}!=0?1"
        && OverlayFormula.Parse("{miss}≠0?1").Serialize() == "{miss}!=0?1",
      "≠ reads every way it is written"
    );
    Check(
      formula.Mask.Contains(OverlayValueId.Accuracy) && formula.Mask.Contains(OverlayValueId.Miss),
      "the values a comparison reads are in the mask"
    );
    OverlayFormula color = OverlayFormula.Parse("{accuracy}=100?{highlight-color};{accuracy}>=98?#5ac8fa");
    Check(
      color.Serialize() == "{accuracy}=100?{highlight-color};{accuracy}>=98?#5AC8FA" && color[4].IsColor,
      "colors are results: the highlight color and one picked by hand"
    );
    Check(OverlayFormula.Parse("#FF0000+1").Serialize() == "1", "a color can't be calculated with");
    Check(
      OverlayFormula.Parse("{song-title}>1?2").Serialize() == "song-title",
      "words can't be compared: the comparison drops out"
    );
    Check(
      OverlayFormula.Parse("{accuracy}=100?60").Describe(value => value.EnglishName, "then", "otherwise")
        == "X-Accuracy = 100 then 60",
      "cases read as words"
    );
  }

  private static void TheFirstCaseThatHoldsGives()
  {
    Numbers numbers = new() { [OverlayValueId.Accuracy] = 99.5, [OverlayValueId.Miss] = 0 };
    OverlayFormula tiers = OverlayFormula.Parse("{accuracy}=100?3;{accuracy}>=98?2;{accuracy}>=95?1");
    Check(tiers.TryEvaluate(numbers, out double result, out _) && result == 2, "99.5 is in the second case");
    numbers[OverlayValueId.Accuracy] = 100;
    Check(tiers.TryEvaluate(numbers, out result, out _) && result == 3, "100 is in the first, read first");
    numbers[OverlayValueId.Accuracy] = 50;
    Check(!tiers.TryEvaluate(numbers, out _, out _), "with no case holding there is no result: the plain value shows");
    Check(
      !OverlayFormula.Parse("{combo}>1?5").TryEvaluate(numbers, out _, out _),
      "a value without data holds no comparison"
    );
    Check(
      OverlayFormula.Parse("{miss}=0?{accuracy}*2").TryEvaluate(numbers, out result, out _) && result == 100,
      "a result can be a calculation"
    );
    Check(
      OverlayFormulaEvaluator.Holds('=', .1 + .2, .3)
        && OverlayFormulaEvaluator.Holds('<', 99.99, 100)
        && !OverlayFormulaEvaluator.Holds('≠', 4, 4)
        && OverlayFormulaEvaluator.Holds('≤', 4, 4)
        && !OverlayFormulaEvaluator.Holds('>', 4, 4),
      "the six comparisons, with numbers the same to six decimals the same"
    );
    Check(
      OverlayFormula.Parse("{combo}*2").TryEvaluate(new Numbers { [OverlayValueId.Combo] = 3 }, out result, out _)
        && result == 6,
      "a formula without cases calculates as before"
    );
  }

  private static OverlayFormula Typed(OverlayFormulaKind kind, params object[] steps)
  {
    OverlayFormula formula = new();
    foreach (object step in steps)
      if (step is char key)
        Check(formula.Type(key, kind), "can type " + key + " after " + formula.Serialize());
      else
      {
        OverlayValueId id = (OverlayValueId)step;
        Check(formula.CanDrop(id, kind), "can drop " + id + " after " + formula.Serialize());
        formula.Drop(id);
      }
    return formula;
  }

  private static void TheKeypadWritesCases()
  {
    OverlayFormula formula = Typed(
      OverlayFormulaKind.Number,
      OverlayValueId.ProgressCurrent,
      '≥',
      '1',
      '0',
      '0',
      OverlayFormulaKeypad.Next,
      '6',
      '0'
    );
    Check(formula.Serialize() == "{progress-current}>=100?60", "a case is typed left to right");
    Check(
      OverlayFormulaKeypad.NextStep(formula) == OverlayFormulaTokenKind.Else,
      "after a result the key adds otherwise"
    );
    Check(
      formula.Type(OverlayFormulaKeypad.Next) && formula.Last?.Kind == OverlayFormulaTokenKind.Else,
      "otherwise starts the next case"
    );
    Check(formula.Trimmed().Serialize() == "{progress-current}>=100?60", "a case not yet whole is left out");
    Check(!formula.CanType('≥'), "a comparison needs something to compare first");
    formula.Drop(OverlayValueId.Combo);
    Check(
      formula.Type('<') && formula.Type('>') && formula.Last?.Operator == '>',
      "a comparison just typed can be swapped"
    );
    Check(formula.Trimmed().Serialize() == "{progress-current}>=100?60", "a case without then is left out");
    Check(!formula.CanType(OverlayFormulaKeypad.Next), "then needs what is compared with");
    formula.Type('5');
    Check(OverlayFormulaKeypad.NextStep(formula) == OverlayFormulaTokenKind.Then, "now then can come");
    Check(
      Typed(OverlayFormulaKind.Number, OverlayValueId.Combo, '≥').Trimmed().IsEmpty,
      "a comparison alone is nothing yet"
    );
    OverlayFormula keyboard = Typed(OverlayFormulaKind.Number, OverlayValueId.Combo);
    keyboard.Type(OverlayFormulaKeypad.FromKeyboard(keyboard, '>'));
    keyboard.Type(OverlayFormulaKeypad.FromKeyboard(keyboard, '='));
    Check(
      keyboard.Last?.Operator == '≥' && OverlayFormulaKeypad.FromKeyboard(keyboard, '!') == '≠',
      "> then = makes ≥ on a keyboard"
    );
    OverlayFormula words = Typed(
      OverlayFormulaKind.Text,
      OverlayValueId.Miss,
      '=',
      '0',
      OverlayFormulaKeypad.Next,
      OverlayValueId.SongTitle
    );
    Check(words.Serialize() == "{miss}=0?{song-title}" && !words.CanType('+'), "words can be a text's result, alone");
    Check(
      !new OverlayFormula().CanDrop(OverlayValueId.SongTitle, OverlayFormulaKind.Number),
      "a number field takes no words"
    );
  }

  private static void ColorsAreValues()
  {
    Check(
      OverlayValueCatalog.Get(OverlayValueId.HighlightColor).IsColor
        && !OverlayValueCatalog.Get(OverlayValueId.HighlightColor).IsNumeric
        && OverlayValueCatalog.Categories.Contains(OverlayValueCategory.Color),
      "the highlight color is a color value in its own category"
    );
    OverlayFormula color = Typed(
      OverlayFormulaKind.Color,
      OverlayValueId.Accuracy,
      '=',
      '1',
      '0',
      '0',
      OverlayFormulaKeypad.Next
    );
    Check(!color.CanType('5', OverlayFormulaKind.Color), "a color's result is no number");
    Check(!color.CanDrop(OverlayValueId.Combo, OverlayFormulaKind.Color), "nor a value to calculate");
    Check(color.CanDrop(OverlayValueId.HighlightColor, OverlayFormulaKind.Color), "it is a color");
    color.Drop(OverlayValueId.CustomColor);
    Check(
      color.Last is { Kind: OverlayFormulaTokenKind.Color, Hex: "#FFFFFF" },
      "the custom color goes in as a color picked by hand"
    );
    color.Drop(OverlayValueId.HighlightColor);
    Check(color.Serialize() == "{accuracy}=100?{highlight-color}", "another color replaces it");
    Check(
      !new OverlayFormula().CanDrop(OverlayValueId.HighlightColor, OverlayFormulaKind.Number),
      "a number takes no color"
    );
    Check(
      color.TryPickColor(new Numbers { [OverlayValueId.Accuracy] = 100 }, out OverlayFormulaToken picked)
        && picked.Value == OverlayValueId.HighlightColor,
      "the case gives its color"
    );
    Check(
      !color.TryPickColor(new Numbers { [OverlayValueId.Accuracy] = 99 }, out _),
      "otherwise the color's own shows"
    );
  }

  private static void ValueColorsBecomeCases()
  {
    OverlayValueColor tiers = new() { Value = "accuracy", Mode = OverlayValueColorMode.Step };
    tiers.Stops.Add(OverlayValueColorStop.Custom(95, "#FFFFFF"));
    tiers.Stops.Add(OverlayValueColorStop.HighlightColor(100));
    tiers.Stops.Add(OverlayValueColorStop.Custom(99.5f, "#5AC8FA", .5f));
    OverlayColorSettings following = OverlayColorSettings.Custom("#00FF00");
    following.ValueColor = tiers;
    OverlayColorSettings noValue = OverlayColorSettings.Custom("#123456");
    noValue.ValueColor = new OverlayValueColor();
    ElementSettings settings = new()
    {
      SettingsVersion = 9,
      OverlayLayouts = new()
      {
        new OverlayLayoutSettings
        {
          Id = "text:acc",
          Text = new OverlayTextSettings
          {
            Runs = { OverlayTextRun.ValueRun("accuracy", following), OverlayTextRun.Literal("!", noValue) },
          },
        },
      },
    };
    SettingsMigrator.Migrate(settings);
    List<OverlayTextRun> runs = settings.OverlayLayouts[0].Text.Runs;
    OverlayColorSettings converted = runs[0].Color;
    Check(
      converted.ValueColor == null && !converted.Highlight && converted.Hex == "#FFFFFF",
      "the lowest stop is the color itself"
    );
    Check(
      converted.Formula == "{accuracy}>=100?{highlight-color};{accuracy}>=99.5?#5AC8FA",
      "a case per stop above it, the highest first: " + converted.Formula
    );
    Check(
      runs[1].Color.ValueColor == null && runs[1].Color.Hex == "#123456" && runs[1].Color.Formula == "",
      "a color that followed nothing yet is its own"
    );
    Check(settings.SettingsVersion == ElementSettings.CurrentSettingsVersion, "the settings are current");
    OverlayFormula formula = OverlayFormula.Parse(converted.Formula);
    Check(
      formula.TryPickColor(new Numbers { [OverlayValueId.Accuracy] = 99.7 }, out OverlayFormulaToken picked)
        && picked.Hex == "#5AC8FA",
      "it colors as the stops did: 99.7 is past 99.5"
    );
  }
}
