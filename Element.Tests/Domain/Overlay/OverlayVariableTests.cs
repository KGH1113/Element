using System.Collections.Generic;
using System.Linq;
using Element.Application.Settings;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Tests.Support;
using static Element.Tests.Domain.Overlay.OverlayFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

// Variables: pieces of every type, conditions with five slots and groups;
// how they save, what they give, what a field of each type takes, and the
// move from colors that followed a value.
internal static class OverlayVariableTests
{
  [Suite("variables hold typed values, conditions and groups, and say why they don't fit")]
  public static void Run()
  {
    PiecesSaveAndRead();
    ConditionsGiveResultOrDefault();
    TypesDecideWhatFits();
    LeftoverAtKeysDropOut();
    ComparisonsAndPointsAreValues();
    ValueColorsBecomeConditions();
  }

  private static void PiecesSaveAndRead()
  {
    string[] saved =
    {
      "{combo}*2+{miss}",
      "\"Acc \" {accuracy} \"%\"",
      "#5AC8FA",
      "true",
      "if({accuracy};>=;100;{highlight-color};#FFFFFF)",
      "\"x\" ({combo}*2)",
      "if({miss};=;0;\"FC\";)",
    };
    foreach (string text in saved)
      Check(
        OverlayVariable.Parse(text).Serialize() == text,
        "saves as read: " + text + " → " + OverlayVariable.Parse(text).Serialize()
      );
    Check(OverlayVariable.Parse("combo").Serialize() == "combo", "one value still saves as its key, as 1.1 did");
    Check(
      OverlayVariable.Parse("\"a \\\"b\\\" \\\\\"")[0].Text == "a \"b\" \\",
      "quotes and backslashes in words come back"
    );
    OverlayVariable condition = OverlayVariable.Parse("if({accuracy};>=;100;60;40)");
    Check(
      condition.Count == 1
        && condition[0].Kind == OverlayPieceKind.Condition
        && condition[0].Slots[OverlayPiece.Compare][0].Op == '≥',
      "a condition is one piece with five slots"
    );
    Check(condition.Mask.Contains(OverlayValueId.Accuracy), "the values in its slots are read");
    Check(OverlayVariable.Parse("{song-title}+1").Serialize() == "1", "words can't be calculated with");
  }

  private static void ConditionsGiveResultOrDefault()
  {
    Numbers numbers = new() { [OverlayValueId.Accuracy] = 99.5, [OverlayValueId.Combo] = 120 };
    OverlayVariable size = OverlayVariable.Parse("if({combo};>=;100;60;40)");
    Check(size.TryNumber(numbers, out double result, out _) && result == 60, "the result while it holds");
    numbers[OverlayValueId.Combo] = 50;
    Check(size.TryNumber(numbers, out result, out _) && result == 40, "the default while it doesn't");
    numbers.Remove(OverlayValueId.Combo);
    Check(size.TryNumber(numbers, out result, out _) && result == 40, "and while the value has no data");
    OverlayVariable tiers = OverlayVariable.Parse("if({accuracy};=;100;#FFD84D;if({accuracy};>=;98;#5AC8FA;#FFFFFF))");
    Check(
      tiers.TryColor(numbers, out OverlayPiece color) && color.Text == "#5AC8FA",
      "a condition in a default makes tiers"
    );
    numbers[OverlayValueId.Accuracy] = 100;
    Check(tiers.TryColor(numbers, out color) && color.Text == "#FFD84D", "the outer condition first");
    Check(
      OverlayVariable.Parse("10+if({accuracy};=;100;5;0)*2").TryNumber(numbers, out result, out _) && result == 20,
      "a condition is a number in a calculation"
    );
    Check(
      OverlayVariable.Parse("if({accuracy};>;1;false;true)").TryBool(numbers, out bool yes) && !yes,
      "conditions give true or false too"
    );
    Check(
      OverlayVariableEvaluator.Holds('=', .1 + .2, .3)
        && !OverlayVariableEvaluator.Holds('≠', 4, 4)
        && OverlayVariableEvaluator.Holds('≤', 4, 4),
      "numbers the same to six decimals are the same"
    );
  }

