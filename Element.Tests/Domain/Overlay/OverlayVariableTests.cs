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

// Variables: pieces of every type, conditions with five slots, groups and
// the player's own values; how they save, what they give, what a field of
// each type takes, and the move from colors that followed a value.
internal static class OverlayVariableTests
{
  [Suite("variables hold typed values, conditions and groups, and say why they don't fit")]
  public static void Run()
  {
    PiecesSaveAndRead();
    ConditionsGiveResultOrDefault();
    TypesDecideWhatFits();
    MadeValuesAreShared();
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
      "{@grade}",
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

  private static void MadeValuesAreShared()
  {
    OverlayCustomValues.Load(
      new[] { Made("grade", "등급 색", "color", "if({accuracy};=;100;{highlight-color};#FFFFFF)") }
    );
    try
    {
      Numbers numbers = new() { [OverlayValueId.Accuracy] = 100 };
      OverlayVariable color = OverlayVariable.Parse("{@grade}");
      Check(
        color.TryColor(numbers, out OverlayPiece piece) && piece.Kind == OverlayPieceKind.Value,
        "a made value gives what its variable does"
      );
      Check(color.Mask.Contains(OverlayValueId.Accuracy), "and reads what it reads");
      Check(OverlayVariableTypes.Check(color, OverlayValueType.Color) == null, "it fits where its type does");
      Check(
        OverlayVariableTypes.Check(color, OverlayValueType.Number)?.Kind == OverlayTypeProblemKind.WrongType,
        "and nowhere else"
      );
      OverlayCustomValues.Load(new[] { Made("loop", "loop", "number", "{@loop}+1") });
      Check(
        !OverlayVariable.Parse("{@loop}").TryNumber(numbers, out _, out _),
        "a value that uses itself gives nothing, and stops"
      );
    }
    finally
    {
      OverlayCustomValues.Load(null);
    }
  }

  private static OverlayCustomValueSettings Made(string key, string name, string type, string variable) =>
    new()
    {
      Key = key,
      Name = name,
      Type = type,
      Variable = variable,
    };

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
