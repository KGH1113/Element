using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Judgement;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Values;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests;

/// <summary>Judgment counts keep the colors they have always had, wherever they are colored.</summary>
internal static class JudgementColorTests
{
  [Suite("judgment counts keep their colors")]
  public static void Run()
  {
    BuiltInRowKeepsItsColors();
    OnlyJudgmentsHaveColors();
  }

  private static void BuiltInRowKeepsItsColors()
  {
    string[] expected =
    {
      "overload #D958FF 0.5",
      "too-early #FF0000 0.5",
      "very-early #FF6F4E 0.5",
      "early-perfect #A0FF4E 0.5",
      "perfect-early #60FF4E 0.5",
      "pure-perfect #60FF4E 0.5",
      "perfect-late #60FF4E 0.5",
      "late-perfect #A0FF4E 0.5",
      "very-late #FF6F4E 0.5",
      "too-late #FF0000 0.5",
      "miss #D958FF 0.5",
    };
    List<OverlayTextRun> runs = OverlayDefaultRuns.Judgements(
      OverlayColorSettings.Custom(JudgementColors.Base, .5f),
      3
    );
    Check(runs.Count == expected.Length * 2 - 1, "the row has every count with a space between");
    for (int i = 0; i < runs.Count; i++)
    {
      OverlayTextRun run = runs[i];
      Check(run.Size == 3 && run.Color.Alpha == .5f, "every run keeps the row's size and opacity");
      if (i % 2 == 1)
      {
        Check(
          !run.IsValue && run.Text == " " && run.Color.Hex == JudgementColors.Base,
          "counts are spaced in the base color"
        );
        continue;
      }
      string shown =
        run.Value
        + " "
        + run.Color.Hex
        + " "
        + run.Color.Alpha.ToString(System.Globalization.CultureInfo.InvariantCulture);
      Check(run.IsValue && shown == expected[i / 2], "count " + i / 2 + " reads " + shown);
    }
  }

  private static void OnlyJudgmentsHaveColors()
  {
    int colored = 0;
    foreach (OverlayValueId id in Enum.GetValues(typeof(OverlayValueId)))
      if (JudgementColors.TryGet(id, out string hex))
      {
        colored++;
        Check(OverlayValueCatalog.Get(id).Category == OverlayValueCategory.Judgement, id + " is a judgment");
        Check(hex.Length == 7 && hex[0] == '#', id + " has a #RRGGBB color");
      }
    Check(colored == 9, "nine judgments have their own color; Overload and Miss take the text's");
    Check(
      !JudgementColors.TryGet(OverlayValueId.Overload, out _) && !JudgementColors.TryGet(OverlayValueId.Miss, out _),
      "Overload and Miss take the text's color"
    );
  }
}
