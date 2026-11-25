using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using static Element.Tests.Domain.Overlay.OverlayFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

/// <summary>Formulas parse, save, calculate and are edited on the keypad.</summary>
internal static class OverlayFormulaTests
{
  internal static void Run()
  {
    FormulasParseAndSave();
    FormulasCalculate();
  }

  private static void FormulasParseAndSave()
  {
    OverlayVariable single = OverlayVariable.Parse("combo");
    Check(single.IsSingleValue(out OverlayValueId id) && id == OverlayValueId.Combo, "a key is one value");
    Check(single.Serialize() == "combo", "one value saves as its key, like 1.1");
    OverlayVariable formula = OverlayVariable.Parse("{combo}*2+{miss}/-4");
    Check(
      formula.Count == 7 && formula.Serialize() == "{combo}*2+{miss}/-4",
      "values, numbers and operators round-trip"
    );
    Check(
      formula.Mask == (OverlayValueCatalog.Bit(OverlayValueId.Combo) | OverlayValueCatalog.Bit(OverlayValueId.Miss)),
      "the mask names its values"
    );
    Check(OverlayVariable.Parse("{gone}*2+{combo}").Serialize() == "2+{combo}", "unknown values drop out");
    Check(OverlayVariable.Parse("{song-title}+1").Serialize() == "1", "words can't take part in arithmetic");
    Check(OverlayVariable.Parse("song-title").IsWords, "words stand alone");
    Check(OverlayVariable.Parse("2 × 3 ÷ 4 − 1").Serialize() == "2*3/4-1", "display symbols read as operators");
    Check(OverlayVariable.Parse("").IsEmpty && OverlayVariable.Parse(null).IsEmpty, "nothing is an empty formula");
    Check(
      OverlayVariable.Parse("12.5").IsPlainNumber(out double number) && number == 12.5,
      "a plain number stays a number"
    );
    Check(
      OverlayVariable
        .Parse("{time-total}-{time-current}")
        .Describe(value => value.EnglishName, OverlayVariableWords.Plain) == "Total time − Elapsed time",
      "formulas read as words"
    );
  }

  private static void FormulasCalculate()
  {
    Numbers numbers = new()
    {
      [OverlayValueId.Combo] = 120,
      [OverlayValueId.CurrentTime] = 87,
      [OverlayValueId.TotalTime] = 228,
      [OverlayValueId.Miss] = 0,
    };
    Check(Evaluate("2+3*4", numbers, out bool time) == 14 && !time, "× before +");
    Check(Evaluate("10-4/2", numbers, out _) == 8, "÷ before −");
    Check(Evaluate("{combo}*2", numbers, out _) == 240, "values calculate");
    Check(Evaluate("-5+1", numbers, out _) == -4, "a leading minus is a negative number");
    Check(Evaluate("{combo}*", numbers, out _) == 120, "a trailing operator is still being typed");
    Check(Evaluate("{time-total}-{time-current}", numbers, out time) == 141 && time, "time minus time is a time");
    Check(Evaluate("{time-current}*2", numbers, out time) == 174 && time, "a time times a number is a time");
    Check(Evaluate("{time-current}/{time-total}*100", numbers, out time) > 38 && !time, "time over time is a number");
    Check(!OverlayVariable.Parse("{combo}/{miss}").TryNumber(numbers, out _, out _), "dividing by zero shows nothing");
    numbers.Remove(OverlayValueId.Combo);
    Check(!OverlayVariable.Parse("{combo}+1").TryNumber(numbers, out _, out _), "a value without data shows nothing");
    Check(!OverlayVariable.Parse("song-title").TryNumber(numbers, out _, out _), "words have no number");
  }
}
