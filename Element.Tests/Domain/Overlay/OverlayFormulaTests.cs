using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
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
    KeypadEditsFormulas();
  }

  private static void FormulasParseAndSave()
  {
    OverlayFormula single = OverlayFormula.Parse("combo");
    Check(single.IsSingleValue(out OverlayValueId id) && id == OverlayValueId.Combo, "a key is one value");
    Check(single.Serialize() == "combo", "one value saves as its key, like 1.1");
    OverlayFormula formula = OverlayFormula.Parse("{combo}*2+{miss}/-4");
    Check(
      formula.Count == 7 && formula.Serialize() == "{combo}*2+{miss}/-4",
      "values, numbers and operators round-trip"
    );
    Check(
      formula.Mask == (OverlayValueCatalog.Bit(OverlayValueId.Combo) | OverlayValueCatalog.Bit(OverlayValueId.Miss)),
      "the mask names its values"
    );
    Check(OverlayFormula.Parse("{gone}*2+{combo}").Serialize() == "2+{combo}", "unknown values drop out");
    Check(OverlayFormula.Parse("{song-title}+1").Serialize() == "1", "words can't take part in arithmetic");
    Check(OverlayFormula.Parse("song-title").IsWords, "words stand alone");
    Check(OverlayFormula.Parse("2 × 3 ÷ 4 − 1").Serialize() == "2*3/4-1", "display symbols read as operators");
    Check(OverlayFormula.Parse("").IsEmpty && OverlayFormula.Parse(null).IsEmpty, "nothing is an empty formula");
    Check(
      OverlayFormula.Parse("12.5").IsPlainNumber(out double number) && number == 12.5,
      "a plain number stays a number"
    );
    Check(
      OverlayFormula.Parse("{time-total}-{time-current}").Describe(value => value.EnglishName)
        == "Total time − Elapsed time",
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
    Check(!OverlayFormula.Parse("{combo}/{miss}").TryEvaluate(numbers, out _, out _), "dividing by zero shows nothing");
    numbers.Remove(OverlayValueId.Combo);
    Check(!OverlayFormula.Parse("{combo}+1").TryEvaluate(numbers, out _, out _), "a value without data shows nothing");
    Check(!OverlayFormula.Parse("song-title").TryEvaluate(numbers, out _, out _), "words have no number");
  }

  private static void KeypadEditsFormulas()
  {
    OverlayFormula formula = OverlayFormula.FromValue(OverlayValueId.Combo);
    Check(!formula.CanType('7'), "a number can't follow a value without an operator");
    Check(formula.Type('*') && formula.AwaitsOperand, "an operator waits for what comes next");
    Check(formula.Type('/') && formula.Serialize() == "{combo}/", "another operator replaces it");
    formula.Type('2');
    formula.Type('.');
    Check(!formula.CanType('.') && formula.Type('5') && formula.Serialize() == "{combo}/2.5", "one point per number");
    Check(formula.Backspace() && formula.Serialize() == "{combo}/2.", "backspace removes a digit");
    formula.Backspace();
    formula.Backspace();
    Check(formula.Serialize() == "{combo}/", "then the number, then the operator");
    Check(formula.CanJoin(OverlayValueId.Miss), "a value can join after an operator");
    formula.Drop(OverlayValueId.Miss);
    Check(formula.Serialize() == "{combo}/{miss}", "a dropped value joins");
    formula.Drop(OverlayValueId.Fps);
    Check(formula.Serialize() == "fps", "without an operator a dropped value replaces the formula");
    Check(!formula.CanJoin(OverlayValueId.SongTitle), "words never join arithmetic");
    OverlayFormula typed = new();
    Check(
      !typed.CanType('+') && typed.Type('-') && typed.Type('0') && typed.Type('7'),
      "a minus starts a negative number"
    );
    Check(typed.Serialize() == "-7", "a leading zero gives way: " + typed.Serialize());
    typed.Type('+');
    Check(
      typed.Trimmed().Serialize() == "-7" && typed.Serialize() == "-7+",
      "trimming drops what is still being typed"
    );
  }
}