  private static OverlayTypeProblem Problem(string text, OverlayValueType type, bool mayBeEmpty = false) =>
    OverlayVariableTypes.Check(OverlayVariable.Parse(text), type, mayBeEmpty);

  private static void TypesDecideWhatFits()
  {
    Check(Problem("{combo}*2", OverlayValueType.Number) == null, "a calculation is a number");
    Check(
      Problem("\"Acc \" {accuracy}", OverlayValueType.Any) == null,
      "anything sits side by side where Any is taken"
    );
    Check(
      Problem("1 2", OverlayValueType.Number)?.Kind == OverlayTypeProblemKind.SideBySide,
      "a number field takes one value"
    );
    Check(
      Problem("{song-title}", OverlayValueType.Number)?.Kind == OverlayTypeProblemKind.WrongType,
      "words aren't a number"
    );
    Check(Problem("#FFFFFF", OverlayValueType.Number)?.Found == OverlayValueType.Color, "and say what they are");
    Check(
      Problem("", OverlayValueType.Number)?.Kind == OverlayTypeProblemKind.Empty,
      "an empty number can't be finished"
    );
    Check(Problem("", OverlayValueType.Any, true) == null, "an empty value in a text can");
    OverlayTypeProblem slot = Problem("if({combo};>=;;60;40)", OverlayValueType.Number);
    Check(
      slot?.Kind == OverlayTypeProblemKind.Empty && slot.Slot == OverlayPiece.Right,
      "an empty slot is named: " + slot?.Slot
    );
    Check(
      Problem("if({combo};>=;100;\"PERFECT\";40)", OverlayValueType.Number)?.Slot == OverlayPiece.Result,
      "a result must be what the field needs"
    );
    Check(
      Problem("if({combo};>=;100;\"FC\";)", OverlayValueType.Any) == null,
      "in Any a default may be empty: it shows nothing"
    );
    Check(
      Problem("if({combo};>=;100;#FFD84D;#FFFFFF)", OverlayValueType.Color) == null,
      "colors from a condition fit a color"
    );
    Check(
      OverlayVariableTypes.TypeOf(OverlayVariable.Parse("if({combo};>=;100;\"a\";\"b\")")) == OverlayValueType.Text,
      "a condition is what its result is"
    );
  }

  // A build that was never released saved a value the player named as {@key}.
  // No such value exists: the piece drops out like any key nobody knows, and
  // the rest of the variable is kept.
  private static void LeftoverAtKeysDropOut()
  {
    Check(OverlayVariable.Parse("{@v1}").IsEmpty, "a leftover {@key} alone leaves nothing");
    Check(
      OverlayVariable.Parse("{combo}*2+{@v1}").Serialize() == "{combo}*2",
      "in a calculation it goes with its operator: " + OverlayVariable.Parse("{combo}*2+{@v1}").Serialize()
    );
    Check(
      OverlayVariable.Parse("\"Acc \" {@v1} {accuracy}").Serialize() == "\"Acc \" {accuracy}",
      "beside other pieces only it goes: " + OverlayVariable.Parse("\"Acc \" {@v1} {accuracy}").Serialize()
    );
    OverlayVariable condition = OverlayVariable.Parse("if({@v1};>=;100;60;40)");
    Check(
      condition.Serialize() == "if(;>=;100;60;40)",
      "in a condition its slot is left empty: " + condition.Serialize()
    );
    Check(
      condition.TryNumber(new Numbers { [OverlayValueId.Combo] = 120 }, out double result, out _) && result == 40,
      "and the condition gives its default"
    );
  }

