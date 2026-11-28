using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Tests.Support;
using static Element.Tests.Domain.Overlay.OverlayFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

// A value with decimals is kept whole; each place reads it at as many
// decimals as it chose, cut the way the game writes that value, and a
// condition compares exactly that number.
internal static class ValueDigitsTests
{
  [Suite("a value is read at the decimals its place chose, cut the game's way")]
  public static void Run()
  {
    EachValueCutsLikeTheGame();
    DigitsSaveWithTheValue();
    ConditionsCompareWhatIsRead();
  }

  private static void EachValueCutsLikeTheGame()
  {
    OverlayValueDefinition progress = OverlayValueCatalog.Get(OverlayValueId.ProgressCurrent),
      accuracy = OverlayValueCatalog.Get(OverlayValueId.Accuracy),
      bpm = OverlayValueCatalog.Get(OverlayValueId.TileBpm),
      combo = OverlayValueCatalog.Get(OverlayValueId.Combo);
    Check(
      progress.HasDigits && progress.Digits == 0 && progress.Rounding == OverlayRounding.Floor,
      "progress shows whole, floored as the game does"
    );
    Check(
      accuracy.HasDigits && accuracy.Digits == 2 && accuracy.Rounding == OverlayRounding.Truncate,
      "X-Accuracy shows two decimals, cut as the game writes it"
    );
    Check(bpm.HasDigits && bpm.Rounding == OverlayRounding.Nearest, "BPM, which the game doesn't write, rounds");
    Check(!combo.HasDigits && combo.MostDigits == 0, "a count is always whole: no decimals to choose");

    Check(OverlayValueNumbers.Cut(99.6, 0, OverlayRounding.Floor) == 99, "progress 99.6 is 99, not 100");
    Check(OverlayValueNumbers.Cut(0.127, 1, OverlayRounding.Floor) == .1, "0.127 at one decimal is 0.1");
    // The game: "##0.00000" (rounded at the fifth), then the last digit dropped.
    Check(
      OverlayValueNumbers.Cut(99.989998, 4, OverlayRounding.Truncate) == 99.99,
      "float noise rounds up at the fifth"
    );
    Check(OverlayValueNumbers.Cut(99.52349, 4, OverlayRounding.Truncate) == 99.5234, "the fifth is dropped");
    Check(
      OverlayValueNumbers.Cut(99.523496, 4, OverlayRounding.Truncate) == 99.5235,
      "after rounding at the sixth into it"
    );
    Check(OverlayValueNumbers.Cut(99.999996, 4, OverlayRounding.Truncate) == 99.9999, "never 100 before it is 100");
    Check(OverlayValueNumbers.Cut(99.996, 2, OverlayRounding.Truncate) == 99.99, "at two decimals too");
    Check(OverlayValueNumbers.Cut(100, 2, OverlayRounding.Truncate) == 100, "100 is 100");
    Check(OverlayValueNumbers.Cut(179.996, 2, OverlayRounding.Nearest) == 180, "BPM rounds to the nearest");
    Check(
      OverlayValueNumbers.Scaled(99.52, 2, OverlayRounding.Truncate) == 9952,
      "scaled, it is a count of its last digit"
    );
  }

  private static void DigitsSaveWithTheValue()
  {
    OverlayVariable read = OverlayVariable.Parse("if({progress-current:1};!=;0;60;40)");
    OverlayPiece left = read[0].Slots[OverlayPiece.Left][0];
    Check(
      left.Kind == OverlayPieceKind.Value && left.Value == OverlayValueId.ProgressCurrent && left.Digits == 1,
      "{name:1} reads at one decimal"
    );
    Check(read.Serialize() == "if({progress-current:1};!=;0;60;40)", "and saves the same way: " + read.Serialize());
    OverlayVariable plain = OverlayVariable.Parse("accuracy");
    Check(plain[0].Digits == -1 && plain.Serialize() == "accuracy", "a value read as it shows saves by its name alone");
    plain[0].Digits = 3;
    Check(plain.Serialize() == "{accuracy:3}", "with decimals of its own it goes in braces: " + plain.Serialize());
    Check(OverlayVariable.Parse(plain.Serialize())[0].Digits == 3, "and reads back");
    Check(
      !plain.SameAs(OverlayVariable.Parse("accuracy")) && plain.Clone()[0].Digits == 3,
      "the decimals are part of the value"
    );
    Check(OverlayVariable.Parse("{accuracy:9}")[0].Digits == OverlayValueNumbers.Most, "no more than four");
  }

  private static void ConditionsCompareWhatIsRead()
  {
    Numbers numbers = new() { [OverlayValueId.ProgressCurrent] = .127, [OverlayValueId.Accuracy] = 99.996 };
    OverlayVariable whole = OverlayVariable.Parse("if({progress-current};!=;0;60;40)");
    OverlayVariable tenth = OverlayVariable.Parse("if({progress-current:1};!=;0;60;40)");
    Check(
      whole.TryNumber(numbers, out double a, out _) && a == 40,
      "read whole, progress 0.127 is 0: the default answer"
    );
    Check(tenth.TryNumber(numbers, out double b, out _) && b == 60, "read at one decimal it is 0.1, not 0: the result");
    Check(
      OverlayVariable.Parse("{accuracy}").TryNumber(numbers, out double shown, out _) && shown == 99.99,
      "X-Accuracy 99.996 calculates as the 99.99 it shows"
    );
    Check(
      OverlayVariable.Parse("{accuracy:4}*1").TryNumber(numbers, out double fine, out _) && fine == 99.996,
      "read at four decimals it is all of it: " + fine
    );
  }
}