  // What a condition compares by, and where an element grows from, are
  // values of their own types: each takes one of its kind, or a condition
  // that gives one, and nothing else takes them.
  private static void ComparisonsAndPointsAreValues()
  {
    // Combo ≥ 100 compares Miss by "<", otherwise by "≥".
    OverlayVariable nested = OverlayVariable.Parse("if({miss};if({combo};>=;100;<;>=);1;60;40)");
    Check(
      OverlayVariableTypes.Check(nested, OverlayValueType.Number) == null
        && nested.Serialize() == "if({miss};if({combo};>=;100;<;>=);1;60;40)",
      "a condition's comparison may itself be a condition that gives comparisons: " + nested.Serialize()
    );
    Check(
      nested.TryNumber(new Numbers { [OverlayValueId.Combo] = 120, [OverlayValueId.Miss] = 0 }, out double many, out _)
        && many == 60
        && nested.TryNumber(
          new Numbers { [OverlayValueId.Combo] = 20, [OverlayValueId.Miss] = 0 },
          out double few,
          out _
        )
        && few == 40,
      "the comparison is worked out first: 0 < 1 holds with a high combo, 0 ≥ 1 doesn't with a low one"
    );
    Check(
      OverlayVariableTypes.Check(OverlayVariable.Parse("if({miss};{combo};1;60;40)"), OverlayValueType.Number)
        is { Slot: OverlayPiece.Compare },
      "a number can't be what a condition compares by"
    );

    OverlayVariable point = OverlayVariable.Parse("at(TopLeft)");
    Check(
      point.Count == 1
        && point[0].Kind == OverlayPieceKind.Point
        && point[0].Point == OverlayGrowth.TopLeft
        && point.Serialize() == "at(TopLeft)"
        && OverlayVariable.Parse("at(Auto)")[0].Point == OverlayGrowth.Auto
        && OverlayVariable.Parse("at(Nowhere)").IsEmpty,
      "a point saves by its name and reads back; a name nobody knows drops out"
    );
    OverlayVariable follows = OverlayVariable.Parse("if({progress-current};>=;50;at(TopRight);at(Auto))");
    Check(
      OverlayVariableTypes.Check(follows, OverlayValueType.Point) == null
        && follows.TryPoint(new Numbers { [OverlayValueId.ProgressCurrent] = 80 }, out OverlayGrowth late)
        && late == OverlayGrowth.TopRight
        && follows.TryPoint(new Numbers { [OverlayValueId.ProgressCurrent] = 10 }, out OverlayGrowth early)
        && early == OverlayGrowth.Auto,
      "a condition gives points: past half the level it grows from the top right"
    );
    Check(
      OverlayVariableTypes.Check(point, OverlayValueType.Any) != null
        && OverlayVariableTypes.Check(point, OverlayValueType.Number) != null
        && OverlayVariableTypes.Check(OverlayVariable.Parse("12"), OverlayValueType.Point) != null,
      "a point goes only where a point is needed, and only a point goes there"
    );
  }

  private static void ValueColorsBecomeConditions()
  {
    OverlayValueColor tiers = new() { Value = "accuracy", Mode = OverlayValueColorMode.Step };
    tiers.Stops.Add(OverlayValueColorStop.Custom(95, "#FFFFFF"));
    tiers.Stops.Add(OverlayValueColorStop.HighlightColor(100));
    tiers.Stops.Add(OverlayValueColorStop.Custom(99.5f, "#5AC8FA", .5f));
    OverlayColorSettings following = OverlayColorSettings.Custom("#00FF00");
    following.ValueColor = tiers;
    ElementSettings settings = new()
    {
      SettingsVersion = 9,
      OverlayLayouts = new()
      {
        new OverlayLayoutSettings
        {
          Id = "text:acc",
          Text = new OverlayTextSettings { Runs = { OverlayTextRun.ValueRun("accuracy", following) } },
        },
      },
    };
    SettingsMigrator.Migrate(settings);
    OverlayColorSettings converted = settings.OverlayLayouts[0].Text.Runs[0].Color;
    Check(
      converted.ValueColor == null && converted.Hex == "#FFFFFF" && !converted.Highlight,
      "the lowest stop is the color underneath"
    );
    Check(
      converted.Formula == "if({accuracy};>=;100;{highlight-color};if({accuracy};>=;99.5;#5AC8FA;#FFFFFF))",
      "the stops become conditions, the highest outside: " + converted.Formula
    );
    Check(
      OverlayVariable
        .Parse(converted.Formula)
        .TryColor(new Numbers { [OverlayValueId.Accuracy] = 99.7 }, out OverlayPiece color)
        && color.Text == "#5AC8FA",
      "and color as the stops did"
    );
  }
}
